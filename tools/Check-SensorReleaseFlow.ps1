$ErrorActionPreference='Stop'
$repoRoot=Split-Path $PSScriptRoot -Parent
$suiteRoot=Join-Path $repoRoot ('artifacts\release-flow-check-'+[guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $suiteRoot | Out-Null
$scriptSource=Join-Path $PSScriptRoot 'Complete-SensorRelease.ps1'
$script:cases=[Collections.Generic.List[object]]::new()

# All GitHub, Git and native installation routes below are test doubles. The
# actual release script runs against an isolated fixture copied under artifacts.
function global:git {
    $global:LASTEXITCODE=0
    if($args[0] -eq 'show') {
        $relative=([string]$args[1]).Substring(41)
        if($relative -eq 'SensorPanel/SensorPanel.csproj'){return '<Project><PropertyGroup><Version>1.8.11</Version></PropertyGroup></Project>'}
        if($relative -eq 'Packaging/README.txt'){return 'Version 1.8.11 | Windows x64'}
        if($relative -eq 'Packaging/ReleaseNotes-1.8.11.md'){
            if($global:releaseFixture.DraftNotes){return '**DRAFT: not approved**'}
            return '# v1.8.11 - Reviewed fixture notes'
        }
        throw "Unexpected fixture git show: $relative"
    }
    if($args[0] -eq 'ls-remote'){
        if($global:releaseFixture.Published){return ($global:releaseFixture.Sha+"`trefs/tags/v1.8.11")}
        return
    }
    throw 'Unexpected fixture Git operation.'
}
function global:gh {
    $global:LASTEXITCODE=0
    $global:releaseFixture.GitHubCalls++
    if($args[0] -ne 'release'){throw 'Unexpected fixture GitHub operation.'}
    switch($args[1]) {
        'view' {
            if(!$global:releaseFixture.Published){$global:LASTEXITCODE=1;return}
            return '{"url":"https://example.invalid/releases/v1.8.11","assets":[]}'
        }
        'create' {$global:releaseFixture.Published=$true;$global:releaseFixture.Creates++;return}
        'download' {
            $position=[array]::IndexOf($args,'--dir')
            if($position -lt 0){throw 'Missing fixture download destination.'}
            foreach($name in @('AORUS-Sensor-Panel-FAN-MADE-Setup.exe','README.txt','SHA256SUMS.txt')){
                Copy-Item -LiteralPath (Join-Path $global:releaseFixture.InstallerRoot $name) -Destination (Join-Path $args[$position+1] $name)
            }
            return
        }
        default {throw 'Unexpected fixture release operation.'}
    }
}
function global:Start-Process {$global:releaseFixture.InstallCalls++;throw 'Test refuses native process/installer execution.'}
function global:Get-ItemProperty {$global:releaseFixture.RegistryCalls++;throw 'Test refuses installed-product registry access.'}

function New-Fixture([string]$Name) {
    $root=Join-Path $suiteRoot $Name
    $build=Join-Path $root 'jenkins\jobs\X890-CI\builds\1'
    $archive=Join-Path $build 'archive\artifacts'
    foreach($folder in @('tools','artifacts\installer')){New-Item -ItemType Directory -Path (Join-Path $root $folder) -Force | Out-Null}
    foreach($folder in @('ci','installer')){New-Item -ItemType Directory -Path (Join-Path $archive $folder) -Force | Out-Null}
    Copy-Item -LiteralPath $scriptSource -Destination (Join-Path $root 'tools\Complete-SensorRelease.ps1')
    $sha='a'*40
    Set-Content -LiteralPath (Join-Path $build 'build.xml') -Value '<build><result>SUCCESS</result><completed>true</completed></build>'
    Set-Content -LiteralPath (Join-Path $build 'log') -Value 'Isolated CI fixture; not a real Jenkins build.'
    Set-Content -LiteralPath (Join-Path $archive 'ci\source-identity.txt') -Value @("EXPECTED_SHA=$sha","JENKINS_CHECKOUT_SHA=$sha","ACTUAL_CHECKED_OUT_SHA=$sha",'RESULT=SUCCESS')
    Set-Content -LiteralPath (Join-Path $archive 'installer\AORUS-Sensor-Panel-FAN-MADE-Setup.exe') -Value 'INERT TEST BYTES; NEVER EXECUTE'
    $digest=(Get-FileHash -LiteralPath (Join-Path $archive 'installer\AORUS-Sensor-Panel-FAN-MADE-Setup.exe')).Hash
    $manifest=[ordered]@{Version='1.8.11';SourceHead=$sha;SourceStatus=@();InstallerSHA256=$digest;Files=@([ordered]@{Path='SensorPanel.exe';SHA256=$digest})}
    $manifest | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $archive 'installer\package-manifest.json')
    Set-Content -LiteralPath (Join-Path $archive 'installer\SHA256SUMS.txt') -Value 'fixture'
    $global:releaseFixture=@{Root=$root;Sha=$sha;Archive=$archive;InstallerRoot=(Join-Path $root 'artifacts\installer');Published=$false;DraftNotes=$false;Creates=0;GitHubCalls=0;InstallCalls=0;RegistryCalls=0}
}
function Invoke-Fixture([string]$Phase='') {
    $parameters=@{BuildNumber=1;ExpectedSha=$global:releaseFixture.Sha;JenkinsHome=(Join-Path $global:releaseFixture.Root 'jenkins')}
    if($Phase){$parameters.Phase=$Phase}
    & (Join-Path $global:releaseFixture.Root 'tools\Complete-SensorRelease.ps1') @parameters
}
function Assert-True([bool]$Value,[string]$Message){if(!$Value){throw $Message}}
function Expect-Failure([scriptblock]$Action,[string]$Pattern){
    $failed=$false
    try{& $Action}catch{$failed=$true;if($_.Exception.Message -notmatch $Pattern){throw}}
    if(!$failed){throw "Expected rejection: $Pattern"}
}
function Case([string]$Name,[scriptblock]$Action){
    New-Fixture $Name
    & $Action
    Assert-True ($global:releaseFixture.InstallCalls -eq 0 -and $global:releaseFixture.RegistryCalls -eq 0) 'A test reached the installation boundary.'
    $script:cases.Add([pscustomobject]@{Name=$Name;Passed=$true})
    Write-Host "PASS: $Name"
}

try {
    Case 'default-review-offline' {
        Invoke-Fixture
        Assert-True ($global:releaseFixture.GitHubCalls -eq 0) 'Default review contacted GitHub.'
        Assert-True (Test-Path -LiteralPath (Join-Path $global:releaseFixture.Root 'artifacts\release-1.8.11\candidate-review.json')) 'Review receipt missing.'
    }
    Case 'wrong-ci-source' {
        $path=Join-Path $global:releaseFixture.Archive 'installer\package-manifest.json'
        $m=Get-Content -LiteralPath $path -Raw | ConvertFrom-Json;$m.SourceHead='b'*40;$m | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $path
        Expect-Failure {Invoke-Fixture} 'source/version'
        Assert-True ($global:releaseFixture.GitHubCalls -eq 0) 'Bad source contacted GitHub.'
    }
    Case 'tampered-installer' {
        Add-Content -LiteralPath (Join-Path $global:releaseFixture.Archive 'installer\AORUS-Sensor-Panel-FAN-MADE-Setup.exe') -Value 'changed'
        Expect-Failure {Invoke-Fixture} 'Installer hash differs'
    }
    Case 'draft-notes-refuse-publication' {
        $global:releaseFixture.DraftNotes=$true
        Expect-Failure {Invoke-Fixture 'Publish'} 'still draft'
        Assert-True ($global:releaseFixture.Creates -eq 0) 'Draft notes published.'
    }
    Case 'publish-does-not-install' {
        Invoke-Fixture 'Publish'
        Assert-True ($global:releaseFixture.Creates -eq 1) 'Publish phase did not create the mocked release.'
        Assert-True (Test-Path -LiteralPath (Join-Path $global:releaseFixture.Root 'artifacts\release-1.8.11\publication.json')) 'Publication receipt missing.'
    }
    Case 'install-needs-verified-publication' {
        Expect-Failure {Invoke-Fixture 'Install'} 'Cannot verify published release'
        Assert-True ($global:releaseFixture.Creates -eq 0) 'Install phase attempted publication.'
    }
    [pscustomobject]@{Passed=$true;Cases=$script:cases;MockedOnly=$true;RealGitHubCalls=$false;Installed=$false} |
        ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $suiteRoot 'result.json')
    Write-Host "PASS: $($script:cases.Count) isolated release-flow cases. No real GitHub, registry or installer action."
} finally {Set-Location -LiteralPath $repoRoot}
