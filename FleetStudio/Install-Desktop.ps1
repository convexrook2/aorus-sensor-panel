param([switch]$Launch)
$ErrorActionPreference='Stop'
$repoPath=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$appFolder=Join-Path ([Environment]::GetFolderPath('LocalApplicationData')) 'Programs\Fleet Studio'
$appExe=Join-Path $appFolder 'FleetStudio.Desktop.exe'
$desktopLink=Join-Path ([Environment]::GetFolderPath('DesktopDirectory')) 'Fleet Studio.lnk'
$startLink=Join-Path ([Environment]::GetFolderPath('Programs')) 'Fleet Studio.lnk'
$receiptFolder=Join-Path $repoPath 'artifacts\fleet-desktop-launcher'
New-Item -ItemType Directory -Path $receiptFolder -Force | Out-Null
Set-Location -LiteralPath $repoPath
$shell=New-Object -ComObject WScript.Shell
# Preserve an unrelated shortcut rather than taking over its name.
foreach($link in @($desktopLink,$startLink)){
    if(Test-Path -LiteralPath $link){
        $existing=$shell.CreateShortcut($link)
        if($existing.TargetPath -ne $appExe){throw "An unrelated shortcut already uses $link. It has been left unchanged."}
    }
}
$running=@(Get-CimInstance Win32_Process -Filter "Name='FleetStudio.Desktop.exe'" | Where-Object {$_.ExecutablePath -eq $appExe})
if($running.Count){throw 'The desktop copy is open. Close Fleet Studio before refreshing it.'}
Write-Host 'Preparing the on-demand desktop app. This is a one-time setup, not a startup service.'
dotnet publish .\FleetStudio.Desktop\FleetStudio.Desktop.csproj -c Release --self-contained false -p:UseAppHost=true -o $appFolder
if($LASTEXITCODE -ne 0){throw 'Fleet Studio desktop publish failed; shortcuts were not changed.'}
if(!(Test-Path -LiteralPath $appExe)){throw 'Fleet Studio desktop executable is missing.'}
$sourceAssets=Join-Path $repoPath 'FleetStudio'
$destinationAssets=Join-Path $appFolder 'FleetStudio'
$manifest=Get-Content -LiteralPath (Join-Path $sourceAssets 'manifest.json') -Raw|ConvertFrom-Json
$checks=[Collections.Generic.List[object]]::new()
foreach($asset in $manifest.assets){
    $path=Join-Path $destinationAssets $asset.file
    $hash=(Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant()
    if($hash -ne $asset.gzip_sha256){throw "Packaged model hash mismatch: $($asset.file)"}
    $checks.Add([pscustomobject]@{File=$asset.file;Sha256=$hash})
}
foreach($file in Get-ChildItem -LiteralPath $sourceAssets -File | Where-Object Extension -in @('.html','.css','.js','.json')){
    $deployed=Join-Path $destinationAssets $file.Name
    if((Get-FileHash -LiteralPath $file.FullName).Hash -ne (Get-FileHash -LiteralPath $deployed).Hash){throw "Packaged interface mismatch: $($file.Name)"}
}
foreach($link in @($desktopLink,$startLink)){
    $shortcut=$shell.CreateShortcut($link)
    $shortcut.TargetPath=$appExe
    $shortcut.WorkingDirectory=$appFolder
    $shortcut.IconLocation="$appExe,0"
    $shortcut.Description='Fleet Studio — ship viewer. Opens on demand; close its window to exit.'
    $shortcut.WindowStyle=1
    $shortcut.Save()
    if($shell.CreateShortcut($link).TargetPath -ne $appExe){throw "Shortcut verification failed: $link"}
    Write-Host "READY: $link"
}
[pscustomobject]@{CreatedAt=[datetimeoffset]::Now;App=$appExe;Desktop=$desktopLink;StartMenu=$startLink;ModelHashes=$checks;OnDemand=$true;StartupTaskCreated=$false} |
    ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $receiptFolder 'setup.json') -Encoding utf8
Write-Host 'READY: open Fleet Studio from its Desktop icon or Start menu. No editor, build command or server is needed.'
if($Launch){
    Write-Host 'Testing the actual Desktop shortcut; the Fleet Studio window will be visible.'
    Invoke-Item -LiteralPath $desktopLink
}
