param([string]$PythonExecutable = 'python')
$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path $PSScriptRoot -Parent
Set-Location -LiteralPath $repoRoot
$ciRoot = Join-Path $repoRoot 'artifacts\ci'
New-Item -ItemType Directory -Path $ciRoot -Force | Out-Null
Start-Transcript -LiteralPath (Join-Path $ciRoot 'validation.txt') -Force
$passed = $false
try {
    Write-Host 'X890 source/build/asset validation. No installation, live sensors, startup changes or deployment.'
    $head = (git rev-parse HEAD).Trim()
    if ($LASTEXITCODE -ne 0) { throw 'Cannot identify source revision.' }
    $dirty = @(git status --porcelain)
    Write-Host "Source HEAD: $head; working-tree entries: $($dirty.Count)"
    [pscustomobject]@{Head=$head;DirtyEntries=$dirty;BuildNumber=$env:BUILD_NUMBER;BuildUrl=$env:BUILD_URL} |
        ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $ciRoot 'source.json')
    git diff --check
    if ($LASTEXITCODE -ne 0) { throw 'Whitespace/conflict check failed.' }
    foreach ($folder in @('tools','FleetStudio')) {
        foreach ($file in Get-ChildItem -LiteralPath (Join-Path $repoRoot $folder) -Filter '*.ps1' -File -Recurse) {
            if ($file.FullName -match '[\\/](bin|obj)[\\/]') { continue }
            $tokens=$null; $errors=$null
            [void][Management.Automation.Language.Parser]::ParseFile($file.FullName,[ref]$tokens,[ref]$errors)
            if ($errors.Count) { $errors | Out-Host; throw "PowerShell syntax failed: $($file.Name)" }
        }
    }
    & .\tools\Check-PublicContent.ps1
    $version = ([xml](Get-Content .\SensorPanel\SensorPanel.csproj -Raw)).Project.PropertyGroup.Version
    $studioVersion = ([xml](Get-Content .\FleetStudio.Desktop\FleetStudio.Desktop.csproj -Raw)).Project.PropertyGroup.Version
    if ($studioVersion -ne $version) { throw 'Fleet Studio and monitor versions disagree.' }
    if ((Get-Content .\Packaging\SensorPanel.iss -Raw) -notmatch ('#define AppVersion "'+[regex]::Escape($version)+'"') -or
        (Get-Content .\Packaging\README.txt -Raw) -notmatch ('Version '+[regex]::Escape($version)+' \|') -or
        !(Test-Path -LiteralPath ".\Packaging\ReleaseNotes-$version.md")) { throw 'Release version/notes disagree.' }
    & pwsh -NoLogo -NoProfile -File .\tools\Check-SensorReleaseFlow.ps1
    if ($LASTEXITCODE -ne 0) { throw 'Isolated release-phase safety checks failed.' }
    & pwsh -NoLogo -NoProfile -File .\tools\Check-FleetAssetSnapshot.ps1
    if ($LASTEXITCODE -ne 0) { throw 'Asset immutability/Jenkins cleanliness checks failed.' }
    & $PythonExecutable .\tools\Sync-FleetViewer.py --check
    if ($LASTEXITCODE -ne 0) { throw 'Monitor fleet integrity failed.' }
    & $PythonExecutable .\tools\Check-FleetPortraits.py --self-test
    if ($LASTEXITCODE -ne 0) { throw 'Portrait validator regression checks failed.' }
    & $PythonExecutable .\tools\Check-FleetPortraits.py
    if ($LASTEXITCODE -ne 0) { throw 'Prepared ship portraits failed provenance/resolution checks.' }
    & $PythonExecutable .\FleetStudio\tools\verify_assets.py
    if ($LASTEXITCODE -ne 0) { throw 'Fleet Studio asset integrity failed.' }
    node .\tools\Check-RocinanteRelease.mjs
    if ($LASTEXITCODE -ne 0) { throw 'Finished Rocinante identity/outlet checks failed.' }
    # Canonical adopted source only; this also runs the bow-light and shared
    # emission-glow contracts without depending on a generated review bundle.
    & $PythonExecutable .\tools\Check-AnubisAdoption.py
    if ($LASTEXITCODE -ne 0) { throw 'Adopted Anubis identity/bow and shared fleet-glow contracts failed.' }
    & $PythonExecutable .\tools\Check-ArboghastAdoption.py
    if ($LASTEXITCODE -ne 0) { throw 'Arboghast adoption check failed.' }
    & $PythonExecutable .\tools\Check-CanterburyAdoption.py
    if ($LASTEXITCODE -ne 0) { throw 'Adopted Canterbury source/model/portrait identity failed.' }
    node .\tools\Check-FleetPlumes.mjs
    if ($LASTEXITCODE -ne 0) { throw 'Fleet exhaust emission/lighting/scale/lifecycle contracts failed.' }
    node .\tools\Check-ShowcaseMotion.mjs
    if ($LASTEXITCODE -ne 0) { throw 'Showcase tilt/return, pause and rendering cadence contracts failed.' }
    node .\tools\Check-HolographicStage.mjs
    if ($LASTEXITCODE -ne 0) { throw 'Holographic platform motion, visibility, cadence and disposal contracts failed.' }
    node .\tools\Check-ShipInspection.mjs
    if ($LASTEXITCODE -ne 0) { throw 'Ship-only inspection and fixed hologram/camera contracts failed.' }
    foreach ($script in @('Assets/FleetViewer/viewer.js','Assets/FleetViewer/rocinante-effects.js','Assets/FleetViewer/light-glow.js','FleetStudio/review.js','FleetStudio/portal.js','FleetStudio/ship-effects.js','FleetStudio/light-glow.js')) {
        node --check $script
        if ($LASTEXITCODE -ne 0) { throw "JavaScript syntax failed: $script" }
    }
    dotnet restore .\X890.slnx -p:NuGetAudit=true -p:NuGetAuditMode=all --tl:off
    if ($LASTEXITCODE -ne 0) { throw 'Restore or dependency audit failed.' }
    dotnet build .\X890.slnx -c Release --no-restore --tl:off
    if ($LASTEXITCODE -ne 0) { throw 'Release solution build failed.' }
    & .\SensorCollector\bin\Release\net10.0-windows\SensorCollector.exe --self-test-managed
    if ($LASTEXITCODE -ne 0) { throw 'Managed sensor contracts failed.' }
    $mapping = Start-Process .\SensorPanel\bin\Release\net10.0-windows\SensorPanel.exe -ArgumentList '--check-mapping' -Wait -PassThru -WindowStyle Normal
    if ($mapping.ExitCode -ne 0) { throw 'Panel mapping/resource contracts failed.' }
    $rlTool = Join-Path $repoRoot 'tools\RlPack\bin\Release\net10.0\RlPack.dll'
    dotnet $rlTool self-test --output (Join-Path $ciRoot 'archive-checks')
    if ($LASTEXITCODE -ne 0) { throw 'Authenticated archive/rejection contracts failed.' }
    foreach ($project in @('SensorPanel','FleetStudio.Desktop')) {
        $publishRoot = Join-Path $repoRoot ('artifacts\ci-publish-'+$project+'-'+(Get-Date -Format 'yyyyMMdd-HHmmss-fff'))
        dotnet publish ".\$project\$project.csproj" -c Release -r win-x64 --self-contained false --tl:off -p:DebugType=None -p:DebugSymbols=false -o $publishRoot
        if ($LASTEXITCODE -ne 0) { throw "Protected publication failed: $project" }
        $product = if ($project -eq 'SensorPanel') { 'X890.Sensor' } else { 'X890.FleetStudio' }
        $source = if ($project -eq 'SensorPanel') { Join-Path $repoRoot 'Assets' } else { $repoRoot }
        dotnet $rlTool verify --archive (Join-Path $publishRoot 'assets.rl') --key (Join-Path $env:LOCALAPPDATA "LennonBuildKeys\$product-v2.key") --source $source --product $product
        if ($LASTEXITCODE -ne 0) { throw "Encrypted asset/source mismatch: $project" }
        foreach ($root in @('FleetViewer','SolarNavigation','FleetStudio','Models','Ships','AORUS')) {
            if (Test-Path -LiteralPath (Join-Path $publishRoot $root)) { throw "Loose protected assets: $project/$root" }
        }
        if (@(Get-ChildItem -LiteralPath $publishRoot -Recurse -File | Where-Object Extension -in @('.key','.cs','.pdb')).Count -or
            @(Get-ChildItem -LiteralPath $publishRoot -File -Filter 'RlPack*').Count) { throw 'Build key/source/debug/tooling leaked into publish.' }
        [pscustomobject]@{Product=$product;ArchiveSHA256=(Get-FileHash -LiteralPath (Join-Path $publishRoot 'assets.rl')).Hash;ExactSourceParity=$true} |
            ConvertTo-Json | Set-Content -LiteralPath (Join-Path $ciRoot ($project+'-assets.json'))
    }
    if ($env:JENKINS_URL) {
        $changes = @(git status --porcelain --untracked-files=no)
        if ($LASTEXITCODE -ne 0 -or $changes.Count) { throw 'CI changed tracked source files.' }
    }
    $passed = $true
    Write-Host 'PASS: X890 source/build/managed/resource/asset gates. Native rendering, installation and hardware acceptance remain separate.'
} finally {
    [pscustomobject]@{Passed=$passed;CompletedAt=[datetimeoffset]::Now;Installed=$false;LiveSensorsTested=$false} |
        ConvertTo-Json | Set-Content -LiteralPath (Join-Path $ciRoot 'result.json')
    Stop-Transcript
}
