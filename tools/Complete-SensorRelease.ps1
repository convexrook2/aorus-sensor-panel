param(
    [Parameter(Mandatory)][int]$BuildNumber,
    [string]$ExpectedSha,
    [string]$JenkinsHome = 'C:\Jenkins\home'
)
$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path $PSScriptRoot -Parent
Set-Location -LiteralPath $repoRoot
if (!$ExpectedSha) {
    $ExpectedSha = (git rev-parse HEAD).Trim()
    if ($LASTEXITCODE -ne 0) { throw 'Cannot identify the release commit.' }
}
if ($ExpectedSha -notmatch '^[0-9a-f]{40}$' -or $BuildNumber -lt 1) { throw 'A full pushed source SHA and build number are required.' }
$version = ([xml](Get-Content SensorPanel/SensorPanel.csproj -Raw)).Project.PropertyGroup.Version
$releaseRoot = Join-Path $repoRoot "artifacts\release-$version"
New-Item -ItemType Directory -Path $releaseRoot -Force | Out-Null
Start-Transcript -LiteralPath (Join-Path $releaseRoot 'release-flow.txt') -Append
try {
    Write-Host "Sensor panel release $version, exact SHA $ExpectedSha, Jenkins X890-CI #$BuildNumber."
    Write-Host 'Waiting for the authorized Jenkins build; its console is mirrored here. Fleet Studio will not be launched, installed or deployed.'
    $buildRoot = Join-Path $JenkinsHome "jobs\X890-CI\builds\$BuildNumber"
    $deadline = [datetime]::UtcNow.AddMinutes(50)
    $offset = 0
    while ($true) {
        $log = Join-Path $buildRoot 'log'
        if (Test-Path -LiteralPath $log) {
            $stream = [IO.File]::Open($log,[IO.FileMode]::Open,[IO.FileAccess]::Read,[IO.FileShare]::ReadWrite)
            try { $reader = [IO.StreamReader]::new($stream); $content = $reader.ReadToEnd() } finally { $stream.Dispose() }
            if ($content.Length -gt $offset) { Write-Host $content.Substring($offset); $offset = $content.Length }
        }
        $buildFile = Join-Path $buildRoot 'build.xml'
        if (Test-Path -LiteralPath $buildFile) {
            # Jenkins uses XML 1.1, which System.Xml does not support. Read only
            # its plain result element; do not parse or execute the document.
            $buildText = Get-Content -LiteralPath $buildFile -Raw
            $resultMatch = [regex]::Match($buildText,'<result>(SUCCESS|FAILURE|ABORTED|UNSTABLE|NOT_BUILT)</result>')
            if ($resultMatch.Success -and $buildText.Contains('<completed>true</completed>')) {
                if ($resultMatch.Groups[1].Value -ne 'SUCCESS') { throw "Jenkins #$BuildNumber finished: $($resultMatch.Groups[1].Value). Nothing published or installed." }
                break
            }
        }
        if ([datetime]::UtcNow -gt $deadline) { throw 'Jenkins wait timed out. No publication or installation performed.' }
        Start-Sleep -Seconds 3
    }
    $archive = Join-Path $buildRoot 'archive\artifacts'
    $identity = (Get-Content -LiteralPath (Join-Path $archive 'ci\source-identity.txt') -Raw) -split '\r?\n'
    foreach ($line in @("EXPECTED_SHA=$ExpectedSha","JENKINS_CHECKOUT_SHA=$ExpectedSha","ACTUAL_CHECKED_OUT_SHA=$ExpectedSha",'RESULT=SUCCESS')) {
        if ($identity -notcontains $line) { throw "CI identity mismatch: $line" }
    }
    $manifest = Get-Content -LiteralPath (Join-Path $archive 'installer\package-manifest.json') -Raw | ConvertFrom-Json
    if ($manifest.Version -ne $version -or $manifest.SourceHead -ne $ExpectedSha -or @($manifest.SourceStatus).Count) { throw 'CI installer source/version is not the clean requested revision.' }
    $installerRoot = Join-Path $repoRoot 'artifacts\installer'
    New-Item -ItemType Directory -Path $installerRoot -Force | Out-Null
    $backup = Join-Path $releaseRoot ('previous-installer-'+(Get-Date -Format 'yyyyMMdd-HHmmss'))
    New-Item -ItemType Directory -Path $backup | Out-Null
    Get-ChildItem -LiteralPath $installerRoot -File | Copy-Item -Destination $backup
    foreach ($name in @('AORUS-Sensor-Panel-FAN-MADE-Setup.exe','SHA256SUMS.txt','package-manifest.json')) {
        Copy-Item -LiteralPath (Join-Path $archive ('installer\'+$name)) -Destination $installerRoot -Force
    }
    $readme = git show "${ExpectedSha}:Packaging/README.txt"
    if ($LASTEXITCODE -ne 0) { throw 'Cannot read release README from committed source.' }
    $readme | Set-Content -LiteralPath (Join-Path $installerRoot 'README.txt') -Encoding utf8
    $installer = Join-Path $installerRoot 'AORUS-Sensor-Panel-FAN-MADE-Setup.exe'
    if ((Get-FileHash -LiteralPath $installer).Hash -ne $manifest.InstallerSHA256) { throw 'Installer hash differs from CI.' }
    foreach ($file in $manifest.Files) {
        if ([IO.Path]::IsPathRooted($file.Path) -or $file.Path -match '(^|[\\/])\.\.([\\/]|$)') { throw 'Unsafe installed-file manifest path.' }
    }
    $sums = foreach ($name in @('AORUS-Sensor-Panel-FAN-MADE-Setup.exe','README.txt')) {
        (Get-FileHash -LiteralPath (Join-Path $installerRoot $name)).Hash.ToLowerInvariant()+'  '+$name
    }
    $sums | Set-Content -LiteralPath (Join-Path $installerRoot 'SHA256SUMS.txt') -Encoding ascii
    $tag = "v$version"
    $existingTag = git ls-remote --tags origin "refs/tags/$tag"
    if ($LASTEXITCODE -ne 0) { throw 'Cannot inspect the remote release tag.' }
    if ($existingTag) { throw "Remote tag $tag already exists; inspect its source before publication." }
    gh release view $tag --json tagName 2>$null | Out-Null
    if ($LASTEXITCODE -eq 0) { throw "Release $tag already exists; inspect it before retrying publication." }
    $releaseNotes = git show "${ExpectedSha}:Packaging/ReleaseNotes-$version.md"
    if ($LASTEXITCODE -ne 0) { throw 'Cannot read committed release notes.' }
    $notesPath = Join-Path $releaseRoot 'published-release-notes.md'
    $releaseNotes | Set-Content -LiteralPath $notesPath -Encoding utf8
    Write-Host "Publishing $tag from successful Jenkins #$BuildNumber / $ExpectedSha."
    gh release create $tag --target $ExpectedSha --title "AORUS Sensor Panel FAN MADE $tag - Anubis and fleet lighting" --notes-file $notesPath (Join-Path $installerRoot 'AORUS-Sensor-Panel-FAN-MADE-Setup.exe') (Join-Path $installerRoot 'README.txt') (Join-Path $installerRoot 'SHA256SUMS.txt')
    if ($LASTEXITCODE -ne 0) { throw 'GitHub release publication failed; installation has not started.' }
    $published = gh release view $tag --json url,assets | ConvertFrom-Json
    if ($LASTEXITCODE -ne 0) { throw 'Cannot verify published release.' }
    $publishedTag = git ls-remote --tags origin "refs/tags/$tag"
    if ($LASTEXITCODE -ne 0 -or ($publishedTag -split '\s+')[0] -ne $ExpectedSha) { throw 'Published release tag differs from the successful CI revision.' }
    $downloadRoot = Join-Path $releaseRoot 'public-download'
    New-Item -ItemType Directory -Path $downloadRoot -Force | Out-Null
    gh release download $tag --dir $downloadRoot --clobber
    if ($LASTEXITCODE -ne 0) { throw 'Cannot verify the public release download.' }
    foreach ($name in @('AORUS-Sensor-Panel-FAN-MADE-Setup.exe','README.txt','SHA256SUMS.txt')) {
        if ((Get-FileHash -LiteralPath (Join-Path $downloadRoot $name)).Hash -ne (Get-FileHash -LiteralPath (Join-Path $installerRoot $name)).Hash) { throw "Public download differs: $name" }
    }
    $registryPath = 'HKLM:\Software\Microsoft\Windows\CurrentVersion\Uninstall\{1B7C308F-694A-4C92-8C95-218009316F78}_is1'
    $installed = Get-ItemProperty -LiteralPath $registryPath
    $panelRoot = [IO.Path]::GetFullPath($installed.InstallLocation).TrimEnd('\')
    if ($panelRoot -ne (Join-Path $env:ProgramFiles 'AORUS Sensor Panel FAN MADE')) { throw 'Unexpected production installation folder; preserve it.' }
    $panelExe = Join-Path $panelRoot 'SensorPanel.exe'
    $running = @(Get-CimInstance Win32_Process | Where-Object { $_.ExecutablePath -in @($panelExe,(Join-Path $panelRoot 'SensorCollector.exe')) })
    if ($running.Count) { throw 'Exit the installed sensor panel before upgrading; published CI installer is ready.' }
    $selectedTasks = [string]$installed.'Inno Setup: Selected Tasks'
    if ($selectedTasks -notmatch '^(desktopicon|startup)?(,(desktopicon|startup))*$') { throw 'Unexpected installer task choice; inspect it before upgrading.' }
    $scheduler = New-Object -ComObject Schedule.Service; $scheduler.Connect()
    $taskName = 'AORUS-FAN-MADE-1B7C308F-'+[Security.Principal.WindowsIdentity]::GetCurrent().User.Value
    $taskBefore = $null
    try { $taskBefore = $scheduler.GetFolder('\').GetTask($taskName) } catch { if ($_.Exception.HResult -ne -2147024894) { throw } }
    $startupBefore = if ($taskBefore) { [bool]$taskBefore.Enabled } else { $false }
    if (($selectedTasks -split ',') -contains 'startup' -and !$startupBefore) { throw 'Saved startup choice and actual task disagree; preserve current state.' }
    if ($startupBefore -and ($selectedTasks -split ',') -notcontains 'startup') { throw 'Actual startup and saved installer choice disagree; preserve current state.' }
    if ($taskBefore) { $taskBefore.Xml | Set-Content -LiteralPath (Join-Path $releaseRoot 'startup-before.xml') }
    $installed | Select-Object DisplayVersion,InstallLocation,'Inno Setup: Selected Tasks' | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $releaseRoot 'installed-before.json')
    Write-Host "Installing verified sensor panel $version; preserving tasks '$selectedTasks'. Approve Windows administrator prompt if shown."
    $arguments = '/SILENT /SP- /NORESTART /ALLUSERS /DIR="'+$panelRoot+'" /TASKS="'+$selectedTasks+'" /LOG="'+(Join-Path $installerRoot 'production-install.log')+'"'
    $setup = Start-Process -FilePath $installer -Verb RunAs -ArgumentList $arguments -WindowStyle Normal -Wait -PassThru
    if ($setup.ExitCode -ne 0) { throw "Installer failed: $($setup.ExitCode)" }
    $verified = 0
    foreach ($file in $manifest.Files) {
        $target = [IO.Path]::GetFullPath((Join-Path $panelRoot $file.Path))
        if (!$target.StartsWith($panelRoot+'\',[StringComparison]::OrdinalIgnoreCase) -or (Get-FileHash -LiteralPath $target).Hash -ne $file.SHA256) { throw "Installed package differs: $($file.Path)" }
        $verified++
    }
    $after = Get-ItemProperty -LiteralPath $registryPath
    if ($after.DisplayVersion -ne $version -or $after.'Inno Setup: Selected Tasks' -ne $selectedTasks) { throw 'Installed version or selected tasks changed unexpectedly.' }
    $startupAfter = $false
    try { $taskAfter = $scheduler.GetFolder('\').GetTask($taskName); $startupAfter = [bool]$taskAfter.Enabled } catch { if ($_.Exception.HResult -ne -2147024894) { throw } }
    if ($startupAfter -ne $startupBefore) { throw 'Startup enabled state changed unexpectedly.' }
    if ($taskAfter) { $taskAfter.Xml | Set-Content -LiteralPath (Join-Path $releaseRoot 'startup-after.xml') }
    $receipt = [pscustomobject]@{Version=$version;SourceHead=$ExpectedSha;CiBuild=$BuildNumber;CiResult='SUCCESS';InstallerSHA256=$manifest.InstallerSHA256;InstallerExitCode=$setup.ExitCode;MonitorInstalled=$true;MonitorFilesVerified=$verified;MonitorStartupPreserved=$true;PublicUrl=$published.url;PublicDownloadVerified=$true;FleetStudioInstalled=$false;FleetStudioDeployed=$false;NativeRenderingVerified=$false;LiveHardwareRetested=$false;CompletedAt=[datetimeoffset]::Now}
    $receipt | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $releaseRoot 'result.json')
    $receipt | Format-List | Out-Host
} finally { Stop-Transcript }
