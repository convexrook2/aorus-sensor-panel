@echo off
cd /d "%~dp0.."
dotnet run --project "FleetStudio.Desktop\FleetStudio.Desktop.csproj"
if errorlevel 1 pause
