@echo off
cd /d "%~dp0"
echo CPU and motherboard: embedded LibreHardwareMonitor with existing PawnIO.
echo GPU: installed NVIDIA NVML driver. Right-click this file and Run as administrator.
"%~dp0SensorCollector\bin\Release\net10.0-windows\SensorCollector.exe" --libre --count 3 --interval-ms 1000 --out "%~dp0artifacts\collector-libre.json"
if errorlevel 1 (
  echo Collector failed. Inspect the error above.
) else (
  echo Collection completed. Inspect source statuses for unavailable readings.
  echo Latest snapshot: artifacts\collector-libre.json
)
pause
