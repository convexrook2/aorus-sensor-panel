param([string]$Start='2026-10-01',[string]$Stop='2027-10-01',[switch]$CheckpointsOnly)
$ErrorActionPreference='Stop'
$taskRoot=Split-Path $PSScriptRoot -Parent
$taskReceipt=Join-Path $taskRoot ('artifacts/nasa-horizons-'+(Get-Date -Format 'yyyyMMdd-HHmmss'))
New-Item -ItemType Directory -Path $taskReceipt -Force | Out-Null
# Development-time download only. Runtime reads the bundled, bounded local data.
# JPL requests must be sequential: https://ssd-api.jpl.nasa.gov/doc/horizons.html
$taskDefs=@(
    @('Mercury','199','Sun',2439.4), @('Venus','299','Sun',6051.8),
    @('Earth','399','Sun',6371.0084), @('Luna','301','Earth',1737.4),
    @('Mars','499','Sun',3389.5), @('Ceres','1;','Sun',469.7), @('Eros','433;','Sun',8.4),
    @('Jupiter','599','Sun',69911), @('Io','501','Jupiter',1821.6),
    @('Europa','502','Jupiter',1560.8), @('Ganymede','503','Jupiter',2634.1),
    @('Callisto','504','Jupiter',2410.3), @('Saturn','699','Sun',58232),
    @('Phoebe','609','Saturn',106.5), @('Uranus','799','Sun',25362), @('Neptune','899','Sun',24622)
)
$taskIds=@{Sun='10';Earth='399';Jupiter='599';Saturn='699'}
if(-not $CheckpointsOnly){
$taskBodies=@(foreach($taskDef in $taskDefs){
    $taskId,$taskTarget,$taskParent,$taskRadius=$taskDef
    $taskStep=if($taskParent -eq 'Sun'){'1 d'}else{'1 h'}
    $taskParameters=[ordered]@{format='json';COMMAND="'$taskTarget'";CENTER="'500@$($taskIds[$taskParent])'";MAKE_EPHEM="'YES'";EPHEM_TYPE="'VECTORS'";START_TIME="'$Start'";STOP_TIME="'$Stop'";STEP_SIZE="'$taskStep'";OUT_UNITS="'AU-D'";REF_PLANE="'ECLIPTIC'";REF_SYSTEM="'ICRF'";VEC_CORR="'NONE'";VEC_TABLE="'2'";CSV_FORMAT="'YES'";OBJ_DATA="'YES'"}
    $taskQuery=($taskParameters.GetEnumerator() | ForEach-Object {[uri]::EscapeDataString($_.Key)+'='+[uri]::EscapeDataString($_.Value)}) -join '&'
    $taskUri='https://ssd.jpl.nasa.gov/api/horizons.api?'+$taskQuery
    Write-Host "NASA/JPL Horizons: $taskId relative to $taskParent, $taskStep, $Start to $Stop"
    $taskResponse=Invoke-RestMethod -Uri $taskUri -TimeoutSec 90
    if($taskResponse.error){throw $taskResponse.error}
    $taskResponse | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $taskReceipt ($taskId+'.json')) -Encoding utf8
    $taskMatch=[regex]::Match($taskResponse.result,'\$\$SOE\s*([\s\S]*?)\s*\$\$EOE')
    if(-not $taskMatch.Success){throw "No ephemeris for $taskId"}
    $taskRows=@(foreach($taskLine in ($taskMatch.Groups[1].Value -split '\r?\n')){
        $taskFields=$taskLine.Split(',')
        if($taskFields.Count -lt 8){continue}
        $taskNumbers=@(foreach($taskIndex in @(0,2,3,4,5,6,7)){[double]::Parse($taskFields[$taskIndex].Trim(),[cultureinfo]::InvariantCulture)})
        ,$taskNumbers
    })
    if($taskRows.Count -lt 2){throw "Too few samples for $taskId"}
    [ordered]@{Id=$taskId;Target=$taskTarget;Parent=$taskParent;RadiusKm=$taskRadius;Query=$taskUri;Samples=$taskRows}
})
$taskData=[ordered]@{Schema=1;Source='NASA/JPL Horizons';RetrievedUtc=[datetime]::UtcNow.ToString('o');Start=$Start;Stop=$Stop;Frame='J2000 ecliptic / ICRF, geometric vectors';TimeScale='TDB';Units='AU and AU/day';AuKm=149597870.7;SunRadiusKm=695700;PhysicalParameters='https://ssd.jpl.nasa.gov/planets/phys_par.html; https://ssd.jpl.nasa.gov/sats/phys_par/';Bodies=$taskBodies}
$taskOutput=Join-Path $taskRoot 'Assets/SolarNavigation/nasa-horizons.json.gz'
$taskBytes=[text.encoding]::UTF8.GetBytes(($taskData | ConvertTo-Json -Depth 8 -Compress))
$taskFile=[IO.File]::Create($taskOutput)
try{$taskGzip=[IO.Compression.GZipStream]::new($taskFile,[IO.Compression.CompressionLevel]::SmallestSize);try{$taskGzip.Write($taskBytes,0,$taskBytes.Length)}finally{$taskGzip.Dispose()}}finally{$taskFile.Dispose()}
Write-Host ('Saved local NASA vectors: '+(Get-Item -LiteralPath $taskOutput).Length+' bytes. '+$taskReceipt)
}
# Independent off-grid vectors test interpolation, rather than testing it against itself.
$taskChecks=@(foreach($taskDef in @(@('Luna','301','399',2461329.5208333335,1),@('Io','501','599',2461329.5208333335,2),@('Mercury','199','10',2461330.0,5))){
    $taskId,$taskTarget,$taskCenter,$taskJd,$taskTolerance=$taskDef
    $taskParameters=[ordered]@{format='json';COMMAND="'$taskTarget'";CENTER="'500@$taskCenter'";MAKE_EPHEM="'YES'";EPHEM_TYPE="'VECTORS'";TLIST="'$($taskJd.ToString('R',[cultureinfo]::InvariantCulture))'";OUT_UNITS="'AU-D'";REF_PLANE="'ECLIPTIC'";REF_SYSTEM="'ICRF'";VEC_CORR="'NONE'";VEC_TABLE="'2'";CSV_FORMAT="'YES'"}
    $taskQuery=($taskParameters.GetEnumerator()|ForEach-Object{[uri]::EscapeDataString($_.Key)+'='+[uri]::EscapeDataString($_.Value)}) -join '&'
    $taskUri='https://ssd.jpl.nasa.gov/api/horizons.api?'+$taskQuery
    Write-Host ('NASA off-grid verification point: '+$taskId)
    $taskResponse=Invoke-RestMethod -Uri $taskUri -TimeoutSec 60
    if($taskResponse.error){throw $taskResponse.error}
    $taskResponse|ConvertTo-Json -Depth 5|Set-Content -LiteralPath (Join-Path $taskReceipt ($taskId+'-off-grid.json')) -Encoding utf8
    $taskLine=[regex]::Match($taskResponse.result,'\$\$SOE\s*([^\r\n]+)').Groups[1].Value
    $taskFields=$taskLine.Split(','); if($taskFields.Count -lt 8){throw 'Missing independent NASA vector.'}
    $taskPosition=@(foreach($taskIndex in 2..4){[double]::Parse($taskFields[$taskIndex].Trim(),[cultureinfo]::InvariantCulture)})
    [ordered]@{Id=$taskId;JulianDate=$taskJd;Position=$taskPosition;ToleranceKm=$taskTolerance;Query=$taskUri}
})
$taskChecks|ConvertTo-Json -Depth 5|Set-Content -LiteralPath (Join-Path $taskRoot 'Assets/SolarNavigation/nasa-checkpoints.json') -Encoding utf8
