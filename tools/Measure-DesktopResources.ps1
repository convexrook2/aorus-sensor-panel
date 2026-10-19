param(
    [ValidateSet('Monitor','FleetStudio')][string]$App = 'Monitor',
    [ValidateRange(2,30)][int]$Seconds = 5
)
$ErrorActionPreference = 'Stop'
$repoPath = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$projectName = if ($App -eq 'Monitor') { 'SensorPanel' } else { 'FleetStudio.Desktop' }
$binaryRoot = Join-Path $repoPath "$projectName\bin\"
$processes = @(Get-CimInstance Win32_Process)
$roots = @($processes | Where-Object {
    ($_.ExecutablePath -and $_.ExecutablePath.StartsWith($binaryRoot,[StringComparison]::OrdinalIgnoreCase) -and $_.Name -eq "$projectName.exe") -or
    ($_.Name -eq 'dotnet.exe' -and $_.CommandLine -and $_.CommandLine.IndexOf($binaryRoot,[StringComparison]::OrdinalIgnoreCase) -ge 0 -and $_.CommandLine.IndexOf("$projectName.dll",[StringComparison]::OrdinalIgnoreCase) -ge 0)
})
if ($roots.Count -eq 0 -and $App -eq 'Monitor') {
    # Windows may hide an elevated process's path from this terminal. Correlate
    # the live app's own receipt with its PID/name and OS creation time.
    $receiptPath=Join-Path $repoPath 'artifacts\live-runtime-check.json'
    if (Test-Path -LiteralPath $receiptPath) {
        $receipt=Get-Content -LiteralPath $receiptPath -Raw | ConvertFrom-Json
        $expected=Join-Path $binaryRoot 'Debug\net10.0-windows\SensorPanel.exe'
        $candidate=@($processes | Where-Object { $_.ProcessId -eq $receipt.processId -and $_.Name -eq 'SensorPanel.exe' })
        if ($receipt.preview -eq $false -and $receipt.executablePath -eq $expected -and $candidate.Count -eq 1 -and
            [math]::Abs((([datetime]$candidate[0].CreationDate).ToUniversalTime()-([datetime]$receipt.startedAt).ToUniversalTime()).TotalSeconds) -lt 15) {
            $roots=$candidate
            Write-Host 'Root identity: live app receipt + visible source launch + OS PID/name/creation time (elevated path hidden).'
        }
    }
}
if ($roots.Count -ne 1) { throw "Expected one verified $App process in this checkout; found $($roots.Count)." }
$ids = [Collections.Generic.HashSet[int]]::new()
[void]$ids.Add([int]$roots[0].ProcessId)
do {
    $added = $false
    foreach ($item in $processes) {
        if ($ids.Contains([int]$item.ParentProcessId) -and $ids.Add([int]$item.ProcessId)) { $added = $true }
    }
} while ($added)
$processes | Where-Object { $ids.Contains([int]$_.ProcessId) } | Select-Object ProcessId,ParentProcessId,Name,ExecutablePath,CommandLine | Format-List
$before = @{}
foreach ($processId in $ids) {
    $process = Get-Process -Id $processId -ErrorAction SilentlyContinue
    if ($process) { $before[$processId] = $process.TotalProcessorTime.TotalSeconds }
}
$watch = [Diagnostics.Stopwatch]::StartNew()
Start-Sleep -Seconds $Seconds
$cpuSeconds = 0.0; $workingBytes = 0L; $privateBytes = 0L; $alive = 0
foreach ($processId in $ids) {
    $process = Get-Process -Id $processId -ErrorAction SilentlyContinue
    if ($process -and $before.ContainsKey($processId)) {
        $cpuSeconds += $process.TotalProcessorTime.TotalSeconds - $before[$processId]
        $workingBytes += $process.WorkingSet64; $privateBytes += $process.PrivateMemorySize64; $alive++
    }
}
$watch.Stop()
[pscustomobject]@{
    App=$App; RootPid=$roots[0].ProcessId; Processes=$alive; Seconds=[math]::Round($watch.Elapsed.TotalSeconds,2)
    CpuPercentOfMachine=[math]::Round(100*$cpuSeconds/$watch.Elapsed.TotalSeconds/[Environment]::ProcessorCount,3)
    SummedWorkingSetMiB=[math]::Round($workingBytes/1MB,1); SummedPrivateMiB=[math]::Round($privateBytes/1MB,1)
    Scope='This interval and verified process tree only; working sets may include shared pages; no GPU/VRAM measurement'
} | Format-List
