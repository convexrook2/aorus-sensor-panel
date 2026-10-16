param([switch]$BuildOnly)
# Compatibility entry point: Belter assets now render inside the original diagram.
& (Join-Path $PSScriptRoot 'Validate-SolarNavigation.ps1') -BuildOnly:$BuildOnly
