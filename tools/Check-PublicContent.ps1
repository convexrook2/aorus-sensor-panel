$ErrorActionPreference='Stop'
$projectRoot=Split-Path $PSScriptRoot -Parent
Push-Location -LiteralPath $projectRoot
try {
    $paths=@(git ls-files --cached --others --exclude-standard)
    if($LASTEXITCODE -ne 0){throw 'Cannot enumerate repository files.'}
    $findings=@()
    foreach($path in $paths|Select-Object -Unique){
        if(-not(Test-Path -LiteralPath $path -PathType Leaf)){continue}
        if([IO.Path]::GetExtension($path) -notin @('.md','.txt','.cs','.ps1','.cmd','.json','.xaml','.csproj','.ini','.inc')){continue}
        $content=[IO.File]::ReadAllText((Join-Path $projectRoot $path))
        if($content -match '(?i)[A-Z]:[\\/]+Users[\\/]+[A-Za-z0-9_. -]+[\\/]'){
            $findings+=$path
        }
    }
    if($findings.Count){throw ('Account-specific profile paths found in: '+($findings -join ', '))}
    Write-Host 'PASS: public text files contain no account-specific Windows profile paths.'
}
finally { Pop-Location }
