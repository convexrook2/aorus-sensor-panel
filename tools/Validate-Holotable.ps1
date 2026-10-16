param([switch]$Package,[string]$SnapshotPath)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
Set-Location -LiteralPath $projectRoot
New-Item -ItemType Directory -Path artifacts -Force | Out-Null
if(-not $SnapshotPath){$SnapshotPath=& (Join-Path $PSScriptRoot 'New-PreviewSnapshot.ps1')}
$SnapshotPath=(Resolve-Path -LiteralPath $SnapshotPath).Path
Start-Transcript -Path artifacts\holotable-validation.txt -Force
try {
    Write-Host 'Build, mapping/resource checks, then visible saved-data previews on the secondary sensor display. No installation or live collector.'
    dotnet build .\SensorPanel\SensorPanel.csproj -c Release
    if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
    $executable = Join-Path $projectRoot 'SensorPanel\bin\Release\net10.0-windows\SensorPanel.exe'
    $check = Start-Process -FilePath $executable -ArgumentList '--check-mapping' -Wait -PassThru
    if ($check.ExitCode -ne 0) { throw 'Mapping/resource checks failed.' }
    foreach ($size in @('1280x720', '1920x480', '480x800')) {
        $report = Join-Path $projectRoot ('artifacts\theme-previews\' + $size + '\navigation-check.json')
        $before = Get-Date
        $preview = Start-Process -FilePath $executable -ArgumentList @('--preview', ('"' + $SnapshotPath + '"'), '--sensor-preview', '--preview-size', $size, '--preview-theme', 'Rocinante', '--preview-check-ships') -PassThru
        if (-not $preview.WaitForExit(180000)) { $preview.Kill(); throw "Visible preview timed out: $size" }
        if ($preview.ExitCode -ne 0 -or -not (Test-Path -LiteralPath $report) -or (Get-Item -LiteralPath $report).LastWriteTime -lt $before) { throw "Preview failed: $size. See artifacts\theme-check-error.txt." }
        $result = Get-Content -LiteralPath $report -Raw | ConvertFrom-Json
        if (-not $result.Passed -or -not $result.ReadoutEffectsIndependent -or -not $result.ShipRotationBoundsChecked -or -not $result.ShipInLeftPanel -or -not $result.SolarSystemChecked -or -not $result.ShipNavigationFocusChecked -or -not $result.ShipSelectorChecked) { throw "Projection checks failed: $size" }
        Write-Host "PASS: $size solar navigation, ship selector/focus, projected readings, source bindings, missing values, independent effects, left ship fit, settings bounds."
    }
    if ($Package) { & .\tools\Build-Installer.ps1 }
    Write-Host 'Validation completed. Native manual drag/pinch and live hardware performance are separate checks.'
}
finally { Stop-Transcript }
