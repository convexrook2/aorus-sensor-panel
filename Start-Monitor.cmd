@echo off
cd /d "%~dp0"
dotnet run --project "SensorPanel\SensorPanel.csproj" -- --windowed
if errorlevel 1 pause
