param([switch]$Official, [int]$Seconds = 10)
$ErrorActionPreference = 'Stop'
Set-Location -LiteralPath $PSScriptRoot
Start-Transcript -Path (Join-Path $PSScriptRoot 'artifacts\windows-fps-probe.txt') -Force
$prefix = if ($Official) { 'windows-fps-official' } else { 'windows-fps-probe' }
$outputPath = Join-Path $PSScriptRoot ('artifacts\' + $prefix + '.csv')
$executable = if ($Official) { Join-Path $PSScriptRoot 'tools\PresentMon\PresentMon-2.6.0-x64.exe' } else { 'C:\Program Files\NVIDIA Corporation\FrameViewSDK\bin\PresentMon_x64.exe' }
$sessionName = "X890Probe-$PID"
Write-Host "Capturing Windows presentation events for $Seconds seconds; session $sessionName"
$capture = Start-Process -FilePath $executable -ArgumentList ('--session_name ' + $sessionName + ' --output_file "' + $outputPath + '" --no_console_stats --timed ' + $Seconds + ' --terminate_after_timed --qpc_time_ms --exclude SensorPanel.exe --no_track_input') -RedirectStandardOutput (Join-Path $PSScriptRoot ('artifacts\' + $prefix + '-stdout.txt')) -RedirectStandardError (Join-Path $PSScriptRoot ('artifacts\' + $prefix + '-stderr.txt')) -WindowStyle Normal -Wait -PassThru
Write-Host "PresentMon exit code: $($capture.ExitCode)"
Get-Content (Join-Path $PSScriptRoot ('artifacts\' + $prefix + '-stdout.txt'))
Get-Content (Join-Path $PSScriptRoot ('artifacts\' + $prefix + '-stderr.txt'))
if (Test-Path -LiteralPath $outputPath) {
    Import-Csv -LiteralPath $outputPath | Group-Object Application | Select-Object Count, Name | Format-Table
    Write-Host "Saved $outputPath"
}
Stop-Transcript
