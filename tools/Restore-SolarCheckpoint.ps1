param([Parameter(Mandatory)][string]$Checkpoint,[ValidateSet('Verify','Source','Installed')][string]$Scope='Verify')
$ErrorActionPreference='Stop'
$taskCheckpoint=(Resolve-Path -LiteralPath $Checkpoint).Path
$taskReceipt=Get-Content -LiteralPath (Join-Path $taskCheckpoint 'checkpoint.json') -Raw | ConvertFrom-Json
function Confirm-Backup([string]$Folder,[string]$Manifest){
    $taskFiles=Get-Content -LiteralPath (Join-Path $taskCheckpoint $Manifest) -Raw | ConvertFrom-Json
    foreach($taskFile in $taskFiles){
        $taskPath=Join-Path (Join-Path $taskCheckpoint $Folder) $taskFile.Path
        if((Get-FileHash -LiteralPath $taskPath -Algorithm SHA256).Hash -ne $taskFile.SHA256){throw ('Backup hash mismatch: '+$taskFile.Path)}
    }
    Write-Host ('Verified '+$taskFiles.Count+' files in '+$Folder)
}
Confirm-Backup 'source-now' 'source-sha256.json'
Confirm-Backup 'installed-now' 'installed-sha256.json'
if((Get-FileHash -LiteralPath (Join-Path $taskCheckpoint 'Setup-NOW-v1.8.3.exe')).Hash -ne $taskReceipt.OriginalInstallerSHA256){throw 'Original installer changed.'}
if($Scope -eq 'Verify'){Write-Host 'NOW checkpoint is intact; no running files or settings changed.';exit 0}
$taskManifest=Join-Path $taskCheckpoint ($Scope.ToLowerInvariant()+'-changes.json')
if(-not(Test-Path -LiteralPath $taskManifest)){throw 'No changes to restore for this scope.'}
$taskChanges=Get-Content -LiteralPath $taskManifest -Raw | ConvertFrom-Json
$taskTarget=if($Scope -eq 'Source'){$taskReceipt.Workspace}else{$taskReceipt.InstalledRoot}
$taskTarget=[IO.Path]::GetFullPath($taskTarget).TrimEnd('\')+'\'
foreach($taskChange in $taskChanges){
    $taskPath=[IO.Path]::GetFullPath((Join-Path $taskTarget $taskChange.Path))
    if(-not $taskPath.StartsWith($taskTarget,[StringComparison]::OrdinalIgnoreCase)){throw 'Rollback target escaped the checkpoint root.'}
    if(Test-Path -LiteralPath $taskPath){
        $taskCurrent=(Get-FileHash -LiteralPath $taskPath).Hash
        $taskKnownHashes=@($taskChange.AfterSHA256,$taskChange.BeforeSHA256)+@($taskChange.AllowedAfterSHA256)
        if($taskCurrent -notin $taskKnownHashes){throw ('Later work is protected; rollback stopped at '+$taskChange.Path)}
    }
}
if($Scope -eq 'Installed'){
    $taskAdmin=([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
    if(-not $taskAdmin){
        $taskArgs=@('-NoProfile','-ExecutionPolicy','Bypass','-File',('"'+$PSCommandPath+'"'),'-Checkpoint',('"'+$taskCheckpoint+'"'),'-Scope','Installed')
        $taskElevated=Start-Process powershell.exe -ArgumentList $taskArgs -Verb RunAs -WindowStyle Normal -PassThru -Wait
        exit $taskElevated.ExitCode
    }
    foreach($taskRecord in @(Get-CimInstance Win32_Process -Filter "Name='SensorPanel.exe'" | Where-Object ExecutablePath -eq (Join-Path $taskTarget 'SensorPanel.exe'))){
        $taskProcess=Get-Process -Id $taskRecord.ProcessId
        $taskHelpers=@(Get-CimInstance Win32_Process | Where-Object {$_.ParentProcessId -eq $taskRecord.ProcessId -and $_.ExecutablePath -eq (Join-Path $taskTarget 'PresentMon-2.6.0-x890-compat-x64.exe')})
        if(-not $taskProcess.CloseMainWindow() -or -not $taskProcess.WaitForExit(6000)){Stop-Process -Id $taskRecord.ProcessId -Force}
        foreach($taskHelper in $taskHelpers){
            if($taskHelper.CommandLine -match '--session_name\s+(X890Panel-\d+-[a-f0-9]+)'){
                & $taskHelper.ExecutablePath --session_name $Matches[1] --terminate_existing_session | Out-Host
            }
            $taskHelperProcess=Get-Process -Id $taskHelper.ProcessId -ErrorAction SilentlyContinue
            if($taskHelperProcess -and -not $taskHelperProcess.WaitForExit(2000)){Stop-Process -Id $taskHelper.ProcessId -Force}
        }
    }
}
$taskBackupFolder=if($Scope -eq 'Source'){'source-now'}else{'installed-now'}
foreach($taskChange in $taskChanges){
    $taskPath=Join-Path $taskTarget $taskChange.Path
    if($taskChange.BeforeSHA256){
        New-Item -ItemType Directory -Path (Split-Path $taskPath -Parent) -Force | Out-Null
        Copy-Item -LiteralPath (Join-Path (Join-Path $taskCheckpoint $taskBackupFolder) $taskChange.Path) -Destination $taskPath -Force
        if((Get-FileHash -LiteralPath $taskPath).Hash -ne $taskChange.BeforeSHA256){throw ('Restore verification failed: '+$taskChange.Path)}
    }elseif(Test-Path -LiteralPath $taskPath){Remove-Item -LiteralPath $taskPath -Force}
}
if($Scope -eq 'Installed'){
    $taskSid=[Security.Principal.WindowsIdentity]::GetCurrent().User.Value
    $taskScheduler=New-Object -ComObject 'Schedule.Service'; $taskScheduler.Connect()
    try{
        $taskStartup=$taskScheduler.GetFolder('\').GetTask('AORUS-FAN-MADE-1B7C308F-'+$taskSid)
        if($taskStartup.Definition.Actions.Item(1).Path -ne (Join-Path $taskTarget 'SensorPanel.exe')){throw 'Startup task points elsewhere.'}
        $null=$taskStartup.Run($null)
    }catch{Start-Process -FilePath (Join-Path $taskTarget 'SensorPanel.exe') -WorkingDirectory $taskTarget -WindowStyle Normal | Out-Null}
}
Write-Host ('Restored '+$Scope+' to '+$taskReceipt.Created+'. Other files, Git index, user media and preferences were preserved.')
