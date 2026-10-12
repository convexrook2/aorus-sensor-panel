$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
$artifactDirectory = Join-Path $projectRoot 'artifacts'
New-Item -ItemType Directory -Path $artifactDirectory -Force | Out-Null
$logPath = Join-Path $artifactDirectory 'libre-validation.txt'
Start-Transcript -Path $logPath -Force | Out-Null
try {
    $project = Join-Path $projectRoot 'SensorCollector\SensorCollector.csproj'
    $collector = Join-Path $projectRoot 'SensorCollector\bin\Release\net10.0-windows\SensorCollector.exe'
    Write-Host 'Building Release; restores the pinned LibreHardwareMonitorLib 0.9.6 NuGet package.'
    dotnet build $project -c Release
    if ($LASTEXITCODE -ne 0) { throw "Build failed: $LASTEXITCODE. Live collection was not started." }
    Write-Host 'Running source-contract checks, without hardware access.'
    & $collector --self-test
    if ($LASTEXITCODE -ne 0) { throw "Source checks failed: $LASTEXITCODE. Live collection was not started." }
    $identity = [Security.Principal.WindowsIdentity]::GetCurrent()
    try { $elevated = ([Security.Principal.WindowsPrincipal]::new($identity)).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator) }
    finally { $identity.Dispose() }
    if (-not $elevated) { throw 'Build/checks completed. Repeat this script in an administrator VS Code PowerShell terminal for live PawnIO readings.' }
    Write-Host 'Collecting three CPU/motherboard samples through embedded LibreHardwareMonitor and GPU samples through NVIDIA NVML.'
    & $collector --libre --count 3 --interval-ms 1000 --out (Join-Path $artifactDirectory 'collector-libre.json')
    if ($LASTEXITCODE -ne 0) { throw "Collector failed: $LASTEXITCODE." }
    Write-Host 'Collection completed. Inspect source statuses; exit code alone does not prove valid hardware readings.'
    Write-Host 'Saved artifacts/collector-libre.json and artifacts/libre-validation.txt.'
}
finally { Stop-Transcript | Out-Null }
