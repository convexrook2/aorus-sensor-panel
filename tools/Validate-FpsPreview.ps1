$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
Set-Location -LiteralPath $projectRoot
Start-Transcript -Path (Join-Path $projectRoot 'artifacts\fps-preview-validation.txt') -Force
try {
    $panel = Join-Path $projectRoot 'SensorPanel\bin\FpsFix\net10.0-windows\SensorPanel.exe'
    foreach ($ownedPreview in @(Get-Process -Name SensorPanel -ErrorAction SilentlyContinue | Where-Object { $_.Path -eq $panel })) {
        if (-not $ownedPreview.CloseMainWindow() -or -not $ownedPreview.WaitForExit(8000)) { throw 'Close the saved-data preview before rebuilding.' }
    }
    Write-Host 'Separate FpsFix build: source checks and saved-data GPU memory-clock layout preview; existing live panel preserved.'
    dotnet build .\SensorPanel\SensorPanel.csproj -c FpsFix -t:Rebuild -p:Optimize=true
    if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
    $collector = Join-Path $projectRoot 'SensorCollector\bin\FpsFix\net10.0-windows\SensorCollector.exe'
    $panel = Join-Path $projectRoot 'SensorPanel\bin\FpsFix\net10.0-windows\SensorPanel.exe'
    & $collector --self-test
    if ($LASTEXITCODE -ne 0) { throw 'Collector checks failed.' }
    $check = Start-Process $panel -ArgumentList '--check-mapping' -WindowStyle Normal -Wait -PassThru
    if ($check.ExitCode -ne 0) { throw 'Panel checks failed.' }
    Get-Content .\artifacts\panel-mapping-check.txt
    $game = Get-Process -Name 'Le Mans Ultimate' -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($game) {
        Write-Host ('Testing initialized NVIDIA public SDK for Le Mans Ultimate PID ' + $game.Id + '. No elevation or service changes.')
        & $collector --frameview --fps-pid $game.Id --count 3 --out .\artifacts\fps-nvidia-initialized.json
        Write-Host 'Eight live Windows ETW FPS samples for the same game PID; foreground switching cannot change the target.'
        & $collector --presentmon --fps-pid $game.Id --count 8 --out .\artifacts\fps-windows-direct.json
        if ($LASTEXITCODE -ne 0) { throw 'Windows FPS collector failed.' }
        $fpsResult = Get-Content .\artifacts\fps-windows-direct.json -Raw | ConvertFrom-Json
        $fpsReading = $fpsResult.readings | Where-Object { $_.source -eq 'Windows ETW / PresentMon' -and $_.status -eq 'ok' }
        if (-not $fpsReading) { throw 'No valid Windows FPS sample; inspect fps-windows-direct.json.' }
        Write-Host ('Windows displayed FPS: ' + $fpsReading.value + ' for ' + $fpsReading.name + ', sampled ' + $fpsReading.sampledAt)
    }
    $fixture = Get-Content .\artifacts\panel-live.json -Raw | ConvertFrom-Json
    foreach ($reading in $fixture.readings) {
        if ($reading.source -eq 'NVIDIA NVML' -and $reading.id.EndsWith('/memory-clock')) { $reading.value = 14001 }
    }
    if (Test-Path .\artifacts\fps-windows-direct.json) {
        $fpsData = Get-Content .\artifacts\fps-windows-direct.json -Raw | ConvertFrom-Json
        $fixture.readings = @($fixture.readings | Where-Object unit -ne 'FPS') + @($fpsData.readings | Where-Object unit -eq 'FPS')
        $fixture.sources = @($fixture.sources | Where-Object source -ne 'NVIDIA FrameView SDK') + @($fpsData.sources | Where-Object source -eq 'Windows ETW / PresentMon')
    }
    $fixturePath = Join-Path $projectRoot 'artifacts\gpu-memory-layout-fixture.json'
    $fixture | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath $fixturePath -Encoding UTF8
    $capture = Join-Path $projectRoot 'artifacts\gpu-memory-layout-preview.png'
    Write-Host 'Launching visible saved-data preview with deliberate 14001 MHz layout fixture.'
    $preview = Start-Process $panel -ArgumentList ('--preview "' + $fixturePath + '" --capture "' + $capture + '"') -WindowStyle Normal -PassThru
    Write-Host ('Preview PID: ' + $preview.Id)
}
finally { Stop-Transcript }
