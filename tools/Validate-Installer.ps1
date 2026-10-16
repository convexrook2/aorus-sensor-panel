param([ValidateSet('Install','VerifyInstalled','Uninstall')][string]$Phase = 'Install',
    [switch]$VerifyLiveSensors,[string[]]$RequiredSensorIds=@())
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
Set-Location -LiteralPath $projectRoot
$testRoot = [IO.Path]::GetFullPath((Join-Path $projectRoot 'artifacts\installer-smoke'))
if ($testRoot -ne [IO.Path]::GetFullPath((Join-Path $projectRoot 'artifacts\installer-smoke'))) { throw 'Unexpected installer test target.' }
$installedPanel = Join-Path $testRoot 'SensorPanel.exe'
if ($Phase -eq 'Install' -and (Test-Path -LiteralPath $installedPanel)) {
    Write-Host 'Resuming verification of the existing isolated test installation; no files or startup tasks are replaced.'
    $Phase = 'VerifyInstalled'
}
$sid = [Security.Principal.WindowsIdentity]::GetCurrent().User.Value
$taskName = 'AORUS-FAN-MADE-1B7C308F-' + $sid
$shortcut = Join-Path ([Environment]::GetFolderPath('CommonDesktopDirectory')) 'AORUS Sensor Panel - FAN MADE.lnk'
$receiptPath = Join-Path $projectRoot 'artifacts\installer\install-test.json'
Start-Transcript -Path (Join-Path $projectRoot ('artifacts\installer-' + $Phase.ToLowerInvariant() + '-test.txt')) -Force
try {
    if (-not ([Security.Principal.WindowsPrincipal]::new([Security.Principal.WindowsIdentity]::GetCurrent())).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) { throw 'Run this visible validation script as administrator.' }
    $scheduler = New-Object -ComObject Schedule.Service; $scheduler.Connect(); $folder = $scheduler.GetFolder('\')
    if ($Phase -ne 'Uninstall') {
        if ($Phase -eq 'Install') {
        if (Test-Path -LiteralPath $installedPanel) { throw 'Installer test files already exist; complete the prior uninstall phase first.' }
        if (Test-Path -LiteralPath $shortcut) { throw 'A production desktop shortcut already exists; preserve it and stop this test.' }
        $existing = @($folder.GetTasks(0) | Where-Object Name -eq $taskName)
        if ($existing.Count -ne 0) { throw 'An existing startup task must be preserved; this test will not replace it.' }
        $registryPath = 'HKLM:\Software\Microsoft\Windows\CurrentVersion\Uninstall\{1B7C308F-694A-4C92-8C95-218009316F78}_is1'
        if (Test-Path -LiteralPath $registryPath) { throw 'An installed package already exists; this test will not overwrite it.' }
        $installer = Join-Path $projectRoot 'artifacts\installer\AORUS-Sensor-Panel-FAN-MADE-Setup.exe'
        Write-Host 'Visible installer test: isolated workspace directory, desktop shortcut and optional startup task enabled.'
        $arguments = '/SILENT /SP- /NORESTART /ALLUSERS /DIR="' + $testRoot + '" /GROUP="AORUS Sensor Panel FAN MADE - Installer Test" /TASKS="desktopicon,startup" /LOG="' + (Join-Path $projectRoot 'artifacts\installer\install.log') + '"'
        $setup = Start-Process $installer -ArgumentList $arguments -WindowStyle Normal -Wait -PassThru
        if ($setup.ExitCode -ne 0) { throw ('Installer failed with exit code ' + $setup.ExitCode) }
        }
        $manifest = Get-Content -LiteralPath '.\artifacts\installer\package-manifest.json' -Raw | ConvertFrom-Json
        foreach ($file in $manifest.Files) {
            $target = [IO.Path]::GetFullPath((Join-Path $testRoot $file.Path))
            if (-not $target.StartsWith($testRoot + '\',[StringComparison]::OrdinalIgnoreCase)) { throw 'Package manifest escaped test target.' }
            $stream = [IO.File]::OpenRead($target); $sha = [Security.Cryptography.SHA256]::Create()
            try { $hash = [BitConverter]::ToString($sha.ComputeHash($stream)).Replace('-','') } finally {$sha.Dispose();$stream.Dispose()}
            if ($hash -ne $file.SHA256) { throw ('Installed file digest mismatch: ' + $file.Path) }
        }
        if (-not (Test-Path -LiteralPath $shortcut)) { throw 'Desktop shortcut was not installed.' }
        $shell = New-Object -ComObject WScript.Shell; $link = $shell.CreateShortcut($shortcut)
        if ($link.TargetPath -ne $installedPanel -or $link.IconLocation.Split(',')[0] -ne $installedPanel) { throw 'Desktop shortcut target/icon mismatch.' }
        $task = $folder.GetTask($taskName)
        [xml]$xml = $task.Xml
        function Resolve-TaskUserSid([string]$User) {
            if ($User.StartsWith('S-1-', [StringComparison]::OrdinalIgnoreCase)) { return ([Security.Principal.SecurityIdentifier]::new($User)).Value }
            return ([Security.Principal.NTAccount]::new($User)).Translate([Security.Principal.SecurityIdentifier]).Value
        }
        if (-not $task.Enabled -or $xml.Task.Principals.Principal.RunLevel -ne 'HighestAvailable' -or
            $xml.Task.Principals.Principal.LogonType -ne 'InteractiveToken' -or
            (Resolve-TaskUserSid $xml.Task.Triggers.LogonTrigger.UserId) -ne $sid -or
            (Resolve-TaskUserSid $xml.Task.Principals.Principal.UserId) -ne $sid -or
            $xml.Task.Actions.Exec.Command -ne $installedPanel -or
            $xml.Task.Triggers.LogonTrigger.Delay -ne 'PT10S' -or
            $xml.Task.Settings.ExecutionTimeLimit -ne 'PT0S') { throw 'Installed startup task contract failed.' }
        $task.Xml | Set-Content -LiteralPath '.\artifacts\installer\startup-task-verified.xml' -Encoding UTF8
        $check = Start-Process $installedPanel -ArgumentList '--check-mapping' -WindowStyle Normal -Wait -PassThru
        if ($check.ExitCode -ne 0) { throw 'Installed mapping check failed.' }
        $snapshot = & (Join-Path $PSScriptRoot 'New-PreviewSnapshot.ps1')
        $capture = Join-Path $projectRoot 'artifacts\installer\installed-16x9.png'
        $preview = Start-Process $installedPanel -ArgumentList ('--preview "' + $snapshot + '" --capture "' + $capture + '"') -WindowStyle Normal -PassThru
        [pscustomobject]@{InstalledAt=(Get-Date).ToString('o');InstalledFilesVerified=$manifest.Files.Count;DesktopShortcutVerified=$true;StartupTaskVerified=$true;TaskName=$taskName;TaskUserSID=$sid;TestRoot=$testRoot;PreviewPID=$preview.Id;UninstallVerified=$false;NativeUIPending=$true} | ConvertTo-Json | Set-Content -LiteralPath $receiptPath -Encoding UTF8
        Write-Host 'Installed file digests, desktop icon, startup task and installed mappings passed. Visible UI validation follows.'
    } else {
        if (-not (Test-Path -LiteralPath $receiptPath)) { throw 'No recorded install test to uninstall.' }
        $receipt = Get-Content -LiteralPath $receiptPath -Raw | ConvertFrom-Json
        if ($receipt.TestRoot -ne $testRoot) { throw 'Receipt target mismatch; stop cleanup.' }
        $live = @(Get-CimInstance Win32_Process -Filter "Name='SensorPanel.exe'" | Where-Object ExecutablePath -eq $installedPanel)
        if ($live.Count -eq 1 -and $VerifyLiveSensors) {
            $snapshot = Get-Content -LiteralPath '.\artifacts\panel-live.json' -Raw | ConvertFrom-Json
            if (-not $snapshot.elevated -or [DateTimeOffset]::UtcNow - [DateTimeOffset]::Parse($snapshot.capturedAt) -gt [TimeSpan]::FromSeconds(35)) { throw 'Installed live snapshot must be elevated and current.' }
            foreach ($id in $RequiredSensorIds) {
                if (-not ($snapshot.readings | Where-Object {$_.id -eq $id -and $_.status -eq 'ok'})) { throw ('Missing real live sensor ID: ' + $id) }
            }
            $helpers = @(Get-CimInstance Win32_Process -Filter "Name='PresentMon-2.6.0-x64.exe'" | Where-Object {$_.ParentProcessId -eq $live[0].ProcessId -and $_.ExecutablePath -eq (Join-Path $testRoot 'PresentMon-2.6.0-x64.exe')})
            if ($helpers.Count -gt 1) { throw 'Unexpected duplicate installed FPS helpers.' }
            Copy-Item -LiteralPath '.\artifacts\panel-live.json' -Destination '.\artifacts\installer\fullscreen-live.json'
            $receipt | Add-Member -NotePropertyName LiveSnapshotChecked -NotePropertyValue $true -Force
            $receipt | Add-Member -NotePropertyName RequiredSensorIds -NotePropertyValue $RequiredSensorIds -Force
            $receipt | Add-Member -NotePropertyName LivePID -NotePropertyValue $live[0].ProcessId -Force
            $receipt | Add-Member -NotePropertyName LiveHelperPID -NotePropertyValue $(if($helpers.Count){$helpers[0].ProcessId}else{$null}) -Force
            $receipt | ConvertTo-Json | Set-Content -LiteralPath $receiptPath -Encoding UTF8
            Write-Host 'PASS: scheduled installed app is elevated, its snapshot is current, and requested sensor checks passed.'
        }
        foreach ($process in @(Get-CimInstance Win32_Process -Filter "Name='SensorPanel.exe'" | Where-Object ExecutablePath -eq $installedPanel)) {
            $owned = Get-Process -Id $process.ProcessId
            if (-not $owned.CloseMainWindow() -or -not $owned.WaitForExit(8000)) { throw 'Close only the installer test preview before uninstalling.' }
        }
        $uninstaller = Join-Path $testRoot 'unins000.exe'
        Write-Host ('Visible uninstall test for exactly ' + $testRoot)
        $remove = Start-Process $uninstaller -ArgumentList ('/SILENT /NORESTART /LOG="' + (Join-Path $projectRoot 'artifacts\installer\uninstall.log') + '"') -WindowStyle Normal -Wait -PassThru
        if ($remove.ExitCode -ne 0) { throw 'Test uninstall failed.' }
        if (Test-Path -LiteralPath $installedPanel) { throw 'Installed panel remained after uninstall.' }
        if (Test-Path -LiteralPath $shortcut) { throw 'Test desktop shortcut remained after uninstall.' }
        $left = @($folder.GetTasks(0) | Where-Object Name -eq $receipt.TaskName)
        if ($left.Count -ne 0) { throw 'Test startup task remained after uninstall.' }
        $receipt.UninstallVerified = $true
        $receipt | Add-Member -NotePropertyName UninstalledAt -NotePropertyValue (Get-Date).ToString('o') -Force
        $receipt | ConvertTo-Json | Set-Content -LiteralPath $receiptPath -Encoding UTF8
        Write-Host 'PASS: installed application, desktop shortcut and owned startup task removed. Existing project panel and PawnIO preserved.'
    }
} catch { Write-Host ($_ | Out-String) -ForegroundColor Red; throw }
finally { Stop-Transcript }
