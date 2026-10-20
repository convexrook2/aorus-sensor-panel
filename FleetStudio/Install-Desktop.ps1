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
$stage=Join-Path $receiptFolder ('publish-'+(Get-Date -Format 'yyyyMMdd-HHmmss-fff'))
dotnet publish .\FleetStudio.Desktop\FleetStudio.Desktop.csproj -c Release --self-contained false -p:UseAppHost=true -p:DebugType=None -p:DebugSymbols=false -o $stage
if($LASTEXITCODE -ne 0){throw 'Fleet Studio publish failed; installed files and shortcuts were not changed.'}
$archive=Join-Path $stage 'assets.rl'
if(!(Test-Path -LiteralPath $archive) -or (Test-Path -LiteralPath (Join-Path $stage 'FleetStudio'))){throw 'Encrypted desktop publish is incomplete.'}
dotnet .\tools\RlPack\bin\Release\net10.0\RlPack.dll verify --key (Join-Path $env:LOCALAPPDATA 'LennonBuildKeys\X890.FleetStudio-v2.key') --archive $archive --source $repoPath --product X890.FleetStudio
if($LASTEXITCODE -ne 0){throw 'Encrypted Fleet Studio assets differ from source.'}
# Remove only positively identified former loose assets. Preserve unknown edits.
$oldRoot=[IO.Path]::GetFullPath((Join-Path $appFolder 'FleetStudio'))
$ownedOldFiles=@()
if(Test-Path -LiteralPath $oldRoot){
    if((Get-Item -LiteralPath $oldRoot).Attributes -band [IO.FileAttributes]::ReparsePoint){throw 'Installed assets are a link; preserve them for manual review.'}
    foreach($file in Get-ChildItem -LiteralPath $oldRoot -File -Recurse){
        $absolute=[IO.Path]::GetFullPath($file.FullName)
        if(!$absolute.StartsWith($oldRoot+'\',[StringComparison]::OrdinalIgnoreCase)){throw 'Old asset path escaped the installation.'}
        if($file.Attributes -band [IO.FileAttributes]::ReparsePoint){throw 'Linked old asset requires manual review.'}
        $original=Join-Path (Join-Path $repoPath 'FleetStudio') $absolute.Substring($oldRoot.Length+1)
        if(!(Test-Path -LiteralPath $original) -or (Get-FileHash -LiteralPath $absolute).Hash -ne (Get-FileHash -LiteralPath $original).Hash){throw "Unknown or changed old asset preserved: $absolute"}
        $ownedOldFiles+=$absolute
    }
}
New-Item -ItemType Directory -Path $appFolder -Force | Out-Null
Copy-Item -Path (Join-Path $stage '*') -Destination $appFolder -Recurse -Force
if((Get-FileHash -LiteralPath $archive).Hash -ne (Get-FileHash -LiteralPath (Join-Path $appFolder 'assets.rl')).Hash){throw 'Installed encrypted archive copy failed.'}
foreach($ownedPath in $ownedOldFiles){Remove-Item -LiteralPath $ownedPath}
if(!(Test-Path -LiteralPath $appExe)){throw 'Fleet Studio desktop executable is missing.'}
$checks=@([pscustomobject]@{File='assets.rl';Sha256=(Get-FileHash -LiteralPath $archive).Hash})
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
