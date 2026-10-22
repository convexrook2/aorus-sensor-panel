param(
    [ValidateSet('Pella','Rocinante')][string]$Ship = 'Pella',
    [ValidateSet('Monitor','MonitorMapPaused','MonitorRotationPaused','MonitorAllPaused','Holotable','HolotableIdle','HolotableRotating')][string]$View = 'Monitor',
    [ValidateSet('Visible','Minimized','Restored','ReleaseFullscreen','ReleaseMinimized')][string]$State = 'Visible',
    [ValidateRange(10,45)][int]$Seconds = 30,
    [ValidateRange(0,30)][int]$SettleSeconds = 8,
    [string]$ExpectedExecutable,
    [switch]$RequireHolotable,
    [switch]$RequireForeground,
    [switch]$RequireUncovered
)
$ErrorActionPreference='Stop'
if($RequireHolotable -and !$PSBoundParameters.ContainsKey('View')){$View='Holotable'}
$repoPath=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
. (Join-Path $PSScriptRoot 'Read-MonitorWindowState.ps1')
$outputFolder=Join-Path $repoPath 'artifacts\performance'
New-Item -ItemType Directory -Path $outputFolder -Force | Out-Null
$runId=[datetimeoffset]::Now.ToString('yyyyMMdd-HHmmss-fff')+"-$PID"
$receipt=Get-Content (Join-Path $repoPath 'artifacts\live-runtime-check.json') -Raw | ConvertFrom-Json
$all=@(Get-CimInstance Win32_Process)
$root=@($all | Where-Object { $_.ProcessId -eq $receipt.processId -and $_.Name -eq 'SensorPanel.exe' })
$binaryRoot=Join-Path $repoPath 'SensorPanel\bin\'
$showcaseCandidate=Join-Path $repoPath 'artifacts\pella-showcase-review\SensorPanel.exe'
$renderCandidate=Join-Path $repoPath 'artifacts\pella-render-review\SensorPanel.exe'
$renderCandidateV2=Join-Path $repoPath 'artifacts\pella-render-review-v2\SensorPanel.exe'
$smoothCandidate=Join-Path $repoPath 'artifacts\pella-smooth-review\SensorPanel.exe'
$allowedPath=$receipt.executablePath.StartsWith($binaryRoot,[StringComparison]::OrdinalIgnoreCase) -or $receipt.executablePath -in @($showcaseCandidate,$renderCandidate,$renderCandidateV2,$smoothCandidate)
if($ExpectedExecutable){$allowedPath=$receipt.executablePath -eq [IO.Path]::GetFullPath($ExpectedExecutable)}
if ($receipt.preview -ne $false -or !$receipt.passed -or $root.Count -ne 1 -or
    !$allowedPath -or ($root[0].ExecutablePath -and $root[0].ExecutablePath -ne $receipt.executablePath) -or
    [math]::Abs((([datetime]$root[0].CreationDate).ToUniversalTime()-([datetime]$receipt.startedAt).ToUniversalTime()).TotalSeconds) -gt 15) {
    throw 'Live process identity does not match this checkout and runtime receipt.'
}
if($State.StartsWith('Release') -and $receipt.executablePath -notlike '*\bin\Release\*') { throw 'The selected sample requires the actual Release build.' }
$ids=[Collections.Generic.HashSet[int]]::new()
[void]$ids.Add([int]$root[0].ProcessId)
$identityById=@{}
foreach($item in $all){$identityById[[int]$item.ProcessId]=$item}
do {
    $added=$false
    foreach($item in $all) {
        if($ids.Contains([int]$item.ParentProcessId) -and $item.CreationDate -ge $identityById[[int]$item.ParentProcessId].CreationDate -and $ids.Add([int]$item.ProcessId)) { $added=$true }
    }
} while($added)
$tree=@($all | Where-Object { $ids.Contains([int]$_.ProcessId) })
$tree | Select-Object ProcessId,ParentProcessId,Name,ExecutablePath | Format-Table -AutoSize
if($View -notlike 'Monitor*' -and @($tree | Where-Object Name -eq 'msedgewebview2.exe').Count -eq 0){throw 'The full model WebView2 process tree is missing.'}
Write-Host "State: $State; root $($root[0].ProcessId); $($tree.Count) processes; $Seconds samples, at least one second apart, after $SettleSeconds seconds settling."
Write-Host 'CPU is percent of the whole machine. GPU is the busiest hardware engine across this process tree.'
Write-Host 'RAM/graphics memory sums can include shared allocations more than once. No app setting or model is changed.'
if($SettleSeconds) { Start-Sleep -Seconds $SettleSeconds }
function Check-SelectedShip {
    # The app rewrites this small receipt in place. Retry an empty/partial read,
    # but never accept a complete receipt identifying another ship/process.
    $displayState=$null
    for($attempt=0;$attempt -lt 4;$attempt++) {
        try { $displayState=Get-Content (Join-Path $repoPath 'artifacts\panel-display-state.json') -Raw | ConvertFrom-Json }
        catch { if($attempt -eq 3) { throw }; Start-Sleep -Milliseconds 50; continue }
        if($null -ne $displayState -and $null -ne $displayState.processId) { break }
        Start-Sleep -Milliseconds 50
    }
    if($displayState.processId -ne $receipt.processId -or $displayState.preview -ne $false -or $displayState.featuredShip -ne $Ship) {
        throw "The live receipt identifies process $($displayState.processId), ship '$($displayState.featuredShip)', preview '$($displayState.preview)'; expected live $Ship in process $($receipt.processId)."
    }
    $expectActive=$State -notin @('Minimized','ReleaseMinimized')
    $nativeWindow=Read-MonitorWindowState -MonitorProcessId $receipt.processId
    if([bool]$nativeWindow.PresentationActive -ne $expectActive){throw 'The native window does not match the requested visible/minimized state.'}
    if($RequireForeground -and $expectActive -and !$nativeWindow.Foreground){throw 'The measurement panel lost foreground; a covered window is not accepted as a visible sample.'}
    if($RequireUncovered -and $expectActive -and ($nativeWindow.CoveragePoints -ne 45 -or $nativeWindow.CoveredPoints -ne 0)){
        $covering=@($nativeWindow.CoveringProcessIds | ForEach-Object {Get-Process -Id $_ -ErrorAction SilentlyContinue} | Select-Object Id,ProcessName,MainWindowTitle)
        throw ('The measurement panel is covered at one or more screen-sample points: '+($covering | ConvertTo-Json -Compress))
    }
    if($RequireHolotable -and !$displayState.holotableOpen){throw 'The holotable is not selected.'}
    if($View -eq 'HolotableIdle' -and $displayState.holotableMotion){throw 'The idle holotable is rotating.'}
    if($View -eq 'HolotableRotating' -and $expectActive -and !$displayState.holotableMotion){throw 'The rotating holotable is stopped.'}
    if($View -like 'Monitor*' -and $displayState.holotableOpen){throw 'The normal monitoring view is covered by the holotable.'}
    if($View -like 'Monitor*' -and !$displayState.shipRendererLoaded){throw 'The showcase renderer is missing.'}
    if($View -in @('MonitorMapPaused','MonitorAllPaused') -and $displayState.navigationMotion){throw 'The isolated solar map is still animating.'}
    if($View -in @('Monitor','MonitorRotationPaused') -and !$displayState.navigationMotion){throw 'The solar map should be animating for this sample.'}
    if($View -in @('MonitorAllPaused','MonitorRotationPaused') -and ($displayState.showcaseRotation -or $displayState.rotatingShipInLeftPanel)){throw 'The isolated ship rotation is still enabled.'}
    if($View -in @('Monitor','MonitorMapPaused') -and !$displayState.rotatingShipInLeftPanel){throw 'The showcase should be rotating for this sample.'}
    return $displayState
}
$displayBefore=Check-SelectedShip
$nativeBefore=Read-MonitorWindowState -MonitorProcessId $receipt.processId
if($State -eq 'ReleaseFullscreen' -and !$displayBefore.fitsTarget) { throw 'Place the live panel on its intended display and choose Fit to display before the fullscreen sample.' }
function Read-Processes {
    $result=@{}
    foreach($processId in $ids) {
        $p=Get-Process -Id $processId -ErrorAction SilentlyContinue
        if($p) {
            $result[$processId]=[pscustomobject]@{
                Id=$processId; Name=$p.ProcessName; Cpu=$p.TotalProcessorTime.TotalSeconds
                Working=$p.WorkingSet64; Private=$p.PrivateMemorySize64
            }
        }
    }
    return $result
}
$counterPaths=@('\GPU Engine(*)\Utilization Percentage','\GPU Process Memory(*)\Dedicated Usage','\GPU Process Memory(*)\Shared Usage',
    '\GPU Adapter Memory(*)\Dedicated Usage','\GPU Adapter Memory(*)\Shared Usage')
