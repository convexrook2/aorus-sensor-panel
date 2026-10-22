param([switch]$SkipExport,[switch]$Review)
$ErrorActionPreference='Stop'
$repoPath=Split-Path $PSScriptRoot -Parent
Set-Location -LiteralPath $repoPath
Write-Host 'Build sensor panel, visibly export prepared full-model portraits, validate source assets, rebuild. No install or Fleet Studio launch.'
dotnet build SensorPanel/SensorPanel.csproj -c Debug
if($LASTEXITCODE -ne 0){throw 'Sensor panel build failed.'}
$appPath=Join-Path $repoPath 'SensorPanel/bin/Debug/net10.0-windows/SensorPanel.exe'
if(!$SkipExport){
    $outputPath=Join-Path $repoPath 'Assets/FleetViewer/portraits'
    $run=Start-Process -FilePath $appPath -ArgumentList @('--export-fleet-portraits',('"'+$outputPath+'"')) -WindowStyle Normal -Wait -PassThru
    if($run.ExitCode -ne 0){throw 'Visible portrait export failed.'}
}
python tools/Check-FleetPortraits.py
if($LASTEXITCODE -ne 0){throw 'Portrait model/hash/dimensions check failed.'}
$bundlePath=Join-Path $repoPath 'Assets/FleetViewer'
$manifestPath=Join-Path $bundlePath 'manifest.json'
$manifest=Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
$manifest.files=@(Get-ChildItem -LiteralPath $bundlePath -File -Recurse | Where-Object FullName -ne $manifestPath | Sort-Object FullName | ForEach-Object {
    @{path=[IO.Path]::GetRelativePath($bundlePath,$_.FullName).Replace('\','/');bytes=$_.Length;sha256=(Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant()}
})
$manifest | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath $manifestPath -Encoding utf8
python tools/Sync-FleetViewer.py --check
if($LASTEXITCODE -ne 0){throw 'Fleet bundle verification failed.'}
dotnet build SensorPanel/SensorPanel.csproj -c Debug
if($LASTEXITCODE -ne 0){throw 'Rebuild with the prepared portraits failed.'}
if($Review){
    Write-Host 'Visible saved-data native review: representative rotating showcases, direct pause/resume, shared holotable, layouts and inactive stop. No live sensor/GPU proof.'
    $began=Get-Date
    $previewPath=Join-Path $repoPath 'tools/preview-empty.json'
    $run=Start-Process -FilePath $appPath -ArgumentList @('--preview',('"'+$previewPath+'"'),'--windowed','--preview-theme','Rocinante','--check-showcase-rotation') -WindowStyle Normal -Wait -PassThru
    $reportPath=Join-Path $repoPath 'artifacts/showcase-rotation-check/result.json'
    if($run.ExitCode -ne 0 -or !(Test-Path -LiteralPath $reportPath) -or (Get-Item -LiteralPath $reportPath).LastWriteTime -lt $began){throw 'Native static fleet review failed or did not produce a fresh report.'}
    $result=Get-Content -LiteralPath $reportPath -Raw | ConvertFrom-Json
    if(!$result.passed){throw $result.error}
    Write-Host "PASS: $($result.rows.Count) rotating showcases with direct pause/resume and shared full-quality holotables."
}
