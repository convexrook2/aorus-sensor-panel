param(
    [Parameter(Mandatory=$true)][string]$ExecutablePath,
    [ValidateRange(10,45)][int]$Seconds=30,
    [switch]$IsolateMap,
    [switch]$CompareRotation,
    [ValidateSet('Efficient','FullFidelity')][string]$GraphicsMode='Efficient',
    [ValidateSet('Pella/Monitor','Pella/HolotableIdle','Pella/HolotableRotating','Pella/Minimized','Rocinante/Monitor','Rocinante/HolotableIdle','Rocinante/HolotableRotating','Rocinante/Minimized')]
    [string[]]$Cases=@('Pella/Monitor','Pella/HolotableIdle','Pella/HolotableRotating','Pella/Minimized','Rocinante/Monitor','Rocinante/HolotableIdle','Rocinante/HolotableRotating','Rocinante/Minimized')
)
$ErrorActionPreference='Stop'
if($CompareRotation -and !$IsolateMap){throw 'CompareRotation requires IsolateMap.'}
$candidate=[IO.Path]::GetFullPath($ExecutablePath)
$repoPath=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
Set-Location -LiteralPath $repoPath
if([IO.Path]::GetFileName($candidate) -ne 'SensorPanel.exe' -or !(Test-Path -LiteralPath $candidate)){throw 'Select the exact existing SensorPanel apphost.'}
$running=@(Get-CimInstance Win32_Process -Filter "Name='SensorPanel.exe'")
if($running.Count){$running | Select-Object ProcessId,ExecutablePath,CommandLine | Format-List;throw 'An existing monitor is running; nothing closed or replaced.'}
Write-Host "LIVE whole-tree measurement: $candidate. No rebuild, installer or quality changes."
$started=[datetimeoffset]::UtcNow
$reviewArgs=@('--windowed','--theme','Pella','--runtime-check','--check-holotable-live','--measure-fleet-rendering','--measure-fleet-pair')
if($GraphicsMode -eq 'FullFidelity'){$reviewArgs+='--measure-full-fidelity'}
if($IsolateMap){$reviewArgs+='--measure-map-only'}
if($CompareRotation){$reviewArgs+='--measure-rotation-pause'}
$launcher=Start-Process -FilePath $candidate -WindowStyle Normal -ArgumentList $reviewArgs -PassThru
function Wait-Phase([string]$state,[string]$ship){
    $deadline=[datetimeoffset]::UtcNow.AddMinutes(4)
    while([datetimeoffset]::UtcNow -lt $deadline){
        if($launcher.HasExited -and $launcher.ExitCode -ne 0){throw "Live review did not start (exit $($launcher.ExitCode)); no measurement recorded."}
        $phase=$null
        try{$phase=Get-Content .\artifacts\fleet-pair-phase.json -Raw | ConvertFrom-Json}catch{}
        if($phase -and $phase.executablePath -eq $candidate -and ([datetimeoffset]$phase.capturedAt) -gt $started){
            if($phase.state -in @('failed','interrupted')){throw $phase.error}
            if($phase.state -eq $state -and (!$ship -or $phase.ship -eq $ship)){return $phase}
        }
        $live=$null;try{$live=Get-Content .\artifacts\live-runtime-check.json -Raw | ConvertFrom-Json}catch{}
        if($live.passed -eq $false -and (Get-Item .\artifacts\live-runtime-check.json).LastWriteTimeUtc -gt $started.UtcDateTime){throw $live.error}
        Start-Sleep -Milliseconds 300
    }
    throw "Timed out waiting for $ship / $state."
}
function Command([string]$state,$phase){
    @{state=$state;ship=$phase.ship;processId=$phase.processId;session=$phase.session} | ConvertTo-Json | Set-Content .\artifacts\fleet-pair-command.json.tmp -Encoding utf8
    Move-Item -LiteralPath .\artifacts\fleet-pair-command.json.tmp -Destination .\artifacts\fleet-pair-command.json -Force
}
function Measure-Case([string]$ship,[string]$view,[string]$state='Visible'){
    for($attempt=1;$attempt -le 2;$attempt++){
        try{& .\tools\Measure-MonitorResources.ps1 -Ship $ship -View $view -State $state -Seconds $Seconds -ExpectedExecutable $candidate -RequireHolotable:($view -notlike 'Monitor*') -RequireUncovered;return}
        catch{
            if($attempt -eq 2 -or $_.Exception.Message -notmatch 'counter missing|process exited|Process identity changed|New descendants'){throw}
            Write-Host "Retry once with a fresh process-tree inventory: $($_.Exception.Message)"
        }
    }
}
foreach($ship in @('Pella','Rocinante')){
    $phase=Wait-Phase 'monitor-ready' $ship
    if($IsolateMap){Write-Host "LIVE $ship at 1920x1080: rotating showcase with solar motion on, then solar paused."}
    else{Write-Host "LIVE $ship at 1920x1080: rotating showcase, idle holotable, rotating holotable, then minimized."}
    if($IsolateMap -or $Cases -contains "$ship/Monitor"){Measure-Case $ship 'Monitor'}
    if($IsolateMap){
        Command 'pause-map' $phase;$phase=Wait-Phase 'map-paused-ready' $ship
        Measure-Case $ship 'MonitorMapPaused'
        if($CompareRotation){
            Command 'pause-rotation' $phase;$phase=Wait-Phase 'all-paused-ready' $ship
            Measure-Case $ship 'MonitorAllPaused'
            Command 'animate-map' $phase;$phase=Wait-Phase 'showcase-paused-ready' $ship
            Measure-Case $ship 'MonitorRotationPaused'
        }
        Command 'next' $phase;continue
    }
    Command 'holotable' $phase
    $phase=Wait-Phase 'holotable-ready' $ship
    if($Cases -contains "$ship/HolotableIdle"){Measure-Case $ship 'HolotableIdle'}
    Command 'rotate' $phase
    $phase=Wait-Phase 'rotating-ready' $ship
    if($Cases -contains "$ship/HolotableRotating"){Measure-Case $ship 'HolotableRotating'}
    Command 'minimize' $phase
    $phase=Wait-Phase 'minimized-ready' $ship
    try{if($Cases -contains "$ship/Minimized"){Measure-Case $ship 'Holotable' 'Minimized'}}
    finally{Command 'restore' $phase}
    $phase=Wait-Phase 'ship-complete' $ship
    Command 'next' $phase
}
$null=Wait-Phase 'complete' ''
Write-Host 'PASS: selected LIVE ship/view cases measured; the review app closed itself.'
