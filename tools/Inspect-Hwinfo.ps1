$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
$memory = $null; $view = $null
try {
    $memory = [IO.MemoryMappedFiles.MemoryMappedFile]::OpenExisting('Global\HWiNFO_SENS_SM2', [IO.MemoryMappedFiles.MemoryMappedFileRights]::Read)
    $view = $memory.CreateViewAccessor(0, 0, [IO.MemoryMappedFiles.MemoryMappedFileAccess]::Read)
    $signature = $view.ReadUInt32(0)
    if ($signature -ne 0x53695748) { throw ('Inactive HWiNFO export signature 0x{0:X8}; no shared-memory limit/settings are changed.' -f $signature) }
    $sensorOffset = $view.ReadUInt32(20); $sensorSize = $view.ReadUInt32(24); $sensorCount = $view.ReadUInt32(28)
    $readingOffset = $view.ReadUInt32(32); $readingSize = $view.ReadUInt32(36); $readingCount = $view.ReadUInt32(40)
    if ($sensorSize -lt 264 -or $readingSize -lt 316 -or $sensorCount -gt 4096 -or $readingCount -gt 100000 -or
        [long]$sensorOffset + [long]$sensorSize * $sensorCount -gt $view.Capacity -or
        [long]$readingOffset + [long]$readingSize * $readingCount -gt $view.Capacity) { throw 'Unsupported/corrupt HWiNFO export layout.' }
    function Read-Label([long]$offset, [int]$length) {
        $bytes = [byte[]]::new($length); [void]$view.ReadArray($offset, $bytes, 0, $length)
        return [Text.Encoding]::UTF8.GetString($bytes).Split([char]0)[0]
    }
    $sensors = @()
    for ($index = 0; $index -lt $sensorCount; $index++) {
        $offset = [long]$sensorOffset + [long]$index * $sensorSize
        $sensors += [pscustomobject]@{Id=$view.ReadUInt32($offset);Instance=$view.ReadUInt32($offset+4);Name=(Read-Label ($offset+8) 128);UserName=(Read-Label ($offset+136) 128)}
    }
    $readings = @()
    for ($index = 0; $index -lt $readingCount; $index++) {
        $offset = [long]$readingOffset + [long]$index * $readingSize
        $sensorIndex = $view.ReadUInt32($offset+4)
        if ($sensorIndex -ge $sensors.Count) { throw 'Invalid sensor index.' }
        $sensor = $sensors[$sensorIndex]; $name = Read-Label ($offset+12) 128
        if ($sensor.Id -eq 0xf0000ff0 -or $sensor.Name -match 'PresentMon|RTSS|FrameView' -or $name -match 'SVI3|Vcore|Core Voltage|VDDCR_VDD') {
            $readings += [pscustomobject]@{SensorId=('0x{0:X8}' -f $sensor.Id);Instance=$sensor.Instance;Sensor=$sensor.Name;SensorUser=$sensor.UserName;EntryId=('0x{0:X8}' -f $view.ReadUInt32($offset+8));Label=$name;UserLabel=(Read-Label ($offset+140) 128);Unit=(Read-Label ($offset+268) 16);Value=$view.ReadDouble($offset+284);Minimum=$view.ReadDouble($offset+292);Maximum=$view.ReadDouble($offset+300)}
        }
    }
    $report = [pscustomobject]@{CapturedAt=(Get-Date).ToString('o');PollUnix=$view.ReadInt64(12);Version=$view.ReadUInt32(4);Revision=$view.ReadUInt32(8);SensorSize=$sensorSize;ReadingSize=$readingSize;Readings=$readings}
    $report | ConvertTo-Json -Depth 6 | Set-Content (Join-Path $projectRoot 'artifacts\hwinfo-live-read.json') -Encoding UTF8
    $readings | Format-List
}
finally { if ($view) { $view.Dispose() }; if ($memory) { $memory.Dispose() } }
