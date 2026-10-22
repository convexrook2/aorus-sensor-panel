[CmdletBinding()]
param([switch]$PreflightOnly)
$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$repoRoot=Split-Path $PSScriptRoot -Parent
Set-Location -LiteralPath $repoRoot
. (Join-Path $PSScriptRoot 'FleetAssetSnapshot.ps1')
if($env:JENKINS_URL){
    $initialChanges=@(git status --porcelain)
    if($LASTEXITCODE -ne 0){throw 'Cannot verify initial Jenkins source state.'}
    Assert-CleanCiSource $initialChanges
}
$assetBaseline=Get-FleetAssetSnapshot $repoRoot
$ciRoot=Join-Path $repoRoot 'artifacts\ci'
New-Item -ItemType Directory -Path $ciRoot -Force | Out-Null
foreach($name in @('gate-tests.xml','navigation-tests.xml','jenkins-checks.json')){
    $generated=[IO.Path]::GetFullPath((Join-Path $ciRoot $name))
    if(!$generated.StartsWith([IO.Path]::GetFullPath($ciRoot)+'\',[StringComparison]::OrdinalIgnoreCase)){throw 'Invalid generated report path.'}
    if(Test-Path -LiteralPath $generated){Remove-Item -LiteralPath $generated}
}
$results=[Collections.Generic.List[object]]::new()
$passed=$false
$assetGuardChecked=$false
$pythonExecutable='python'
$commandLog=Join-Path $ciRoot 'commands.txt'
Set-Content -LiteralPath $commandLog -Value ''
function Invoke-Gate([string]$Name,[scriptblock]$Action){
    Write-Host "`n== $Name ==" -ForegroundColor Cyan
    $timer=[Diagnostics.Stopwatch]::StartNew();$failure=$null
    try{& $Action}catch{$failure=$_.Exception.Message;throw}
    finally{$results.Add([pscustomobject]@{Name=$Name;Seconds=$timer.Elapsed.TotalSeconds;Failure=$failure})}
}
function Invoke-Checked([string]$Command,[string[]]$Arguments){
    Write-Host ('+ '+$Command+' '+($Arguments -join ' '))
    & $Command @Arguments 2>&1 | Tee-Object -FilePath $commandLog -Append | Out-Host
    if($LASTEXITCODE -ne 0){throw "Command failed ($LASTEXITCODE): $Command $($Arguments -join ' ')"}
}
function Read-JsonReport([string]$Command,[string[]]$Arguments,[string]$Report,[int[]]$AllowedExitCodes=@(0)){
    $raw=(& $Command @Arguments 2>&1 | Out-String).Trim();$exitCode=$LASTEXITCODE
    $raw | Set-Content -LiteralPath (Join-Path $ciRoot $Report)
    if($exitCode -notin $AllowedExitCodes -or !$raw){throw "Dependency report failed ($exitCode): $Report"}
    return ($raw | ConvertFrom-Json)
}
try{
    Invoke-Gate 'Windows toolchain preflight' {
        if($env:OS -ne 'Windows_NT' -or $PSVersionTable.PSVersion.Major -lt 7){throw 'Windows x64 and PowerShell 7+ are required.'}
        foreach($tool in @('git','dotnet','node','npm.cmd','pwsh.exe')){
            if(!(Get-Command $tool -ErrorAction SilentlyContinue)){throw "Missing CI prerequisite: $tool"}
        }
        $python=Get-Command python -ErrorAction SilentlyContinue
        if($python){$script:pythonExecutable=$python.Source}
        elseif($env:JENKINS_HOME){
            # Per-job toolchain metadata lives outside checkout/deletion and Git.
            # It points to an existing interpreter; no install or global PATH edit.
            $configPath=Join-Path $env:JENKINS_HOME 'X890-CI-toolchain.json'
            if(!(Test-Path -LiteralPath $configPath)){throw 'Python is missing from agent PATH; configure X890-CI-toolchain.json.'}
            $config=Get-Content -LiteralPath $configPath -Raw | ConvertFrom-Json
            $script:pythonExecutable=[IO.Path]::GetFullPath($config.PythonExecutable)
            if(!(Test-Path -LiteralPath $script:pythonExecutable -PathType Leaf)){throw 'Configured X890 Python interpreter is missing.'}
        }else{throw 'Python is unavailable.'}
        Invoke-Checked dotnet @('--version')
        if(!((& dotnet --list-sdks) -match '^10\.')){throw '.NET 10 SDK is required.'}
        Invoke-Checked node @('--version')
        Invoke-Checked $script:pythonExecutable @('--version')
        Invoke-Checked npm.cmd @('--version')
        Write-Host "PowerShell $($PSVersionTable.PSVersion)"
    }
    if($PreflightOnly){$passed=$true;return}
    Invoke-Gate 'Release build, sensor/resource contracts, authenticated assets and protected publish' {
        # A child console command keeps every native tool's output in this visible
        # terminal and the log, including output missed by PS transcription alone.
        Invoke-Checked pwsh.exe @('-NoLogo','-NoProfile','-NonInteractive','-File','.\tools\Run-CI.ps1','-PythonExecutable',$script:pythonExecutable)
        $core=Get-Content -LiteralPath (Join-Path $ciRoot 'result.json') -Raw | ConvertFrom-Json
        if(!$core.Passed){throw 'Core CI receipt did not pass.'}
    }
    Invoke-Gate 'Navigation tests, dependency audit and isolated engineering build' {
        Invoke-Checked npm.cmd @('--prefix','NavigationWeb','ci','--ignore-scripts','--no-fund','--no-audit')
        Invoke-Checked npm.cmd @('--prefix','NavigationWeb','audit','--audit-level=high','--fetch-retries=1','--fetch-timeout=20000')
        Invoke-Checked npm.cmd @('--prefix','NavigationWeb','test','--','--reporter=default','--reporter=junit',('--outputFile.junit='+(Join-Path $ciRoot 'navigation-tests.xml')))
        # The normal Vite build targets accepted assets; CI must always override
        # that output into artifacts, never regenerate the runtime visual bundle.
        $navOutput=[IO.Path]::GetFullPath((Join-Path $ciRoot 'navigation-build'))
        if(!$navOutput.StartsWith([IO.Path]::GetFullPath($ciRoot)+'\',[StringComparison]::OrdinalIgnoreCase)){throw 'Unsafe navigation output.'}
        Invoke-Checked npm.cmd @('--prefix','NavigationWeb','run','build','--','--outDir',$navOutput)
        $report=Join-Path $ciRoot 'navigation-tests.xml'
        if(!(Test-Path -LiteralPath $report)){throw 'Navigation test report missing.'}
        [xml]$tests=Get-Content -LiteralPath $report -Raw
        if([int]$tests.testsuites.tests -lt 1){throw 'Navigation tests unexpectedly discovered zero tests.'}
    }
    Invoke-Gate 'NuGet and npm dependency freshness' {
        $nuget=Read-JsonReport dotnet @('list','.\X890.slnx','package','--outdated','--format','json','--output-version','1','--no-restore') 'nuget-outdated.json'
        if($nuget.PSObject.Properties.Name -notcontains 'projects'){throw 'NuGet freshness report lacks projects.'}
        $updates=@(foreach($project in $nuget.projects){
            if($project.PSObject.Properties.Name -notcontains 'frameworks'){continue}
            foreach($framework in $project.frameworks){
                if($framework.PSObject.Properties.Name -contains 'topLevelPackages'){$framework.topLevelPackages}
            }
        })
        if($updates.Count){throw ('Outdated NuGet packages: '+(($updates | ForEach-Object {"$($_.id): $($_.resolvedVersion) -> $($_.latestVersion)"}) -join '; '))}
        $npm=Read-JsonReport npm.cmd @('--prefix','NavigationWeb','outdated','--json') 'npm-outdated.json' @(0,1)
        $compatible=@($npm.PSObject.Properties | Where-Object {$_.Value.current -ne $_.Value.wanted})
        $migrations=@($npm.PSObject.Properties | Where-Object {$_.Value.wanted -ne $_.Value.latest} | ForEach-Object {
            [pscustomobject]@{Package=$_.Name;Current=$_.Value.current;Compatible=$_.Value.wanted;Latest=$_.Value.latest;Status='Separate compatibility-range migration review'}
        })
        ConvertTo-Json -InputObject $migrations -Depth 4 | Set-Content -LiteralPath (Join-Path $ciRoot 'dependency-migrations.json')
        foreach($migration in $migrations){Write-Host "REVIEW: $($migration.Package) compatibility-range migration $($migration.Compatible) -> $($migration.Latest)"}
        if($compatible.Count){throw ('Compatible navigation updates required: '+(($compatible | ForEach-Object {"$($_.Name): $($_.Value.current) -> $($_.Value.wanted)"}) -join '; '))}
    }
    Invoke-Gate 'Accepted asset inputs remain unchanged during checks' {
        Assert-FleetAssetSnapshot $assetBaseline (Get-FleetAssetSnapshot $repoRoot)
        $script:assetGuardChecked=$true
        if($env:JENKINS_URL){
            $changes=@(git status --porcelain)
            if($LASTEXITCODE -ne 0){throw 'Cannot verify final Jenkins source state.'}
            Assert-CleanCiSource $changes
        }
    }
    $passed=$true
    Write-Host 'PASS: X890 Jenkins checks. No installer compiled or executed; no live sensors or release publication.'
}finally{
    # A failed network or build gate must not prevent preservation accounting.
    if(!$assetGuardChecked){
        $assetTimer=[Diagnostics.Stopwatch]::StartNew();$assetFailure=$null
        try{Assert-FleetAssetSnapshot $assetBaseline (Get-FleetAssetSnapshot $repoRoot);$assetGuardChecked=$true}
        catch{$assetFailure=$_.Exception.Message;$passed=$false;Write-Warning $assetFailure}
        $results.Add([pscustomobject]@{Name='Asset input immutability after incomplete checks';Seconds=$assetTimer.Elapsed.TotalSeconds;Failure=$assetFailure})
    }
    # These are aggregate check groups; navigation's report contains its actual
    # individual test cases. Do not invent assertion counts for console checks.
    $xml=[xml]'<testsuites><testsuite name="X890 CI gates" /></testsuites>'
    $suite=$xml.testsuites.testsuite
    $suite.SetAttribute('tests',[string]$results.Count)
    $suite.SetAttribute('failures',[string]@($results | Where-Object Failure).Count)
    foreach($gate in $results){
        $case=$xml.CreateElement('testcase')
        $case.SetAttribute('name',$gate.Name);$case.SetAttribute('classname','X890.CI.Gates')
        $case.SetAttribute('time',$gate.Seconds.ToString('0.000',[Globalization.CultureInfo]::InvariantCulture))
        if($gate.Failure){$failure=$xml.CreateElement('failure');$failure.SetAttribute('message',$gate.Failure);$case.AppendChild($failure) | Out-Null}
        $suite.AppendChild($case) | Out-Null
    }
    $xml.Save((Join-Path $ciRoot 'gate-tests.xml'))
    [pscustomobject]@{Passed=$passed;CompletedAt=[datetimeoffset]::Now;PreflightOnly=[bool]$PreflightOnly;Groups=$results;AssetInputs=$assetBaseline.Count;AssetInputsUnchanged=$assetGuardChecked;ExactCommitCI=[bool]$env:JENKINS_URL;Installed=$false;LiveSensorsTested=$false} |
        ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $ciRoot 'jenkins-checks.json')
}
