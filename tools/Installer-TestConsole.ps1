param([Parameter(Mandatory=$true)][string]$QueueDirectory)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
Set-Location -LiteralPath $projectRoot
$Host.UI.RawUI.WindowTitle = 'AORUS FAN MADE — installer validation'
$testPanel = Join-Path $projectRoot 'artifacts\installer-smoke\SensorPanel.exe'
$originalPanel = Join-Path $projectRoot 'SensorPanel\bin\Release\net10.0-windows\SensorPanel.exe'
$originalClosed = $false
function Close-ExactPanel([string]$Path) {
    foreach ($record in @(Get-CimInstance Win32_Process -Filter "Name='SensorPanel.exe'" | Where-Object ExecutablePath -eq $Path)) {
        $owned = Get-Process -Id $record.ProcessId
        for ($attempt=0; $attempt -lt 3 -and -not $owned.HasExited; $attempt++) {
            [void]$owned.CloseMainWindow(); [void]$owned.WaitForExit(3000); $owned.Refresh()
        }
        if (-not $owned.HasExited) { throw ('Close this panel before proceeding: ' + $Path) }
    }
}
New-Item -ItemType Directory -Path $QueueDirectory -Force | Out-Null
[pscustomobject]@{PID=$PID;StartedAt=(Get-Date).ToString('o');Elevated=([Security.Principal.WindowsPrincipal]::new([Security.Principal.WindowsIdentity]::GetCurrent())).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $QueueDirectory 'console.json') -Encoding UTF8
Write-Host 'One reusable validation console. All build/install/startup/cleanup jobs run here.'
while ($true) {
    $request = Get-ChildItem -LiteralPath $QueueDirectory -Filter '*.request.json' | Sort-Object Name | Select-Object -First 1
    if (-not $request) { Start-Sleep -Milliseconds 300; continue }
    $job = Get-Content -LiteralPath $request.FullName -Raw | ConvertFrom-Json
    $result = [ordered]@{Action=$job.Action;StartedAt=(Get-Date).ToString('o');OK=$false;Error=$null}
    Write-Host ('Running ' + $job.Action) -ForegroundColor Cyan
    try {
        switch ($job.Action) {
            'Install' { & '.\tools\Validate-Installer.ps1' -Phase Install }
            'VerifyInstalled' { & '.\tools\Validate-Installer.ps1' -Phase VerifyInstalled }
            'Build' { & '.\tools\Build-Installer.ps1' }
            'PollingStep' { & '.\tools\Validate-Polling.ps1' -Stage $job.Stage }
            'StartLive' {
                Close-ExactPanel $testPanel
                $previous = @(Get-CimInstance Win32_Process -Filter "Name='SensorPanel.exe'" | Where-Object ExecutablePath -eq $originalPanel)
                if ($previous.Count -gt 0) { Close-ExactPanel $originalPanel; $originalClosed = $true }
                $scheduler = New-Object -ComObject Schedule.Service; $scheduler.Connect()
                $receipt = Get-Content -LiteralPath '.\artifacts\installer\install-test.json' -Raw | ConvertFrom-Json
                $task = $scheduler.GetFolder('\').GetTask($receipt.TaskName)
                $null = $task.Run($null)
                $live = @()
                for ($attempt=0; $attempt -lt 25 -and $live.Count -eq 0; $attempt++) {
                    Start-Sleep -Milliseconds 800
                    $live = @(Get-CimInstance Win32_Process -Filter "Name='SensorPanel.exe'" | Where-Object ExecutablePath -eq $testPanel)
                }
                if ($live.Count -ne 1) { throw 'Scheduled startup did not create exactly one installed live panel.' }
                $result.LivePID = $live[0].ProcessId
                $result.TaskState = $task.State
                $result.TaskPath = $task.Definition.Actions.Item(1).Path
                Write-Host ('Scheduled task started the installed live panel, PID ' + $live[0].ProcessId)
            }
            'CheckLive' {
                $live = @(Get-CimInstance Win32_Process -Filter "Name='SensorPanel.exe'" | Where-Object ExecutablePath -eq $testPanel)
                if ($live.Count -ne 1) { throw 'Expected one installed live panel.' }
                $snapshot = Get-Content -LiteralPath '.\artifacts\panel-live.json' -Raw | ConvertFrom-Json
                if (-not $snapshot.elevated -or [DateTimeOffset]::UtcNow - [DateTimeOffset]::Parse($snapshot.capturedAt) -gt [TimeSpan]::FromSeconds(35)) { throw 'Scheduled panel snapshot is not elevated and fresh.' }
                Copy-Item -LiteralPath '.\artifacts\panel-live.json' -Destination '.\artifacts\installer\fullscreen-live.json'
                $required = @('cpu/core-voltage','ram/mclk','libre/amdcpu/0/temperature/2')
                foreach ($id in $required) { if (-not ($snapshot.readings | Where-Object {$_.id -eq $id -and $_.status -eq 'ok'})) { throw ('Missing live sensor: ' + $id) } }
                $helpers = @(Get-CimInstance Win32_Process -Filter "Name='PresentMon-2.6.0-x64.exe'" | Where-Object ParentProcessId -eq $live[0].ProcessId)
                if ($helpers.Count -ne 1) { throw 'Expected exactly one FPS helper for the installed panel.' }
                $result.LivePID=$live[0].ProcessId; $result.HelperPID=$helpers[0].ProcessId; $result.Elevated=$snapshot.elevated
                $snapshot.readings | Where-Object { $_.id -in $required -or $_.id -match '/temperature$' } | Select-Object name,value,unit,status | Format-Table -AutoSize
            }
            'UninstallAndRestore' {
                & '.\tools\Validate-Installer.ps1' -Phase Uninstall
                if ($originalClosed) { $restored = Start-Process $originalPanel -WindowStyle Normal -PassThru; $result.RestoredPID=$restored.Id; $originalClosed=$false }
            }
            'Exit' { $result.OK=$true; $result.CompletedAt=(Get-Date).ToString('o'); $result | ConvertTo-Json | Set-Content -LiteralPath ($request.FullName -replace '\.request\.json$','.result.json') -Encoding UTF8; Remove-Item -LiteralPath $request.FullName; Write-Host 'Validation complete; closing this console.'; exit }
            default { throw 'Unknown validation action; nothing executed.' }
        }
        $result.OK=$true
    } catch { $result.Error=($_ | Out-String); Write-Host $result.Error -ForegroundColor Red }
    $result.CompletedAt=(Get-Date).ToString('o')
    $result | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath ($request.FullName -replace '\.request\.json$','.result.json') -Encoding UTF8
    Remove-Item -LiteralPath $request.FullName
    Write-Host 'Waiting for the next validation step in this same window.'
}
