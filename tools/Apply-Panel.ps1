$ErrorActionPreference = 'Stop'
& (Join-Path $PSScriptRoot 'Cleanup-FpsCheck.ps1')
& (Join-Path $PSScriptRoot 'Validate-Panel.ps1')
