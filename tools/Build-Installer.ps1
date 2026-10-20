$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
Set-Location -LiteralPath $projectRoot
function Get-ArtifactHash([string]$Path) {
    $stream = [IO.File]::OpenRead($Path); $sha = [Security.Cryptography.SHA256]::Create()
    try { [BitConverter]::ToString($sha.ComputeHash($stream)).Replace('-','') }
    finally { $sha.Dispose(); $stream.Dispose() }
}
$packageRoot = Join-Path $projectRoot ('artifacts\package-win-x64-shared-' + (Get-Date -Format 'yyyyMMdd-HHmmss-fff'))
$outputRoot = Join-Path $projectRoot 'artifacts\installer'
New-Item -ItemType Directory -Path $packageRoot,$outputRoot -Force | Out-Null
Start-Transcript -Path '.\artifacts\installer-build.txt' -Force
try {
    $version = ([xml](Get-Content -LiteralPath '.\SensorPanel\SensorPanel.csproj' -Raw)).Project.PropertyGroup.Version
    if ((Get-Content -LiteralPath '.\Packaging\SensorPanel.iss' -Raw) -notmatch ('#define AppVersion "'+[regex]::Escape($version)+'"')) { throw 'Installer/app versions disagree.' }
    $sourceHead = (git rev-parse HEAD).Trim()
    if ($LASTEXITCODE -ne 0) { throw 'Cannot record package source identity.' }
    $sourceStatus = @(git status --porcelain)
    Write-Host "Building installer $version from $sourceHead with $($sourceStatus.Count) working-tree entries. Does not install or launch live sensors."
    & '.\tools\Check-PublicContent.ps1'
    python '.\tools\Sync-FleetViewer.py' --check
    if ($LASTEXITCODE -ne 0) { throw 'Desktop fleet asset checks failed.' }
    # Preserve accepted branding pixels. Regenerate only when preparing a checkout
    # that genuinely lacks the existing generated icon/PNG.
    if (!(Test-Path -LiteralPath '.\Assets\Branding\AorusFanMade.ico') -or
        !(Test-Path -LiteralPath '.\Assets\Branding\AorusFanMade.png')) {
        & '.\tools\Build-Branding.ps1'
    }
    dotnet build '.\SensorPanel\SensorPanel.csproj' -c Package -p:BuildFrameViewBridge=true
    if ($LASTEXITCODE -ne 0) { throw 'Package build failed.' }
    & '.\SensorCollector\bin\Package\net10.0-windows\SensorCollector.exe' --self-test
    if ($LASTEXITCODE -ne 0) { throw 'Collector checks failed.' }
    $check = Start-Process '.\SensorPanel\bin\Package\net10.0-windows\SensorPanel.exe' -ArgumentList '--check-mapping' -WindowStyle Normal -Wait -PassThru
    if ($check.ExitCode -ne 0) { throw 'Panel mapping/resource checks failed.' }
    dotnet publish '.\SensorPanel\SensorPanel.csproj' -c Package -r win-x64 --self-contained false -p:BuildFrameViewBridge=true -p:PublishSingleFile=false -p:PublishTrimmed=false -p:DebugType=None -p:DebugSymbols=false -o $packageRoot
    if ($LASTEXITCODE -ne 0) { throw 'Shared-runtime publication failed.' }
    foreach ($required in @('SensorPanel.exe','SensorPanel.dll','SensorPanel.runtimeconfig.json','PresentMon-2.6.0-x64.exe','PresentMon-2.6.0-x890-compat-x64.exe','PresentMon-COMPAT-NOTICES.txt','NvidiaFrameViewBridge.dll','README.txt','Holotable-Model-Credits.txt','assets.rl','Licenses\AssetCredits\CREDITS.txt')) {
        if (-not (Test-Path -LiteralPath (Join-Path $packageRoot $required))) { throw ('Missing packaged dependency: ' + $required) }
    }
    foreach ($protectedRoot in @('FleetViewer','SolarNavigation','Models','Ships','AORUS')) {
        if (Test-Path -LiteralPath (Join-Path $packageRoot $protectedRoot)) { throw "Protected publish contains loose asset folder: $protectedRoot" }
    }
    if (@(Get-ChildItem -LiteralPath $packageRoot -File -Recurse | Where-Object Extension -in @('.key','.cs','.pdb')).Count -or
        @(Get-ChildItem -LiteralPath $packageRoot -File -Filter 'RlPack*').Count) { throw 'Build-only key/source/debug/tooling leaked into installer payload.' }
    foreach ($excluded in @('coreclr.dll','hostfxr.dll','hostpolicy.dll','System.Private.CoreLib.dll','PresentationFramework.dll','wpfgfx_cor3.dll')) {
        if (Test-Path -LiteralPath (Join-Path $packageRoot $excluded)) { throw ('A private runtime was unexpectedly published: ' + $excluded) }
    }
    foreach ($requiredWebView in @('Microsoft.Web.WebView2.Core.dll','Microsoft.Web.WebView2.Wpf.dll')) {
        if (-not (Test-Path -LiteralPath (Join-Path $packageRoot $requiredWebView))) { throw ('Missing ship viewport dependency: ' + $requiredWebView) }
    }
    if (-not ((Test-Path -LiteralPath (Join-Path $packageRoot 'WebView2Loader.dll')) -or
        (Test-Path -LiteralPath (Join-Path $packageRoot 'runtimes\win-x64\native\WebView2Loader.dll')))) { throw 'Missing x64 WebView2 loader.' }
    $fleetManifest = Get-Content -LiteralPath (Join-Path $projectRoot 'Assets\FleetViewer\manifest.json') -Raw | ConvertFrom-Json
    $assetKey=Join-Path $env:LOCALAPPDATA 'LennonBuildKeys\X890.Sensor-v2.key'
    dotnet .\tools\RlPack\bin\Package\net10.0\RlPack.dll verify --key $assetKey --archive (Join-Path $packageRoot 'assets.rl') --source (Join-Path $projectRoot 'Assets') --product X890.Sensor
    if($LASTEXITCODE -ne 0){throw 'Encrypted fleet/navigation payload differs from source.'}
    $runtimeConfig = Get-Content -LiteralPath (Join-Path $packageRoot 'SensorPanel.runtimeconfig.json') -Raw | ConvertFrom-Json
    if ($runtimeConfig.runtimeOptions.includedFrameworks -or $runtimeConfig.runtimeOptions.rollForward -ne 'LatestPatch') { throw 'Expected shared runtime with compatible patch roll-forward.' }
    foreach ($framework in @('Microsoft.NETCore.App','Microsoft.WindowsDesktop.App')) {
        if (-not ($runtimeConfig.runtimeOptions.frameworks | Where-Object { $_.name -eq $framework -and $_.version -eq '10.0.0' })) { throw ('Shared runtime requirement mismatch: ' + $framework) }
    }
    if ((Get-ArtifactHash (Join-Path $packageRoot 'PresentMon-2.6.0-x64.exe')) -ne 'B2A706BC6AD475749E3B7E3409263AA1E6906D45BDCF993F6DBC0F660188F1AF') { throw 'PresentMon release digest mismatch.' }
    if ((Get-ArtifactHash (Join-Path $packageRoot 'PresentMon-2.6.0-x890-compat-x64.exe')) -ne (Get-ArtifactHash (Join-Path $projectRoot 'tools\PresentMon\PresentMon-2.6.0-x890-compat-x64.exe'))) { throw 'PresentMon compatibility repair was not packaged correctly.' }
    $nativeAssetRoot = Join-Path $projectRoot 'Assets\SolarNavigation'
    $nativeAssets = @(Get-ChildItem -LiteralPath (Join-Path $nativeAssetRoot 'textures') -File)
    $nativeAssets += @('eros','phoebe','tycho','medina','solgate') | ForEach-Object { Get-Item -LiteralPath (Join-Path $nativeAssetRoot ('models\' + $_ + '.fnm')) }
    & '.\tools\Package-Notices.ps1' -PackageRoot $packageRoot
    Copy-Item -LiteralPath (Join-Path $projectRoot "Packaging\ReleaseNotes-$version.md") -Destination (Join-Path $packageRoot 'ReleaseNotes.md')
    & (Join-Path $packageRoot 'SensorCollector.exe') --self-test
    if ($LASTEXITCODE -ne 0) { throw 'Published collector/native bridge checks failed.' }
    $packageCheck = Start-Process (Join-Path $packageRoot 'SensorPanel.exe') -ArgumentList '--check-mapping' -WindowStyle Normal -Wait -PassThru
    if ($packageCheck.ExitCode -ne 0) { throw 'Published resource/mapping check failed.' }
    $compiler = 'C:\Program Files (x86)\Inno Setup 6\ISCC.exe'
    if (-not (Test-Path -LiteralPath $compiler)) { throw 'Inno Setup compiler is unavailable.' }
    # Delete only recorded v1.0.0/1.0.1 package files absent from the new package.
    # Exact paths are scoped to {app}; the shared system runtime is never touched.
    $cleanupLines = @(foreach ($relative in Get-Content -LiteralPath '.\Packaging\LegacySelfContainedFiles.txt') {
        if ($relative.StartsWith('#') -or [string]::IsNullOrWhiteSpace($relative)) { continue }
        if ($relative -match '["{}:*?]' -or [IO.Path]::IsPathRooted($relative) -or ($relative.Split('\') -contains '..')) { throw 'Unsafe legacy cleanup path.' }
        if (-not (Test-Path -LiteralPath (Join-Path $packageRoot $relative))) { 'Type: files; Name: "{app}\' + $relative + '"' }
    })
    # Remove only exact former loose app assets on upgrade, never entire folders.
    $formerAssets=@($fleetManifest.files | ForEach-Object {'FleetViewer\'+$_.path.Replace('/','\')})+@('FleetViewer\manifest.json')
    $formerAssets+=@($nativeAssets | ForEach-Object {'SolarNavigation\'+$_.FullName.Substring($nativeAssetRoot.Length+1)})
    $formerAssets+=@('SolarNavigation\nasa-horizons.json.gz','SolarNavigation\nasa-checkpoints.json')
    $formerAssets+=@(Get-ChildItem -LiteralPath $nativeAssetRoot -File | Where-Object Extension -in @('.txt','.md') | ForEach-Object {'SolarNavigation\'+$_.Name})
    foreach($relative in $formerAssets | Sort-Object -Unique){
        if($relative -match '["{}:*?]' -or [IO.Path]::IsPathRooted($relative) -or ($relative.Split('\') -contains '..')){throw 'Unsafe old asset cleanup path.'}
        $cleanupLines+='Type: files; Name: "{app}\'+$relative+'"'
    }
    # Keep compiler input outside the installed package.
    $cleanupPath = Join-Path $outputRoot 'LegacyCleanup.iss'
    $cleanupLines | Set-Content -LiteralPath $cleanupPath -Encoding UTF8
    & $compiler ('/DPackageRoot=' + $packageRoot) ('/DOutputRoot=' + $outputRoot) ('/DUpgradeCleanup=' + $cleanupPath) '.\Packaging\SensorPanel.iss'
    if ($LASTEXITCODE -ne 0) { throw 'Installer compilation failed.' }
    $installer = Join-Path $outputRoot 'AORUS-Sensor-Panel-FAN-MADE-Setup.exe'
    $files = @(Get-ChildItem -LiteralPath $packageRoot -File -Recurse | ForEach-Object {
        [pscustomobject]@{Path=$_.FullName.Substring($packageRoot.Length+1);Bytes=$_.Length;SHA256=(Get-ArtifactHash $_.FullName)}
    })
    $installerHash = Get-ArtifactHash $installer
    [pscustomobject]@{Version=$version;CreatedAt=(Get-Date).ToString('o');SourceHead=$sourceHead;SourceStatus=$sourceStatus;Architecture='win-x64';SelfContained=$false;SharedRuntime='Microsoft .NET 10 Desktop Runtime (x64), stable 10.0.x';Canvas='Adaptive: landscape / wide strip / portrait';PackageRoot=$packageRoot;PackageBytes=($files | Measure-Object Bytes -Sum).Sum;Installer=$installer;InstallerBytes=(Get-Item -LiteralPath $installer).Length;InstallerSHA256=$installerHash;Files=$files} | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath '.\artifacts\installer\package-manifest.json' -Encoding UTF8
    ($installerHash.ToLowerInvariant()+'  '+[IO.Path]::GetFileName($installer)) | Set-Content -LiteralPath (Join-Path $outputRoot 'SHA256SUMS.txt') -Encoding ASCII
    Write-Host ('Single installer ready: ' + $installer)
    Write-Host 'Build/source/published-resource checks passed. Native UI and install round-trip validation follows.'
} catch { Write-Host ($_ | Out-String) -ForegroundColor Red; throw }
finally { Stop-Transcript }
