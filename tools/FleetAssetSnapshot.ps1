function Get-FleetAssetSnapshot([string]$RepositoryRoot) {
    $root=[IO.Path]::GetFullPath($RepositoryRoot).TrimEnd('\','/')
    $snapshot=[Collections.Generic.Dictionary[string,string]]::new([StringComparer]::Ordinal)
    foreach($relative in @('Assets','FleetStudio/models')) {
        $directory=Join-Path $root $relative
        if(!(Test-Path -LiteralPath $directory -PathType Container)){throw "Missing protected asset input root: $relative"}
        foreach($file in Get-ChildItem -LiteralPath $directory -File -Recurse) {
            $key=$file.FullName.Substring($root.Length+1).Replace('\','/')
            $snapshot.Add($key,([string]$file.Length+':'+(Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash))
        }
    }
    return ,$snapshot
}

function Assert-FleetAssetSnapshot($Before,$After) {
    foreach($path in $Before.Keys) {
        if(!$After.ContainsKey($path)){throw "Asset removed during checks: $path"}
        if($Before[$path] -ne $After[$path]){throw "Asset bytes changed during checks: $path"}
    }
    foreach($path in $After.Keys) {
        if(!$Before.ContainsKey($path)){throw "Asset added during checks: $path"}
    }
}

function Assert-CleanCiSource([string[]]$Changes) {
    if($null -ne $Changes -and $Changes.Count -gt 0){throw 'Jenkins requires clean tracked and untracked source at both entry and exit.'}
}
