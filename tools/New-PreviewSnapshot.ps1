param([string]$Path)
$ErrorActionPreference='Stop'
$projectRoot=Split-Path $PSScriptRoot -Parent
if(-not $Path){$Path=Join-Path $projectRoot 'artifacts/synthetic-preview.json'}
$now=[DateTimeOffset]::UtcNow.ToString('o')
$readings=@(
    @('libre/amdcpu/0/temperature/2','Example CPU package',54,'°C','Synthetic'),
    @('libre/amdcpu/0/power/0','Example CPU power',65,'W','Synthetic'),
    @('libre/amdcpu/0/clock/1','Example CPU clock',4200,'MHz','Synthetic'),
    @('cpu/utilization','Example CPU utilization',17,'%','Synthetic'),
    @('cpu/core-voltage','Example measured rail',1.15,'V','Synthetic'),
    @('ram/mclk','Example RAM clock',2400,'MHz','Synthetic'),
    @('ram/data-rate','Example DDR data rate',4800,'MT/s','Synthetic'),
    @('nvidia/GPU-example/temperature','NVIDIA Example GPU: Temperature',45,'°C','NVIDIA NVML'),
    @('nvidia/GPU-example/power','NVIDIA Example GPU: Power',85,'W','NVIDIA NVML'),
    @('nvidia/GPU-example/utilization','NVIDIA Example GPU: Utilization',24,'%','NVIDIA NVML'),
    @('nvidia/GPU-example/graphics-clock','NVIDIA Example GPU: Graphics clock',1800,'MHz','NVIDIA NVML'),
    @('nvidia/GPU-example/memory-clock','NVIDIA Example GPU: Memory clock',7000,'MHz','NVIDIA NVML')
)|ForEach-Object {[ordered]@{id=$_[0];name=$_[1];value=$_[2];unit=$_[3];source=$_[4];status='ok';sampledAt=$now;detail='Fictional presentation fixture; not a hardware measurement.'}}
$snapshot=[ordered]@{capturedAt=$now;elevated=$false;motherboard='Example motherboard';cpu='Example processor';readings=@($readings);sources=@(@{source='Synthetic preview';status='available';detail='Fictional presentation data. No hardware was read.'})}
$absolute=[IO.Path]::GetFullPath($Path)
New-Item -ItemType Directory -Path (Split-Path $absolute -Parent) -Force|Out-Null
$snapshot|ConvertTo-Json -Depth 6|Set-Content -LiteralPath $absolute -Encoding utf8
Write-Output $absolute
