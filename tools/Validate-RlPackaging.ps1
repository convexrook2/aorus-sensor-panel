$ErrorActionPreference='Stop'
$repoPath=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
Set-Location -LiteralPath $repoPath
$output=Join-Path $repoPath ('artifacts\rl-package-check-'+(Get-Date -Format 'yyyyMMdd-HHmmss-fff'))
New-Item -ItemType Directory -Path $output -Force | Out-Null
Write-Host 'Source/PowerShell checks and Package/win-x64 protected publication only. No installer, app launch, installation or hardware check.'
git diff --check
if($LASTEXITCODE -ne 0){throw 'Whitespace/conflict-marker check failed.'}
foreach($relative in @('tools\Build-Installer.ps1','tools\Package-Notices.ps1','tools\Measure-MonitorResources.ps1','tools\Measure-FleetPair.ps1','tools\Validate-ProtectedAssets.ps1','FleetStudio\Install-Desktop.ps1')){
    $tokens=$null;$errors=$null
    [void][Management.Automation.Language.Parser]::ParseFile((Join-Path $repoPath $relative),[ref]$tokens,[ref]$errors)
    if($errors.Count){$errors | Format-List;throw "PowerShell parse failed: $relative"}
    Write-Host "PASS: PowerShell parse / $relative"
}
dotnet publish .\SensorPanel\SensorPanel.csproj -c Package -r win-x64 --self-contained false --tl:off -p:DebugType=None -p:DebugSymbols=false -o $output 2>&1 | Tee-Object -FilePath (Join-Path $output 'publish-check.txt') | Out-Host
if($LASTEXITCODE -ne 0){throw 'Package/win-x64 protected publish failed.'}
dotnet .\tools\RlPack\bin\Package\net10.0\RlPack.dll verify --archive (Join-Path $output 'assets.rl') --key (Join-Path $env:LOCALAPPDATA 'LennonBuildKeys\X890.Sensor-v2.key') --source (Join-Path $repoPath 'Assets') --product X890.Sensor
if($LASTEXITCODE -ne 0){throw 'Package archive differs from source.'}
foreach($root in @('FleetViewer','SolarNavigation','Models','Ships','AORUS')){
    if(Test-Path -LiteralPath (Join-Path $output $root)){throw "Loose protected root leaked: $root"}
}
if(@(Get-ChildItem -LiteralPath $output -Recurse -File | Where-Object Extension -in @('.key','.cs','.pdb')).Count){throw 'Unexpected secret/source/debug file in package.'}
if(@(Get-ChildItem -LiteralPath $output -File -Filter 'RlPack*').Count){throw 'Build-only tooling leaked into package.'}
[pscustomobject]@{Passed=$true;Configuration='Package';RuntimeIdentifier='win-x64';ArchiveSha256=(Get-FileHash -LiteralPath (Join-Path $output 'assets.rl')).Hash;InstallerBuilt=$false;Installed=$false} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $output 'result.json')
Write-Host "PASS: release packaging configuration protects exact assets; no installer built. $output"
