param(
    [Parameter(Mandatory=$true)][string]$ExecutablePath,
    [ValidateRange(10,45)][int]$Seconds=30,
    [ValidateSet('Pella/Monitor','Pella/Holotable','Pella/Minimized','Rocinante/Monitor','Rocinante/Holotable','Rocinante/Minimized')]
    [string[]]$Cases=@('Pella/Monitor','Pella/Holotable','Pella/Minimized','Rocinante/Monitor','Rocinante/Holotable','Rocinante/Minimized')
)
$ErrorActionPreference='Stop'
$candidate=[IO.Path]::GetFullPath($ExecutablePath)
$repoPath=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
Set-Location -LiteralPath $repoPath
if([IO.Path]::GetFileName($candidate) -ne 'SensorPanel.exe' -or !(Test-Path -LiteralPath $candidate)){throw 'Select the exact existing SensorPanel apphost.'}
$running=@(Get-CimInstance Win32_Process -Filter "Name='SensorPanel.exe'")
if($running.Count){$running | Select-Object ProcessId,ExecutablePath,CommandLine | Format-List;throw 'An existing monitor is running; nothing closed or replaced.'}
Write-Host "LIVE whole-tree measurement: $candidate. No rebuild, installer or quality changes."
$started=[datetimeoffset]::UtcNow
Start-Process -FilePath $candidate -WindowStyle Normal -ArgumentList @('--windowed','--theme','Pella','--runtime-check','--check-holotable-live','--measure-fleet-rendering','--measure-fleet-pair') | Out-Null
function Wait-Phase([string]$state,[string]$ship){
    $deadline=[datetimeoffset]::UtcNow.AddMinutes(4)
    while([datetimeoffset]::UtcNow -lt $deadline){
        $phase=$null
        try{$phase=Get-Content .\artifacts\fleet-pair-phase.json -Raw | ConvertFrom-Json}catch{}
        if($phase -and $phase.executablePath -eq $candidate -and ([datetimeoffset]$phase.capturedAt) -gt $started){
            if($phase.state -eq 'failed'){throw $phase.error}
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
foreach($ship in @('Pella','Rocinante')){
    $phase=Wait-Phase 'monitor-ready' $ship
    Write-Host "LIVE $ship at 1920x1080: monitoring, rotating holotable, then minimized."
    if($Cases -contains "$ship/Monitor"){& .\tools\Measure-MonitorResources.ps1 -Ship $ship -View Monitor -State Visible -Seconds $Seconds -ExpectedExecutable $candidate}
    Command 'holotable' $phase
    $phase=Wait-Phase 'holotable-ready' $ship
    if($Cases -contains "$ship/Holotable"){& .\tools\Measure-MonitorResources.ps1 -Ship $ship -View Holotable -State Visible -Seconds $Seconds -ExpectedExecutable $candidate -RequireHolotable}
    Command 'minimize' $phase
    $phase=Wait-Phase 'minimized-ready' $ship
    try{if($Cases -contains "$ship/Minimized"){& .\tools\Measure-MonitorResources.ps1 -Ship $ship -View Holotable -State Minimized -Seconds $Seconds -ExpectedExecutable $candidate -RequireHolotable}}
    finally{Command 'restore' $phase}
    $phase=Wait-Phase 'ship-complete' $ship
    Command 'next' $phase
}
$null=Wait-Phase 'complete' ''
Write-Host 'PASS: selected LIVE ship/view cases measured; the review app closed itself.'
