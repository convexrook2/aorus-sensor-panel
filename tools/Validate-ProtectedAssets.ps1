$ErrorActionPreference='Stop'
$repoPath=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
Set-Location -LiteralPath $repoPath
$reviewRoot=Join-Path $repoPath ('artifacts\rl-publish-'+(Get-Date -Format 'yyyyMMdd-HHmmss-fff'))
New-Item -ItemType Directory -Path $reviewRoot -Force | Out-Null
Start-Transcript -LiteralPath (Join-Path $reviewRoot 'validation.txt')
try{
    if(@(Get-CimInstance Win32_Process -Filter "Name='SensorPanel.exe'").Count){throw 'Finish the live measurement before building.'}
    Write-Host 'Builds, authenticated asset tests, protected publish and visible saved-data rendering. No installer or installation.'
    foreach($project in @('SensorPanel','FleetStudio.Desktop')){
        dotnet build ".\$project\$project.csproj" -c Release --tl:off 2>&1 | Tee-Object -FilePath (Join-Path $reviewRoot ($project+'-build.txt')) | Out-Host
        if($LASTEXITCODE -ne 0){throw "Build failed: $project"}
    }
    $tool=Join-Path $repoPath 'tools\RlPack\bin\Release\net10.0\RlPack.dll'
    dotnet $tool self-test --output (Join-Path $reviewRoot 'archive-checks')
    if($LASTEXITCODE -ne 0){throw 'RLAR rejection/round-trip tests failed.'}
    python .\tools\Sync-FleetViewer.py --check
    if($LASTEXITCODE -ne 0){throw 'Accepted fleet source bundle changed.'}
    $published=@{}
    foreach($project in @('SensorPanel','FleetStudio.Desktop')){
        $folder=Join-Path $reviewRoot $project
        dotnet publish ".\$project\$project.csproj" -c Release --tl:off --self-contained false -p:DebugType=None -p:DebugSymbols=false -o $folder 2>&1 | Tee-Object -FilePath (Join-Path $reviewRoot ($project+'-publish.txt')) | Out-Host
        if($LASTEXITCODE -ne 0){throw "Protected publish failed: $project"}
        $product=if($project -eq 'SensorPanel'){'X890.Sensor'}else{'X890.FleetStudio'}
        $source=if($project -eq 'SensorPanel'){Join-Path $repoPath 'Assets'}else{$repoPath}
        dotnet $tool verify --archive (Join-Path $folder 'assets.rl') --key (Join-Path $env:LOCALAPPDATA "LennonBuildKeys\$product-v2.key") --product $product --source $source
        if($LASTEXITCODE -ne 0){throw 'Protected archive/source parity failed.'}
        foreach($root in @('FleetViewer','SolarNavigation','FleetStudio','Models','Ships','AORUS')){
            if(Test-Path -LiteralPath (Join-Path $folder $root)){throw "Loose protected root leaked: $root"}
        }
        if(@(Get-ChildItem -LiteralPath $folder -Recurse -File | Where-Object Extension -in @('.key','.cs','.pdb')).Count){throw 'Build secrets/source/debug files leaked into publish.'}
        if(@(Get-ChildItem -LiteralPath $folder -File -Filter 'RlPack*').Count){throw 'Build-only asset tooling leaked into publish.'}
        $published[$project]=$folder
    }
    $mapping=Start-Process -FilePath (Join-Path $published.SensorPanel 'SensorPanel.exe') -ArgumentList '--check-mapping' -WindowStyle Normal -Wait -PassThru
    if($mapping.ExitCode -ne 0){throw 'Protected native resources/mapping failed.'}
    $started=[datetime]::UtcNow
    $preview=Start-Process -FilePath (Join-Path $published.SensorPanel 'SensorPanel.exe') -ArgumentList @('--preview',('"'+(Join-Path $repoPath 'tools\preview-empty.json')+'"'),'--windowed','--preview-theme','Pella','--check-protected-assets') -WindowStyle Normal -PassThru
    if(!$preview.WaitForExit(180000)){throw 'Protected native preview timed out; close only this review app.'}
    $native=Join-Path $repoPath 'artifacts\rl-protected-native-check\result.json'
    if(!(Test-Path -LiteralPath $native) -or (Get-Item -LiteralPath $native).LastWriteTimeUtc -lt $started -or !(Get-Content -LiteralPath $native -Raw|ConvertFrom-Json).passed){throw 'Protected native rendering failed; inspect its receipt.'}
    $studio=Start-Process -FilePath (Join-Path $published.'FleetStudio.Desktop' 'FleetStudio.Desktop.exe') -ArgumentList '--check-protected-assets' -WindowStyle Normal -PassThru
    if(!$studio.WaitForExit(180000)){throw 'Protected Studio preview timed out; close only this review app.'}
    $studioReceipt=Join-Path $published.'FleetStudio.Desktop' 'protected-check\result.json'
    if(!(Test-Path -LiteralPath $studioReceipt) -or !(Get-Content -LiteralPath $studioReceipt -Raw|ConvertFrom-Json).passed){throw 'Protected Studio rendering failed; inspect its receipt.'}
    [pscustomobject]@{Passed=$true;CreatedAt=[datetimeoffset]::Now;Published=$published;NativeSavedData=$native;StudioNative=$studioReceipt;Installed=$false;InstallerBuilt=$false} | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $reviewRoot 'result.json')
    Write-Host "PASS: protected assets match exact source bytes; native review apps closed themselves. Evidence: $reviewRoot"
}finally{Stop-Transcript}
