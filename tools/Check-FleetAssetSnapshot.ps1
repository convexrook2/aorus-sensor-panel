$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
. (Join-Path $PSScriptRoot 'FleetAssetSnapshot.ps1')
$repoRoot=Split-Path $PSScriptRoot -Parent
$fixture=Join-Path $repoRoot ('artifacts\asset-snapshot-check-'+[guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path (Join-Path $fixture 'Assets'),(Join-Path $fixture 'FleetStudio\models') -Force | Out-Null
$asset=Join-Path $fixture 'Assets\fixture.txt'
$model=Join-Path $fixture 'FleetStudio\models\fixture.txt'
Set-Content -LiteralPath $asset -Value 'before'
Set-Content -LiteralPath $model -Value 'source'
$passed=[Collections.Generic.List[string]]::new()
function Reject([scriptblock]$Action,[string]$Pattern){
    $failed=$false
    try{& $Action}catch{$failed=$true;if($_.Exception.Message -notmatch $Pattern){throw}}
    if(!$failed){throw "Expected rejection: $Pattern"}
}
$baseline=Get-FleetAssetSnapshot $fixture
Assert-FleetAssetSnapshot $baseline (Get-FleetAssetSnapshot $fixture)
$passed.Add('existing candidate asset inputs remain valid when unchanged')
Set-Content -LiteralPath $asset -Value 'after!'
Reject {Assert-FleetAssetSnapshot $baseline (Get-FleetAssetSnapshot $fixture)} 'bytes changed'
$passed.Add('same-size modified bytes rejected')
Set-Content -LiteralPath $asset -Value 'before'
$added=Join-Path $fixture 'Assets\added.txt'
Set-Content -LiteralPath $added -Value 'new'
Reject {Assert-FleetAssetSnapshot $baseline (Get-FleetAssetSnapshot $fixture)} 'added'
$passed.Add('new asset rejected')
Remove-Item -LiteralPath $added
Remove-Item -LiteralPath $asset
Reject {Assert-FleetAssetSnapshot $baseline (Get-FleetAssetSnapshot $fixture)} 'removed'
$passed.Add('removed asset rejected')
Set-Content -LiteralPath $asset -Value 'before'
Set-Content -LiteralPath $model -Value 'changed'
Reject {Assert-FleetAssetSnapshot $baseline (Get-FleetAssetSnapshot $fixture)} 'bytes changed'
$passed.Add('Studio model mutation rejected')
Reject {Assert-CleanCiSource @(' M tracked.cs')} 'clean tracked and untracked'
$passed.Add('dirty tracked Jenkins source rejected')
Reject {Assert-CleanCiSource @('?? untracked.cs')} 'clean tracked and untracked'
$passed.Add('untracked Jenkins source rejected')
Assert-CleanCiSource @()
$passed.Add('clean Jenkins source accepted')
[pscustomobject]@{Passed=$true;Count=$passed.Count;Cases=$passed;RealAssetsModified=$false} |
    ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $fixture 'result.json')
Write-Host "PASS: $($passed.Count) asset-immutability and Jenkins-cleanliness cases; production assets untouched."
