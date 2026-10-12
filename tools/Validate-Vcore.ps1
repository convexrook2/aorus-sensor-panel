$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
Set-Location -LiteralPath $projectRoot
Start-Transcript -Path .\artifacts\vcore-validation.txt -Force
try {
    Write-Host 'Building separate FpsFix collector. Three CPU SVI3 telemetry refresh/read samples through existing PawnIO.'
    dotnet build .\SensorPanel\SensorPanel.csproj -c FpsFix -p:Optimize=true
    if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
    $collector = Join-Path $projectRoot 'SensorCollector\bin\FpsFix\net10.0-windows\SensorCollector.exe'
    & $collector --self-test
    if ($LASTEXITCODE -ne 0) { throw 'Source checks failed.' }
    & $collector --libre --count 3 --out .\artifacts\svi3-probe.json
    if ($LASTEXITCODE -ne 0) { throw 'Collector failed.' }
    $result = Get-Content .\artifacts\svi3-probe.json -Raw | ConvertFrom-Json
    $result.sources | Where-Object source -like '*SVI3*' | Format-List
    $result.readings | Where-Object id -like 'cpu/core-v*' | Format-List
}
finally { Stop-Transcript }
