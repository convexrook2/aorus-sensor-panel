param(
    [string]$PythonwPath,
    [switch]$Remove
)

$ErrorActionPreference = 'Stop'
$taskName = 'Fleet Studio Local Viewer'
$serverPath = Join-Path $PSScriptRoot 'serve.py'
$logPath = Join-Path $env:LOCALAPPDATA 'FleetStudio\server.log'

if ($Remove) {
    $task = Get-ScheduledTask -TaskName $taskName -TaskPath '\' -ErrorAction SilentlyContinue
    if (-not $task) {
        Write-Output 'No Fleet Studio sign-in task is installed. Use Start-Viewer.cmd on demand.'
        return
    }
    $expectedArgs = '"' + $serverPath + '" --port 8770 --log-file "' + $logPath + '"'
    if (@($task.Actions).Count -ne 1 -or $task.Actions[0].Arguments -ne $expectedArgs -or $task.Actions[0].WorkingDirectory -ne $PSScriptRoot) {
        throw 'The named task belongs to another viewer path. Inspect it before removing it.'
    }
    if ($task.State -eq 'Running') {
        throw 'The viewer task is running. Verify its PID, executable, command line and owner, then stop that task before removing it.'
    }
    Unregister-ScheduledTask -TaskName $taskName -TaskPath '\' -Confirm:$false
    Write-Output 'Removed Fleet Studio sign-in startup. Use Start-Viewer.cmd on demand.'
    return
}

if (-not (Test-Path -LiteralPath $serverPath -PathType Leaf)) {
    throw "Fleet Studio server was not found: $serverPath"
}
if (-not $PythonwPath) {
    $pythonPath = (& py -3 -c 'import sys; print(sys.executable)').Trim()
    if ($LASTEXITCODE -ne 0 -or -not $pythonPath) {
        throw 'Python 3 was not found. Pass -PythonwPath with the full path to pythonw.exe.'
    }
    $PythonwPath = Join-Path (Split-Path -Parent $pythonPath) 'pythonw.exe'
}
$PythonwPath = (Resolve-Path -LiteralPath $PythonwPath -ErrorAction Stop).Path
if ([IO.Path]::GetFileName($PythonwPath) -ne 'pythonw.exe') {
    throw 'The server task requires pythonw.exe so it can run without a console window.'
}

$userId = [Security.Principal.WindowsIdentity]::GetCurrent().Name
$action = New-ScheduledTaskAction -Execute $PythonwPath -Argument ('"' + $serverPath + '" --port 8770 --log-file "' + $logPath + '"') -WorkingDirectory $PSScriptRoot
$trigger = New-ScheduledTaskTrigger -AtLogOn -User $userId
$principal = New-ScheduledTaskPrincipal -UserId $userId -LogonType Interactive -RunLevel Limited
$settings = New-ScheduledTaskSettingsSet -RestartCount 3 -RestartInterval (New-TimeSpan -Minutes 1) -ExecutionTimeLimit (New-TimeSpan -Seconds 0) -MultipleInstances IgnoreNew -StartWhenAvailable -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries

Register-ScheduledTask -TaskName $taskName -Action $action -Trigger $trigger -Principal $principal -Settings $settings -Description 'Serve the local, read-only Fleet Studio viewer at http://127.0.0.1:8770/ after sign-in.' -Force | Out-Null
Write-Output "Installed $taskName for $userId."
Write-Output "Server: $serverPath"
Write-Output "Python: $PythonwPath"
Write-Output "Log: $logPath"
Write-Output 'URL: http://127.0.0.1:8770/'
Write-Output 'Run Start-Viewer.cmd to start or reuse the registered task and open the viewer.'
