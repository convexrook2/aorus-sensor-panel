$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
Set-Location -LiteralPath $projectRoot
function Get-ArtifactHash([string]$Path) {
    $stream = [IO.File]::OpenRead($Path); $sha = [Security.Cryptography.SHA256]::Create()
    try { [BitConverter]::ToString($sha.ComputeHash($stream)).Replace('-','') }
    finally { $sha.Dispose(); $stream.Dispose() }
}
$packageRoot = Join-Path $projectRoot 'artifacts\package-win-x64'
$outputRoot = Join-Path $projectRoot 'artifacts\installer'
New-Item -ItemType Directory -Path $packageRoot,$outputRoot -Force | Out-Null
Start-Transcript -Path '.\artifacts\installer-build.txt' -Force
try {
    & '.\tools\Build-Branding.ps1'
    dotnet build '.\SensorPanel\SensorPanel.csproj' -c Package
    if ($LASTEXITCODE -ne 0) { throw 'Package build failed.' }
    & '.\SensorCollector\bin\Package\net10.0-windows\SensorCollector.exe' --self-test
    if ($LASTEXITCODE -ne 0) { throw 'Collector checks failed.' }
    $check = Start-Process '.\SensorPanel\bin\Package\net10.0-windows\SensorPanel.exe' -ArgumentList '--check-mapping' -WindowStyle Normal -Wait -PassThru
    if ($check.ExitCode -ne 0) { throw 'Panel mapping/resource checks failed.' }
    dotnet publish '.\SensorPanel\SensorPanel.csproj' -c Package -r win-x64 --self-contained true -p:PublishSingleFile=false -p:PublishTrimmed=false -p:DebugType=None -p:DebugSymbols=false -o $packageRoot
    if ($LASTEXITCODE -ne 0) { throw 'Self-contained publication failed.' }
    foreach ($required in @('SensorPanel.exe','SensorPanel.dll','coreclr.dll','wpfgfx_cor3.dll','PresentMon-2.6.0-x64.exe','README.txt')) {
        if (-not (Test-Path -LiteralPath (Join-Path $packageRoot $required))) { throw ('Missing packaged dependency: ' + $required) }
    }
    if ((Get-ArtifactHash (Join-Path $packageRoot 'PresentMon-2.6.0-x64.exe')) -ne 'B2A706BC6AD475749E3B7E3409263AA1E6906D45BDCF993F6DBC0F660188F1AF') { throw 'PresentMon release digest mismatch.' }
    & '.\tools\Package-Notices.ps1' -PackageRoot $packageRoot
    $packageCheck = Start-Process (Join-Path $packageRoot 'SensorPanel.exe') -ArgumentList '--check-mapping' -WindowStyle Normal -Wait -PassThru
    if ($packageCheck.ExitCode -ne 0) { throw 'Published resource/mapping check failed.' }
    $compiler = 'C:\Program Files (x86)\Inno Setup 6\ISCC.exe'
    if (-not (Test-Path -LiteralPath $compiler)) { throw 'Inno Setup compiler is unavailable.' }
    & $compiler ('/DPackageRoot=' + $packageRoot) ('/DOutputRoot=' + $outputRoot) '.\Packaging\SensorPanel.iss'
    if ($LASTEXITCODE -ne 0) { throw 'Installer compilation failed.' }
    $installer = Join-Path $outputRoot 'AORUS-Sensor-Panel-FAN-MADE-Setup.exe'
    $files = @(Get-ChildItem -LiteralPath $packageRoot -File -Recurse | ForEach-Object {
        [pscustomobject]@{Path=$_.FullName.Substring($packageRoot.Length+1);Bytes=$_.Length;SHA256=(Get-ArtifactHash $_.FullName)}
    })
    [pscustomobject]@{Version='1.0.1';CreatedAt=(Get-Date).ToString('o');Architecture='win-x64';SelfContained=$true;Canvas='1024x576';Installer=$installer;InstallerBytes=(Get-Item -LiteralPath $installer).Length;InstallerSHA256=(Get-ArtifactHash $installer);Files=$files} | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath '.\artifacts\installer\package-manifest.json' -Encoding UTF8
    Write-Host ('Single installer ready: ' + $installer)
    Write-Host 'Build/source/published-resource checks passed. Native UI and install round-trip validation follows.'
} catch { Write-Host ($_ | Out-String) -ForegroundColor Red; throw }
finally { Stop-Transcript }
