param([switch]$SourceOnly)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
$artifactDirectory = Join-Path $projectRoot 'artifacts'
New-Item -ItemType Directory -Path $artifactDirectory -Force | Out-Null
Start-Transcript -Path (Join-Path $artifactDirectory 'panel-upgrade-validation.txt') -Force | Out-Null
try {
    $panel = Join-Path $projectRoot 'SensorPanel\bin\Release\net10.0-windows\SensorPanel.exe'
    foreach ($existing in @(Get-CimInstance Win32_Process -Filter "Name='SensorPanel.exe'" | Where-Object { $_.ExecutablePath -eq $panel })) {
        if ($SourceOnly) { throw 'Close the project panel before rebuilding its Release files.' }
        Write-Host ('Closing only this project panel, PID ' + $existing.ProcessId)
        $ownedPanel = Get-Process -Id $existing.ProcessId
        if (-not $ownedPanel.CloseMainWindow() -or -not $ownedPanel.WaitForExit(8000)) { throw 'Panel did not close cleanly; close it manually and rerun.' }
    }
    Write-Host 'Release build: panel, collector, and FrameView native ABI bridge.'
    dotnet build (Join-Path $projectRoot 'SensorPanel\SensorPanel.csproj') -c Release
    if ($LASTEXITCODE -ne 0) { throw 'Build failed; validation stopped.' }
    $collector = Join-Path $projectRoot 'SensorCollector\bin\Release\net10.0-windows\SensorCollector.exe'
    $panel = Join-Path $projectRoot 'SensorPanel\bin\Release\net10.0-windows\SensorPanel.exe'
    & $collector --self-test
    if ($LASTEXITCODE -ne 0) { throw 'Collector source checks failed.' }
    $check = Start-Process -FilePath $panel -ArgumentList '--check-mapping' -WindowStyle Normal -Wait -PassThru
    if ($check.ExitCode -ne 0) { throw 'Panel mapping checks failed.' }
    Get-Content (Join-Path $artifactDirectory 'panel-mapping-check.txt')
    if ($SourceOnly) { Write-Host 'Release build and source checks complete. Privileged live launch remains owner-controlled.'; return }
    Write-Host 'Eight live samples: DDR5 ratio/BCLK, CPU/motherboard, NVIDIA NVML and Windows displayed FPS.'
    & $collector --libre --presentmon --count 8 --out (Join-Path $artifactDirectory 'collector-panel-upgrade.json')
    if ($LASTEXITCODE -ne 0) { throw 'Collector failed.' }
    $existingPanels = @(Get-CimInstance Win32_Process -Filter "Name='SensorPanel.exe'" | Where-Object { $_.ExecutablePath -eq $panel })
    if ($existingPanels.Count -gt 0) { throw 'The panel is already open. Close it before launching the new build.' }
    $capture = Join-Path $artifactDirectory 'panel-ui.png'
    Write-Host 'Launching the visible panel with animated artwork. No NVIDIA service is started by this script.'
    $launched = Start-Process -FilePath $panel -ArgumentList ('--capture "' + $capture + '"') -WindowStyle Normal -PassThru
    Write-Host ('Panel PID: ' + $launched.Id)
}
finally { Stop-Transcript | Out-Null }