$privateWorkingFilter=($ids | ForEach-Object {"IDProcess=$_"}) -join ' OR '
$rows=[Collections.Generic.List[object]]::new()
$details=[Collections.Generic.List[object]]::new()
$engineRows=[Collections.Generic.List[object]]::new()
$counterIssues=[Collections.Generic.HashSet[string]]::new()
$previous=$null; $previousAt=$null
Get-Counter -Counter $counterPaths -SampleInterval 1 -MaxSamples ($Seconds+1) -ErrorAction Continue -ErrorVariable counterErrors | ForEach-Object {
    $set=$_; $now=[datetimeoffset]::Now; $null=Check-SelectedShip; $current=Read-Processes
    if($null -ne $previous) {
        $dt=($now-$previousAt).TotalSeconds
        $cpu=0.0; $working=0L; $private=0L
        # Query each row by its numeric PID. PDH omits some private working-set
        # instances on this host; do not infer missing bytes from process names.
        $privateResidentById=@{}
        foreach($p in Get-CimInstance Win32_PerfFormattedData_PerfProc_Process -Filter $privateWorkingFilter){
            if($null -ne $p.WorkingSetPrivate){$privateResidentById[[int]$p.IDProcess]=[double]$p.WorkingSetPrivate}
        }
        $privateResident=0.0
        foreach($processId in $current.Keys) {
            $p=$current[$processId]
            if(!$previous.ContainsKey($processId)) { throw "Process tree changed while sampling: $processId" }
            $partCpu=100*($p.Cpu-$previous[$processId].Cpu)/$dt/[Environment]::ProcessorCount
            $cpu+=$partCpu; $working+=$p.Working; $private+=$p.Private
            if(!$privateResidentById.ContainsKey([int]$processId)){throw "Private working set counter missing for process $processId"}
            $privateResident+=$privateResidentById[[int]$processId]
            $details.Add([pscustomobject]@{ Time=$now.ToString('o'); Id=$processId; Name=$p.Name; CpuPercent=$partCpu; WorkingMiB=$p.Working/1MB; PrivateMiB=$p.Private/1MB; PrivateResidentMiB=$privateResidentById[[int]$processId]/1MB })
        }
        if($current.Count -ne $previous.Count) { throw 'A monitored process exited while sampling; repeat for a stable tree.' }
        $engines=@{}; $dedicated=0.0; $shared=0.0; $validEngines=0; $validMemory=0; $boardDedicated=0.0; $boardShared=0.0
        foreach($counter in $set.CounterSamples) {
            if($counter.Status -notin @(0,1)) {
                if($counter.InstanceName -match '^pid_(\d+)_' -and $ids.Contains([int]$Matches[1])){throw "Invalid GPU counter for tracked PID: $($counter.Path)"}
                [void]$counterIssues.Add("$($counter.Path): status $($counter.Status)"); continue
            }
            if($counter.Path -like '*\GPU Engine(*)\*') { $validEngines++ }
            if($counter.Path -like '*\GPU Process Memory(*)\*') { $validMemory++ }
            if($counter.Path -like '*\GPU Adapter Memory(*)\Dedicated Usage'){$boardDedicated+=$counter.CookedValue}
            if($counter.Path -like '*\GPU Adapter Memory(*)\Shared Usage'){$boardShared+=$counter.CookedValue}
            if($counter.InstanceName -notmatch '^pid_(\d+)_(.+)$' -or !$ids.Contains([int]$Matches[1])) { continue }
            $engine=$Matches[2]
            if($counter.Path -like '*\GPU Engine(*)\*') {
                $engines[$engine]+=[math]::Max(0,[double]$counter.CookedValue)
            } elseif($counter.Path -like '*\Dedicated Usage') { $dedicated+=$counter.CookedValue
            } elseif($counter.Path -like '*\Shared Usage') { $shared+=$counter.CookedValue }
        }
        if($validEngines -eq 0) { throw 'GPU engine counters unavailable; do not report zero GPU usage.' }
        if($validMemory -eq 0) { throw 'GPU process memory counters unavailable.' }
        $busiest=0.0; $busiestName='Idle'
        foreach($engine in $engines.Keys) {
            $value=$engines[$engine]
            if($value -gt $busiest) { $busiest=$value; $busiestName=$engine }
            $engineRows.Add([pscustomobject]@{ Time=$now.ToString('o'); Engine=$engine; Percent=$value })
        }
        $row=[pscustomobject]@{
            Time=$now.ToString('o'); State=$State; IntervalSeconds=$dt
            CpuPercent=$cpu; WorkingMiB=$working/1MB; PrivateMiB=$private/1MB
            PrivateResidentMiB=$privateResident/1MB
            GpuPercent=$busiest; GpuEngine=$busiestName
            GpuDedicatedMiB=$dedicated/1MB; GpuSharedMiB=$shared/1MB; Processes=$current.Count
            WholeAdapterDedicatedMiB=$boardDedicated/1MB; WholeAdapterSharedMiB=$boardShared/1MB
        }
        $rows.Add($row)
        '{0,2}/{1} CPU {2,6:N2}%  RAM {3,8:N1} MiB  GPU {4,6:N2}%' -f $rows.Count,$Seconds,$cpu,($working/1MB),$busiest | Write-Host
    }
    $previous=$current; $previousAt=$now
}
if($rows.Count -ne $Seconds) { throw "Expected $Seconds samples; collected $($rows.Count)." }
$after=@(Get-CimInstance Win32_Process)
$extra=@($after | Where-Object { $ids.Contains([int]$_.ParentProcessId) -and $_.CreationDate -ge $identityById[[int]$_.ParentProcessId].CreationDate -and !$ids.Contains([int]$_.ProcessId) })
if($extra.Count) { throw 'New descendants appeared during measurement; repeat with the complete tree.' }
foreach($item in $tree) {
    $same=@($after | Where-Object { $_.ProcessId -eq $item.ProcessId -and $_.CreationDate -eq $item.CreationDate })
    if($same.Count -ne 1) { throw "Process identity changed during measurement: $($item.ProcessId)" }
}
function Summary([string]$field) {
    $m=$rows | Measure-Object -Property $field -Average -Minimum -Maximum
    return [pscustomobject]@{ Average=[math]::Round($m.Average,3); Minimum=[math]::Round($m.Minimum,3); Maximum=[math]::Round($m.Maximum,3) }
}
$parts=@($details | Group-Object Id | ForEach-Object {
    [pscustomobject]@{ Id=[int]$_.Name; Name=$_.Group[0].Name
        CpuAverage=[math]::Round(($_.Group | Measure-Object CpuPercent -Average).Average,3)
        WorkingMiBAverage=[math]::Round(($_.Group | Measure-Object WorkingMiB -Average).Average,1)
        PrivateMiBAverage=[math]::Round(($_.Group | Measure-Object PrivateMiB -Average).Average,1)
        PrivateResidentMiBAverage=[math]::Round(($_.Group | Measure-Object PrivateResidentMiB -Average).Average,1) }
})
$result=[pscustomobject]@{
    State=$State; View=$View; RunId=$runId; RootPid=$root[0].ProcessId; Ship=$Ship; ExecutablePath=$receipt.executablePath
    AssemblySha256=(Get-FileHash -LiteralPath ([IO.Path]::ChangeExtension($receipt.executablePath,'.dll'))).Hash
    ProtectedArchiveSha256=$(if(Test-Path -LiteralPath (Join-Path ([IO.Path]::GetDirectoryName($receipt.executablePath)) 'assets.rl')){(Get-FileHash -LiteralPath (Join-Path ([IO.Path]::GetDirectoryName($receipt.executablePath)) 'assets.rl')).Hash}else{$null})
    Hardware=[pscustomobject]@{LogicalProcessors=[Environment]::ProcessorCount;Cpu=@(Get-CimInstance Win32_Processor | Select-Object -ExpandProperty Name);Gpu=@(Get-CimInstance Win32_VideoController | Select-Object Name,DriverVersion)}
    DisplayStateBefore=$displayBefore; DisplayStateAfter=(Check-SelectedShip)
    NativeWindowBefore=$nativeBefore; NativeWindowAfter=(Read-MonitorWindowState -MonitorProcessId $receipt.processId)
    WindowStateBasis='Read-only IsIconic/IsWindowVisible with HWND-to-PID verification; optional 45-point interior WindowFromPoint coverage check on every sample; no window input or movement'
    RequireForeground=[bool]$RequireForeground; RequireUncovered=[bool]$RequireUncovered
    StartedAt=$rows[0].Time; CompletedAt=$rows[-1].Time; Samples=$rows.Count
    ElapsedSeconds=($rows | Measure-Object IntervalSeconds -Sum).Sum
    CpuPercentOfMachine=(Summary 'CpuPercent'); WorkingSetMiB=(Summary 'WorkingMiB'); PrivateMiB=(Summary 'PrivateMiB')
    PrivateResidentMiB=(Summary 'PrivateResidentMiB')
    ResidentCounterSet='Win32_PerfFormattedData_PerfProc_Process.WorkingSetPrivate filtered by numeric IDProcess on every sample'
    GpuBusiestEnginePercent=(Summary 'GpuPercent'); GpuDedicatedCounterSumMiB=(Summary 'GpuDedicatedMiB'); GpuSharedCounterSumMiB=(Summary 'GpuSharedMiB')
    GpuMemoryStatus='Unvalidated per-process sums, not exclusive VRAM. These counters exceeded whole-board usage in the visible check and are excluded from resource conclusions.'
    WholeAdapterDedicatedMiB=(Summary 'WholeAdapterDedicatedMiB'); WholeAdapterSharedMiB=(Summary 'WholeAdapterSharedMiB')
    WholeAdapterMemoryScope='All applications and desktop on all Windows GPU adapters; context only, not attributed to this app.'
    GpuEngines=@($engineRows | Group-Object Engine | ForEach-Object { [pscustomobject]@{ Engine=$_.Name; Average=($_.Group | Measure-Object Percent -Average).Average; Maximum=($_.Group | Measure-Object Percent -Maximum).Maximum } })
    ProcessTree=$parts; ProcessIdentity=@($tree | Select-Object ProcessId,ParentProcessId,Name,CreationDate,ExecutablePath,CommandLine)
    IdentityBasis='Exact runtime receipt path and PID correlated with OS name/creation time; child creation must not predate parent (PID reuse excluded); stable descendants verified at end'
    CounterWarnings=@($counterIssues); CounterErrors=@($counterErrors | ForEach-Object { $_.ToString() })
    Limits='Short Windows counter sample. RAM working sets and per-process GPU memory may double-count shared pages. Private bytes are committed memory, not physical RAM. GPU percentage is busiest engine, not sum of independent engines. Desktop compositor/kernel driver costs outside this tree are excluded.'
}
$prefix=Join-Path $outputFolder "$($Ship.ToLowerInvariant())-$($View.ToLowerInvariant())-$($State.ToLowerInvariant())-$runId"
$rows | Export-Csv "$prefix.csv" -NoTypeInformation
$details | Export-Csv "$prefix-processes.csv" -NoTypeInformation
$engineRows | Export-Csv "$prefix-engines.csv" -NoTypeInformation
$result | ConvertTo-Json -Depth 8 | Set-Content "$prefix.json" -Encoding utf8
$result | Select-Object State,RootPid,Samples,ElapsedSeconds,CpuPercentOfMachine,PrivateResidentMiB,WorkingSetMiB,PrivateMiB,GpuBusiestEnginePercent,GpuMemoryStatus | Format-List
$parts | Sort-Object CpuAverage -Descending | Format-Table -AutoSize
Write-Host "Saved $prefix.json and raw sample CSVs."
