$ErrorActionPreference='Stop'
$projectRoot=Split-Path $PSScriptRoot -Parent
Set-Location -LiteralPath $projectRoot
$panel=Join-Path $projectRoot 'SensorPanel\bin\Package\net10.0-windows\SensorPanel.exe'
$fixture=Join-Path $env:LOCALAPPDATA 'X890Sensor\artifacts\panel-live.json'
if(-not (Test-Path -LiteralPath $fixture)){$fixture=Join-Path $projectRoot 'artifacts\panel-live.json'}
$output=Join-Path $projectRoot ('artifacts\layout-previews-'+(Get-Date -Format 'yyyyMMdd-HHmmss'))
New-Item -ItemType Directory -Path $output -Force | Out-Null
foreach($size in @('1920x1080','1024x600','800x600','1920x480','1920x462','480x1920','480x800')) {
    $image=Join-Path $output ($size+'.png')
    $arguments='--preview "'+$fixture+'" --sensor-preview --preview-size '+$size+' --capture "'+$image+'"'
    $preview=Start-Process $panel -ArgumentList $arguments -WindowStyle Normal -PassThru
    try {
        $deadline=[DateTime]::UtcNow.AddSeconds(12)
        while(-not (Test-Path -LiteralPath $image) -and [DateTime]::UtcNow -lt $deadline -and -not $preview.HasExited) {Start-Sleep -Milliseconds 200;$preview.Refresh()}
        if(-not (Test-Path -LiteralPath $image)) {throw ('Preview failed for '+$size)}
        Write-Host ('Rendered visible secondary-display preview: '+$size+' -> '+$image)
    } finally {
        if(-not $preview.HasExited) {[void]$preview.CloseMainWindow();if(-not $preview.WaitForExit(3000)){throw 'Close the preview before continuing.'}}
    }
}
Write-Host 'All previews closed. Inspect PNGs for layout/label clipping; readings are saved snapshots.'
