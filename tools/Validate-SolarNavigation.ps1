param([switch]$BuildOnly)
$ErrorActionPreference='Stop'
$taskRoot=Split-Path $PSScriptRoot -Parent
Set-Location -LiteralPath $taskRoot
New-Item -ItemType Directory -Path 'artifacts/solar-navigation-validation' -Force | Out-Null
Start-Transcript -LiteralPath 'artifacts/solar-navigation-validation/validation.txt' -Force
try {
    & './tools/Build-SolarAssets.ps1'
    dotnet build './SensorPanel/SensorPanel.csproj' -c Release | Out-Host
    if($LASTEXITCODE -ne 0){throw 'Panel build failed.'}
    & './SensorCollector/bin/Release/net10.0-windows/SensorCollector.exe' --self-test | Out-Host
    if($LASTEXITCODE -ne 0){throw 'Existing collector checks failed.'}
    $taskExecutable=Join-Path $taskRoot 'SensorPanel/bin/Release/net10.0-windows/SensorPanel.exe'
    $taskMapping=Start-Process -FilePath $taskExecutable -ArgumentList '--check-mapping' -WindowStyle Normal -Wait -PassThru
    if($taskMapping.ExitCode -ne 0){throw 'Existing mapping/resource checks failed.'}
    if(-not $BuildOnly) {
        $taskSnapshot = & './tools/New-PreviewSnapshot.ps1'
        foreach($taskSize in @('1920x480','1280x720','480x800')) {
            Write-Host ('VISIBLE NATIVE DIAGRAM CHECK: '+$taskSize+' / saved readings / no live collector / no installation')
            $taskStarted=Get-Date
            $taskPreview=Start-Process -FilePath $taskExecutable -ArgumentList @('--preview',('"'+$taskSnapshot+'"'),'--sensor-preview','--preview-size',$taskSize,'--preview-theme','Expanse','--preview-check-navigation') -WindowStyle Normal -Wait -PassThru
            $taskResultPath=Join-Path $taskRoot ('artifacts/solar-navigation-validation/'+$taskSize+'/result.json')
            if($taskPreview.ExitCode -ne 0 -or -not (Test-Path -LiteralPath $taskResultPath) -or (Get-Item -LiteralPath $taskResultPath).LastWriteTime -lt $taskStarted){throw ('Native diagram check failed: '+$taskSize)}
            $taskResult=Get-Content -LiteralPath $taskResultPath -Raw | ConvertFrom-Json
            if(-not $taskResult.Passed){throw ('Native diagram result failed: '+$taskSize)}
            Write-Host ('PASS: original native diagram, local detailed assets, focus, label placement, paused/hidden rendering and sensor preservation at '+$taskSize)
        }
    }
    Write-Host 'PASS: original Sol diagram validation completed.'
} finally { Stop-Transcript }
