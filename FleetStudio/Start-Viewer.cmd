@echo off
setlocal
set "FLEET_URL=http://127.0.0.1:8770/"
set "FLEET_SERVER=%~dp0serve.py"
powershell -NoProfile -Command ^
  "$ErrorActionPreference='Stop';" ^
  "function Test-FleetStudio { try { return (Invoke-WebRequest -UseBasicParsing ($env:FLEET_URL+'health') -TimeoutSec 1).Content.Trim() -eq 'Fleet Studio ready' } catch { return $false } };" ^
  "if (Test-FleetStudio) { exit 0 };" ^
  "$task=Get-ScheduledTask -TaskName 'Fleet Studio Local Viewer' -ErrorAction SilentlyContinue; if (-not $task) { exit 2 };" ^
  "$expectedArgs='{0}{1}{0} --port 8770 --log-file {0}{2}{0}' -f [char]34,$env:FLEET_SERVER,(Join-Path $env:LOCALAPPDATA 'FleetStudio\server.log');" ^
  "if (@($task.Actions).Count -ne 1 -or $task.Actions[0].Arguments -ne $expectedArgs -or $task.Actions[0].WorkingDirectory -ne (Split-Path -Parent $env:FLEET_SERVER) -or [IO.Path]::GetFileName($task.Actions[0].Execute) -ne 'pythonw.exe') { throw 'The registered task belongs to another viewer path. Review it before running Install-LocalServer.ps1.' };" ^
  "if ($task.State -ne 'Running') { Write-Host 'Starting Fleet Studio Local Viewer...'; Start-ScheduledTask -InputObject $task };" ^
  "$timer=[Diagnostics.Stopwatch]::StartNew(); while ($timer.Elapsed.TotalSeconds -lt 10) { if (Test-FleetStudio) { exit 0 }; Start-Sleep -Milliseconds 200 };" ^
  "Write-Error 'Fleet Studio did not become ready. Inspect the task and %LOCALAPPDATA%\FleetStudio\server.log in a visible terminal.'; exit 1"
if errorlevel 2 goto fallback
if errorlevel 1 goto failed
if /I "%~1"=="--check" exit /b 0
start "" "%FLEET_URL%"
exit /b 0

:fallback
if /I "%~1"=="--check" exit /b 2
title Fleet Studio - close this window when finished
cd /d "%~dp0"
echo Fleet Studio: %FLEET_URL%
echo On-demand viewer: keep this console open while viewing ships.
echo When finished, close the viewer tab AND this console. Ctrl+C also stops the server.
echo.
where py >nul 2>nul
if errorlevel 1 (python "%FLEET_SERVER%" --port 8770 --open) else (py -3 "%FLEET_SERVER%" --port 8770 --open)
echo.
echo Viewer stopped. Open Start-Viewer.cmd to start it again.
pause
exit /b

:failed
echo Fleet Studio could not start. The registered task was left in place; no fallback was started.
if /I "%~1"=="--check" exit /b 1
pause
exit /b 1
