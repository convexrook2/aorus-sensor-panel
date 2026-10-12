@echo off
cd /d "%~dp0"
echo Direct sensor collector: Gigabyte firmware, NVIDIA driver, Windows CPU usage.
echo This is a three-sample run. No fan, voltage, clock, or firmware settings are changed.
"%~dp0SensorCollector\bin\Release\net10.0-windows\SensorCollector.exe" --count 3 --interval-ms 1000 --out "%~dp0artifacts\collector-latest.json"
echo.
echo Snapshot saved to artifacts\collector-latest.json. Unavailable readings remain unavailable.
pause
