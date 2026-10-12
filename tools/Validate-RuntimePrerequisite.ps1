$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
Set-Location -LiteralPath $projectRoot
$manifest = Get-Content -LiteralPath '.\artifacts\installer\package-manifest.json' -Raw | ConvertFrom-Json
if ($manifest.SelfContained -ne $false) { throw 'Build the shared-runtime installer first.' }
$testRoot = Join-Path $projectRoot ('artifacts\runtime-prerequisite-' + (Get-Date -Format 'yyyyMMdd-HHmmss'))
$compiler = 'C:\Program Files (x86)\Inno Setup 6\ISCC.exe'
$cleanup = Join-Path $projectRoot 'artifacts\installer\LegacyCleanup.iss'
$results = @(foreach ($case in @('missing','wrong-major','preview-only','core-only')) {
    $caseRoot = Join-Path $testRoot $case
    $runtimeRoot = Join-Path $caseRoot 'fake-dotnet'
    $outputRoot = Join-Path $caseRoot 'compiler-output'
    $target = Join-Path $caseRoot 'install-must-not-exist'
    New-Item -ItemType Directory -Path $runtimeRoot,$outputRoot -Force | Out-Null
    if ($case -ne 'missing') {
        $version = switch ($case) { 'wrong-major' {'8.0.31'} 'preview-only' {'10.0.0-preview.1'} 'core-only' {'10.0.0'} }
        $core = Join-Path $runtimeRoot ('shared\Microsoft.NETCore.App\' + $version)
        New-Item -ItemType Directory -Path $core -Force | Out-Null
        Set-Content -LiteralPath (Join-Path $core 'coreclr.dll') -Value 'fixture, not executable'
        if ($case -ne 'core-only') {
            $desktop = Join-Path $runtimeRoot ('shared\Microsoft.WindowsDesktop.App\' + $version)
            New-Item -ItemType Directory -Path $desktop -Force | Out-Null
            Set-Content -LiteralPath (Join-Path $desktop 'PresentationFramework.dll') -Value 'fixture, not executable'
        }
    }
    & $compiler /Qp ('/DPackageRoot=' + $manifest.PackageRoot) ('/DOutputRoot=' + $outputRoot) ('/DUpgradeCleanup=' + $cleanup) ('/DRuntimeProbeRoot=' + $runtimeRoot) '.\Packaging\SensorPanel.iss' | Out-Host
    if ($LASTEXITCODE -ne 0) { throw ('Test installer compile failed: ' + $case) }
    $log = Join-Path $caseRoot 'blocked-install.log'
    $arguments = '/CURRENTUSER /VERYSILENT /SUPPRESSMSGBOXES /SP- /NORESTART /NOICONS /TASKS="" /DIR="' + $target + '" /LOG="' + $log + '"'
    $run = Start-Process (Join-Path $outputRoot 'AORUS-Sensor-Panel-FAN-MADE-Setup.exe') -ArgumentList $arguments -Wait -PassThru
    if ($run.ExitCode -eq 0 -or (Test-Path -LiteralPath $target)) { throw ('Missing-runtime gate did not stop installation: ' + $case) }
    if (-not (Select-String -LiteralPath $log -SimpleMatch 'Microsoft .NET 10 Desktop Runtime (x64) is required but was not found.' -Quiet)) { throw ('Required message absent: ' + $case) }
    Write-Host ('PASS: '+$case+' blocked before installation; correct runtime message logged.')
    [pscustomobject]@{Case=$case;ExitCode=$run.ExitCode;NoInstallFiles=$true;MessageVerified=$true}
})
$results | ConvertTo-Json | Set-Content -LiteralPath '.\artifacts\installer\runtime-prerequisite-tests.json' -Encoding UTF8
Write-Host 'The real shared .NET runtime and production installation were not modified.'
