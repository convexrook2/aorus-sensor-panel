param([ValidateSet('Benchmark','Upgrade','UpgradeNoLaunch','Hold')][string]$Stage)
$ErrorActionPreference='Stop'
$projectRoot=Split-Path $PSScriptRoot -Parent
Set-Location -LiteralPath $projectRoot
$production=Join-Path $env:ProgramFiles 'AORUS Sensor Panel FAN MADE\SensorPanel.exe'
$development=Join-Path $projectRoot 'SensorPanel\bin\Release\net10.0-windows\SensorPanel.exe'
$package=Join-Path $projectRoot 'artifacts\package-win-x64\SensorPanel.exe'
$packageManifest=Get-Content -LiteralPath (Join-Path $projectRoot 'artifacts\installer\package-manifest.json') -Raw | ConvertFrom-Json
if($packageManifest.PackageRoot) {$package=Join-Path $packageManifest.PackageRoot 'SensorPanel.exe'}
$packageVersion=$packageManifest.Version
function Close-Panel([string]$Path) {
    foreach($record in @(Get-CimInstance Win32_Process -Filter "Name='SensorPanel.exe'" | Where-Object ExecutablePath -eq $Path)) {
        $process=Get-Process -Id $record.ProcessId
        if(-not $process.CloseMainWindow() -or -not $process.WaitForExit(9000)) {throw ('Close this panel before continuing: '+$Path)}
    }
}
Start-Transcript -Path (Join-Path $projectRoot ('artifacts\polling-'+$Stage.ToLowerInvariant()+'.txt')) -Force
try {
    if($Stage -eq 'Hold') {
        Close-Panel $package; Close-Panel $development; Close-Panel $production
        Write-Host 'Workspace hold: all panel copies closed. No launch or installer update will run.'
    } elseif($Stage -eq 'Benchmark') {
        Close-Panel $development; Close-Panel $production
        Write-Host 'Visible sensor-panel benchmark: 1000, 500 and 5000 ms, 20 seconds each after warm-up. Artwork remains at 30 ms.'
        try {
            $run=Start-Process $package -ArgumentList '--benchmark-polling' -WindowStyle Normal -Wait -PassThru
            if($run.ExitCode -ne 0) {throw 'Visible polling benchmark failed.'}
            $report=Get-Content -LiteralPath '.\artifacts\polling-benchmark.json' -Raw | ConvertFrom-Json
            if($report.Rows.Count -ne 3) {throw 'Incomplete polling benchmark.'}
            $report.Rows | Select-Object IntervalMs,ActualCollectionsPerSecond,MeanCollectionMs,CombinedCPUPercent,WorkingSetMiB | Format-Table -AutoSize
        } finally { if((Test-Path -LiteralPath $production) -and -not (Test-Path -LiteralPath '.\artifacts\polling-workspace-hold.json')) {$null=Start-Process $production -WindowStyle Normal} }
    } else {
        if($Stage -ne 'UpgradeNoLaunch' -and (Test-Path -LiteralPath '.\artifacts\polling-workspace-hold.json')){throw 'Workspace hold prevents application launch until the owner resumes it.'}
        if(-not (Test-Path -LiteralPath $production)) {throw 'The known production installation is absent; no other install location is changed.'}
        $scheduler=New-Object -ComObject Schedule.Service; $scheduler.Connect()
        $sid=[Security.Principal.WindowsIdentity]::GetCurrent().User.Value
        $name='AORUS-FAN-MADE-1B7C308F-'+$sid
        $ownedTasks=@($scheduler.GetFolder('\').GetTasks(0) | Where-Object Name -eq $name)
        $startup=$ownedTasks.Count -eq 1 -and $ownedTasks[0].Enabled
        if($startup -and $ownedTasks[0].Definition.Actions.Item(1).Path -ne $production) {throw 'Startup task belongs to another package path; stop update.'}
        Close-Panel $production
        $installer=Join-Path $projectRoot 'artifacts\installer\AORUS-Sensor-Panel-FAN-MADE-Setup.exe'
        $tasks=if($startup){'desktopicon,startup'}else{'desktopicon'}
        Write-Host ('Updating the existing installed panel to '+$packageVersion+'; preserving the startup option.')
        $arguments='/SILENT /SP- /NORESTART /ALLUSERS /DIR="'+(Split-Path $production)+'" /TASKS="'+$tasks+'" /LOG="'+(Join-Path $projectRoot 'artifacts\installer\polling-upgrade.log')+'"'
        $install=Start-Process $installer -ArgumentList $arguments -WindowStyle Normal -Wait -PassThru
        if($install.ExitCode -ne 0) {throw 'Polling upgrade installer failed.'}
        if((Get-Item -LiteralPath $production).VersionInfo.FileVersion -ne ($packageVersion+'.0')) {throw 'Installed version mismatch.'}
        if($packageManifest.SelfContained -eq $false -and (Test-Path -LiteralPath (Join-Path (Split-Path $production) 'coreclr.dll'))) {throw 'Legacy private runtime was not removed.'}
        $check=Start-Process $production -ArgumentList '--check-mapping' -WindowStyle Normal -Wait -PassThru
        if($check.ExitCode -ne 0) {throw 'Installed polling/source checks failed.'}
        if($Stage -ne 'UpgradeNoLaunch') {
            if($startup) {$null=$scheduler.GetFolder('\').GetTask($name).Run($null)}else{$null=Start-Process $production -WindowStyle Normal}
        }
        [pscustomobject]@{Version=$packageVersion;SelfContained=$packageManifest.SelfContained;CheckedAt=(Get-Date).ToString('o');Range='500-5000 ms';DefaultMs=1000;StartupPreserved=$startup;Installed=$production;PollingChecks=$true} | ConvertTo-Json | Set-Content -LiteralPath '.\artifacts\installer\polling-upgrade.json' -Encoding UTF8
        Write-Host ('PASS: installed '+$packageVersion+' and polling checks. Launch skipped: ' + ($Stage -eq 'UpgradeNoLaunch'))
    }
}catch{Write-Host ($_ | Out-String) -ForegroundColor Red;throw}
finally{Stop-Transcript}
