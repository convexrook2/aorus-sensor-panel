$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
Set-Location -LiteralPath $projectRoot
Start-Transcript -Path '.\artifacts\panel-resource-usage.txt' -Force
try {
    $started = Get-Date
    $logicalProcessors = (Get-CimInstance Win32_ComputerSystem).NumberOfLogicalProcessors
    $processes = @(Get-CimInstance Win32_Process)
    $targets = @($processes | Where-Object { $_.Name -match '^(SensorPanel|SensorCollector|PresentMon.*|HWiNFO.*|Rainmeter|nvfvsdksvc_x64)\.exe$' })
    $targetIds = @($targets.ProcessId)
    if (-not ($targets.Name -contains 'SensorPanel.exe')) { throw 'No running panel to measure.' }
    $panelIds = @($targets | Where-Object Name -eq 'SensorPanel.exe' | Select-Object -ExpandProperty ProcessId)
    $ownedIds = @($targets | Where-Object { $_.ProcessId -in $panelIds -or ($_.Name -like 'PresentMon*' -and $_.ParentProcessId -in $panelIds) } | Select-Object -ExpandProperty ProcessId)
    function Read-Counters {
        $cpu = @(Get-CimInstance Win32_PerfRawData_PerfProc_Process | Where-Object { $_.IDProcess -in $targetIds })
        $gpu = @(Get-CimInstance Win32_PerfRawData_GPUPerformanceCounters_GPUEngine | Where-Object {
            $_.Name -match '^pid_(\d+)_' -and [int]$Matches[1] -in $targetIds
        })
        [pscustomobject]@{ CPU=$cpu; GPU=$gpu }
    }
    $previous = Read-Counters
    $samples = @()
    Write-Host 'Read-only 30-second resource sample; CPU is normalized across all logical processors.'
    Write-Host ('Logical processors: ' + $logicalProcessors)
    for ($index = 1; $index -le 6; $index++) {
        Start-Sleep -Seconds 5
        $current = Read-Counters
        $sample = @()
        foreach ($counter in $current.CPU) {
            $old = @($previous.CPU | Where-Object { $_.IDProcess -eq $counter.IDProcess -and $_.Name -eq $counter.Name })
            if ($old.Count -ne 1) { continue }
            $timeDelta = [double]$counter.Timestamp_Sys100NS - [double]$old[0].Timestamp_Sys100NS
            if ($timeDelta -le 0) { continue }
            $cpuPercent = 100 * ([double]$counter.PercentProcessorTime - [double]$old[0].PercentProcessorTime) / $timeDelta / $logicalProcessors
            $engines = @()
            foreach ($engine in $current.GPU | Where-Object { $_.Name -like ('pid_' + $counter.IDProcess + '_*') }) {
                $oldEngine = @($previous.GPU | Where-Object Name -eq $engine.Name)
                if ($oldEngine.Count -ne 1) { continue }
                $gpuTimeDelta = [double]$engine.Timestamp_Sys100NS - [double]$oldEngine[0].Timestamp_Sys100NS
                if ($gpuTimeDelta -gt 0) {
                    $engines += [pscustomobject]@{Name=$engine.Name; Percent=(100 * ([double]$engine.RunningTime - [double]$oldEngine[0].RunningTime) / $gpuTimeDelta)}
                }
            }
            $busiest = $engines | Sort-Object Percent -Descending | Select-Object -First 1
            $target = $targets | Where-Object ProcessId -eq $counter.IDProcess | Select-Object -First 1
            $sample += [pscustomobject]@{
                Interval=$index; SampledAt=(Get-Date).ToString('o'); Name=$target.Name; PID=[int]$counter.IDProcess
                ParentPID=[int]$target.ParentProcessId; OwnedByPanel=($counter.IDProcess -in $ownedIds)
                CPUPercent=$cpuPercent; WorkingSetMiB=([double]$counter.WorkingSet / 1MB)
                PrivateWorkingSetMiB=([double]$counter.WorkingSetPrivate / 1MB); PrivateCommittedMiB=([double]$counter.PrivateBytes / 1MB)
                BusiestGPUEnginePercent=$(if ($busiest) {$busiest.Percent} else {$null})
                BusiestGPUEngine=$(if ($busiest) {$busiest.Name} else {$null})
            }
        }
        $samples += $sample
        $sample | Select-Object Interval,Name,PID,@{N='CPU %';E={[math]::Round($_.CPUPercent,3)}},@{N='RAM MiB';E={[math]::Round($_.WorkingSetMiB,1)}},@{N='GPU engine %';E={[math]::Round([double]$_.BusiestGPUEnginePercent,2)}} | Format-Table -AutoSize
        $previous = $current
    }
    $summary = @($samples | Group-Object PID | ForEach-Object {
        $rows = @($_.Group)
        [pscustomobject]@{
            Name=$rows[0].Name; PID=$rows[0].PID; OwnedByPanel=$rows[0].OwnedByPanel
            CPUAveragePercent=($rows | Measure-Object CPUPercent -Average).Average
            CPUPeakIntervalPercent=($rows | Measure-Object CPUPercent -Maximum).Maximum
            WorkingSetAverageMiB=($rows | Measure-Object WorkingSetMiB -Average).Average
            PrivateWorkingSetAverageMiB=($rows | Measure-Object PrivateWorkingSetMiB -Average).Average
            PrivateCommittedAverageMiB=($rows | Measure-Object PrivateCommittedMiB -Average).Average
            BusiestGPUEngineAveragePercent=($rows | Where-Object {$null -ne $_.BusiestGPUEnginePercent} | Measure-Object BusiestGPUEnginePercent -Average).Average
        }
    })
    $combined = @($samples | Where-Object OwnedByPanel | Group-Object Interval | ForEach-Object {
        [pscustomobject]@{
            CPUPercent=($_.Group | Measure-Object CPUPercent -Sum).Sum
            WorkingSetMiB=($_.Group | Measure-Object WorkingSetMiB -Sum).Sum
            PrivateWorkingSetMiB=($_.Group | Measure-Object PrivateWorkingSetMiB -Sum).Sum
            PrivateCommittedMiB=($_.Group | Measure-Object PrivateCommittedMiB -Sum).Sum
        }
    })
    $report = [pscustomobject]@{
        StartedAt=$started.ToString('o'); CompletedAt=(Get-Date).ToString('o'); LogicalProcessors=$logicalProcessors
        GamesAtEnd=@(Get-CimInstance Win32_Process | Where-Object Name -in @('bf6.exe','Le Mans Ultimate.exe') | Select-Object Name,ProcessId)
        Summary=$summary
        PanelCombined=[pscustomobject]@{
            CPUAveragePercent=($combined | Measure-Object CPUPercent -Average).Average
            CPUPeakIntervalPercent=($combined | Measure-Object CPUPercent -Maximum).Maximum
            WorkingSetAverageMiB=($combined | Measure-Object WorkingSetMiB -Average).Average
            PrivateWorkingSetAverageMiB=($combined | Measure-Object PrivateWorkingSetMiB -Average).Average
            PrivateCommittedAverageMiB=($combined | Measure-Object PrivateCommittedMiB -Average).Average
        }
        Notes=@('CPU is total-machine percentage normalized by LogicalProcessors; intervals are about 5 seconds.', 'Working set includes shared pages and is not additive unique physical memory. Private working set excludes shared pages.', 'Private committed bytes include nonresident memory; do not add them to working set.', 'GPU values are each process busiest engine, not a game FPS impact measurement.', 'No HWiNFO/Rainmeter baseline unless those processes appear in Summary. No apps were started/stopped or settings changed.')
        Samples=$samples
    }
    $report | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath '.\artifacts\panel-resource-usage.json' -Encoding utf8
    Write-Host 'Panel plus owned FPS helper:'
    $report.PanelCombined | Format-List
    Write-Host 'Saved artifacts\panel-resource-usage.json and panel-resource-usage.txt. This window can be closed.'
}
finally { Stop-Transcript }
