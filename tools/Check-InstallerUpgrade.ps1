[CmdletBinding()]
param(
    [ValidatePattern('^$|^[0-9a-f]{7,40}$')][string]$BaselineRevision = '',
    [ValidateSet('visible','tray','stubborn','missing-runtime')]
    [string[]]$Cases = @('visible','tray','stubborn','missing-runtime')
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$repo = Split-Path $PSScriptRoot -Parent
$root = Join-Path $repo ('artifacts\installer\upgrade-check-' + (Get-Date -Format 'yyyyMMdd-HHmmss'))
New-Item -ItemType Directory -Path $root -Force | Out-Null
$compiler = 'C:\Program Files (x86)\Inno Setup 6\ISCC.exe'
$csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$package = Join-Path $root 'package'
$old = Join-Path $root 'old'
New-Item -ItemType Directory -Path $package,$old -Force | Out-Null
foreach ($build in @(@{Dir=$old;Define='OLD'},@{Dir=$package;Define='NEW'})) {
    & $csc /nologo /target:winexe /platform:x64 /reference:System.Windows.Forms.dll /reference:System.Drawing.dll ('/define:'+$build.Define) ('/out:'+(Join-Path $build.Dir 'SensorPanel.exe')) (Join-Path $PSScriptRoot 'fixtures\InstallerUpgrade.cs')
    if ($LASTEXITCODE -ne 0) { throw 'Fixture compilation failed.' }
}
$source = Get-Content -LiteralPath (Join-Path $repo 'Packaging\SensorPanel.iss') -Raw
if ($BaselineRevision) {
    $source = (git -C $repo show "${BaselineRevision}:Packaging/SensorPanel.iss") -join "`n"
    if ($LASTEXITCODE -ne 0) { throw 'Cannot read committed installer baseline.' }
}
# Only isolate identity and install side effects. Keep the production shutdown,
# mutex and prerequisite code verbatim, so this exercises the actual policy.
$source = $source -replace '(?m)^AppId=.*$', 'AppId=InstallerUpgradeFixture'
$source = $source -replace '(?m)^AppName=.*$', 'AppName=Isolated installer upgrade fixture'
$source = $source -replace '(?m)^PrivilegesRequired=.*$', 'PrivilegesRequired=lowest'
$source = $source -replace '(?m)^SetupIconFile=.*$', ('SetupIconFile='+ (Join-Path $repo 'Assets\Branding\AorusFanMade.ico'))
$source = $source -replace '(?m)^InfoBeforeFile=.*$', ('InfoBeforeFile='+ (Join-Path $repo 'Packaging\README.txt'))
$source = $source -replace '(?ms)^\[(Tasks|Icons|Run|UninstallRun)\]\r?\n.*?(?=^\[)', ''
$source = $source.Replace('[Setup]', "[Setup]`nUninstallable=no`nCreateUninstallRegKey=no`nUsePreviousAppDir=no`nUsePreviousTasks=no")
$script = Join-Path $root 'fixture.iss'
$source | Set-Content -LiteralPath $script
$cleanup = Join-Path $root 'cleanup.iss'
'; No production cleanup in this isolated fixture.' | Set-Content -LiteralPath $cleanup
$results = [Collections.Generic.List[object]]::new()
$owned = [Collections.Generic.List[object]]::new()
function Start-Fixture([string]$Directory, [string]$Mode) {
    New-Item -ItemType Directory -Path $Directory -Force | Out-Null
    Copy-Item -LiteralPath (Join-Path $old 'SensorPanel.exe') -Destination $Directory
    $process = Start-Process -FilePath (Join-Path $Directory 'SensorPanel.exe') -ArgumentList $Mode -PassThru
    $owned.Add($process)
    $limit = [datetime]::UtcNow.AddSeconds(15)
    while (!(Test-Path -LiteralPath (Join-Path $Directory 'ready.txt'))) {
        if ($process.HasExited -or [datetime]::UtcNow -gt $limit) { throw "Fixture did not start: $Mode" }
        Start-Sleep -Milliseconds 100
    }
    return $process
}
try {
    $decoy = Start-Fixture (Join-Path $root 'unrelated-same-name') 'decoy'
    if ($BaselineRevision) { $Cases = @('visible') }
    foreach ($case in $Cases) {
        $caseRoot = Join-Path $root $case
        $target = Join-Path $caseRoot 'installed'
        $out = Join-Path $caseRoot 'setup'
        New-Item -ItemType Directory -Path $out -Force | Out-Null
        [string[]]$probe = @()
        if ($case -eq 'missing-runtime') { $probe += '/DRuntimeProbeRoot='+(Join-Path $caseRoot 'no-runtime') }
        & $compiler /Qp ('/DPackageRoot='+$package) ('/DOutputRoot='+$out) ('/DUpgradeCleanup='+$cleanup) @probe $script | Out-Host
        if ($LASTEXITCODE -ne 0) { throw "Installer fixture compile failed: $case" }
        $mode = if ($case -eq 'missing-runtime') { 'tray' } else { $case }
        $app = Start-Fixture $target $mode
        $settings = Join-Path $target 'preferences.json'
        '{"theme":"AORUS","startup":false}' | Set-Content -LiteralPath $settings
        $beforeSettings = (Get-FileHash -LiteralPath $settings).Hash
        $beforeExe = (Get-FileHash -LiteralPath (Join-Path $target 'SensorPanel.exe')).Hash
        $log = Join-Path $caseRoot 'install.log'
        # SILENT still shows progress. No force-close command-line override: test
        # the shipped script's default, not a wrapper that hides a wrong default.
        $arguments = '/CURRENTUSER /SILENT /SUPPRESSMSGBOXES /SP- /NORESTART /NOICONS /LOGCLOSEAPPLICATIONS /DIR="'+$target+'" /LOG="'+$log+'"'
        Write-Host "Running $case upgrade; target PID $($app.Id), unrelated PID $($decoy.Id)."
        $setup = Start-Process -FilePath (Join-Path $out 'AORUS-Sensor-Panel-FAN-MADE-Setup.exe') -ArgumentList $arguments -PassThru
        $owned.Add($setup)
        if (!$setup.WaitForExit(120000)) { throw "Installer did not finish within 120 seconds: $case" }
        $setup.Refresh(); $app.Refresh(); $decoy.Refresh()
        $afterExe = (Get-FileHash -LiteralPath (Join-Path $target 'SensorPanel.exe')).Hash
        if ($BaselineRevision -or $case -eq 'missing-runtime') {
            if ($setup.ExitCode -eq 0 -or $app.HasExited -or $afterExe -ne $beforeExe) { throw 'Expected install gate did not preserve the running target.' }
            $expected = if ($BaselineRevision) { 'currently running' } else { '.NET 10 Desktop Runtime (x64) is required' }
            if (!(Select-String -LiteralPath $log -SimpleMatch $expected -Quiet)) { throw 'Expected gate diagnostic missing.' }
        } else {
            if ($setup.ExitCode -ne 0 -or !$app.HasExited) { throw "Upgrade did not close target and succeed: $case / exit $($setup.ExitCode)" }
            if ($afterExe -ne (Get-FileHash -LiteralPath (Join-Path $package 'SensorPanel.exe')).Hash -or $afterExe -eq $beforeExe) { throw 'Upgraded executable does not match the new package.' }
            if (!(Select-String -LiteralPath $log -SimpleMatch 'RestartManager' -Quiet)) { throw 'No Restart Manager evidence in installer log.' }
        }
        if ($decoy.HasExited) { throw 'Installer closed the unrelated same-name program.' }
        if ((Get-FileHash -LiteralPath $settings).Hash -ne $beforeSettings) { throw 'Existing preferences changed.' }
        $results.Add([pscustomobject]@{Case=$case;BaselineRevision=$BaselineRevision;ExitCode=$setup.ExitCode;TargetClosed=$app.HasExited;UnrelatedSurvived=$true;PreferencesPreserved=$true;InstallerLog=$log})
        Write-Host "PASS $case; target closed=$($app.HasExited), unrelated process and preferences preserved."
        if (!$app.HasExited) { $app.Kill(); $app.WaitForExit() }
    }
} finally {
    # Only exact process handles created by this test; never enumerate by name.
    foreach ($process in $owned) { if (!$process.HasExited) { $process.Kill(); $process.WaitForExit() }; $process.Dispose() }
    $results | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $root 'results.json')
}
Write-Host "Installer upgrade evidence: $root"
