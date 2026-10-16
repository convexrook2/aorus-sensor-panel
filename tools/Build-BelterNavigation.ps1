param([switch]$Restore)
$ErrorActionPreference='Stop'
$taskRoot=Split-Path $PSScriptRoot -Parent
Push-Location -LiteralPath (Join-Path $taskRoot 'NavigationWeb')
try {
    foreach($taskTexture in Get-ChildItem -LiteralPath 'public/textures' -File) {
        $taskHeader=[IO.File]::ReadAllBytes($taskTexture.FullName)
        if($taskTexture.Extension -eq '.jpg' -and ($taskHeader[0] -ne 255 -or $taskHeader[1] -ne 216)) {throw ('Invalid JPEG asset: '+$taskTexture.Name)}
    }
    if($Restore -or -not (Test-Path -LiteralPath 'node_modules/three/package.json')) {
        npm ci --ignore-scripts --no-audit --no-fund
        if($LASTEXITCODE -ne 0){throw 'Navigation dependency restore failed.'}
    }
    node node_modules/typescript/bin/tsc --noEmit
    if($LASTEXITCODE -ne 0){throw 'Navigation TypeScript check failed.'}
    node node_modules/vite/bin/vite.js build
    if($LASTEXITCODE -ne 0){throw 'Local navigation bundle failed.'}
    $taskOutput=Join-Path $taskRoot 'Assets/SolarNavigation'
    Copy-Item -LiteralPath 'vendor/belter-charts/LICENSE' -Destination (Join-Path $taskOutput 'Belter-Charts-LICENSE.txt')
    Copy-Item -LiteralPath 'vendor/belter-charts/CREDITS.md' -Destination (Join-Path $taskOutput 'Belter-Charts-Upstream-Credits.md')
    foreach($taskPackage in @('three','astronomy-engine','zustand')) {
        $taskLicense=Get-ChildItem -LiteralPath (Join-Path 'node_modules' $taskPackage) -File | Where-Object Name -match '^(LICENSE|COPYING)(\.|$)' | Select-Object -First 1
        if($taskLicense){Copy-Item -LiteralPath $taskLicense.FullName -Destination (Join-Path $taskOutput ($taskPackage+'-LICENSE.txt'))}
        elseif($taskPackage -eq 'astronomy-engine') { Copy-Item -LiteralPath 'licenses/astronomy-engine-LICENSE.txt' -Destination (Join-Path $taskOutput 'astronomy-engine-LICENSE.txt') }
        else {throw ('Missing dependency licence: '+$taskPackage)}
    }
    Copy-Item -LiteralPath 'CREDITS.txt' -Destination (Join-Path $taskOutput 'CREDITS.txt')
    $taskFiles=@(Get-ChildItem -LiteralPath $taskOutput -Recurse -File | ForEach-Object {
        [pscustomobject]@{Path=$_.FullName.Substring($taskOutput.Length+1);Bytes=$_.Length;Sha256=(Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash}
    })
    [pscustomobject]@{Upstream='skylercole/belter-charts';Commit='ba86075477fc529294a5e51cb4e1b0d183ff893a';BuiltAtUtc=(Get-Date).ToUniversalTime().ToString('o');Files=$taskFiles} | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $taskRoot 'artifacts/belter-bundle-manifest.json') -Encoding utf8
    Write-Host ('PASS: local Belter Charts bundle, '+$taskFiles.Count+' files with notices.')
}
finally { Pop-Location }
