$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
Set-Location -LiteralPath $projectRoot
Start-Transcript -Path .\artifacts\automatic-fps-validation.txt -Force
try {
    dotnet build .\SensorPanel\SensorPanel.csproj -c FpsFix -p:Optimize=true
    if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
    $collector = Join-Path $projectRoot 'SensorCollector\bin\FpsFix\net10.0-windows\SensorCollector.exe'
    $panel = Join-Path $projectRoot 'SensorPanel\bin\FpsFix\net10.0-windows\SensorPanel.exe'
    & $collector --self-test
    if ($LASTEXITCODE -ne 0) { throw 'Source checks failed.' }
    $mapping = Start-Process $panel -ArgumentList '--check-mapping' -WindowStyle Normal -Wait -PassThru
    if ($mapping.ExitCode -ne 0) { throw 'Panel mapping failed.' }
    & $collector --list-games | Tee-Object .\artifacts\detected-games.json
    Write-Host 'Eight Windows FPS samples with automatic game discovery; no PID override.'
    $capturePath = Join-Path $projectRoot 'artifacts\automatic-fps.json'
    $capture = Start-Process $collector -ArgumentList ('--presentmon --count 8 --out "' + $capturePath + '"') -NoNewWindow -Wait -PassThru
    if ($capture.ExitCode -ne 0) { throw 'Automatic collector failed.' }
    $result = Get-Content .\artifacts\automatic-fps.json -Raw | ConvertFrom-Json
    $result.readings | Where-Object unit -eq 'FPS' | Format-List
    $leftover = @(Get-CimInstance Win32_Process -Filter "Name='PresentMon-2.6.0-x64.exe'" | Where-Object { $_.ExecutablePath -eq (Join-Path (Split-Path $collector) 'PresentMon-2.6.0-x64.exe') -and $_.ParentProcessId -eq $capture.Id })
    Write-Host ('Helper cleanup check: ' + $leftover.Count + ' matching running helpers.')
    if ($leftover.Count -ne 0) { throw 'The owned FPS helper did not stop.' }
    $trace = @(logman query -ets | Select-String ('X890Panel-' + $capture.Id + '-'))
    if ($trace.Count -ne 0) { throw 'The owned FPS trace did not stop.' }
}
finally { Stop-Transcript }
