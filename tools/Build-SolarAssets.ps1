param([switch]$Restore)
$ErrorActionPreference='Stop'
$taskRoot=Split-Path $PSScriptRoot -Parent
Push-Location -LiteralPath $taskRoot
try {
    New-Item -ItemType Directory -Path 'Assets/SolarNavigation/textures','Assets/SolarNavigation/models' -Force | Out-Null
    if($Restore -or -not (Test-Path -LiteralPath 'NavigationWeb/node_modules/tsx/dist/cli.mjs')) {
        Push-Location -LiteralPath 'NavigationWeb'
        try { npm ci --ignore-scripts --no-audit --no-fund; if($LASTEXITCODE -ne 0){throw 'Asset exporter dependency restore failed.'} }
        finally { Pop-Location }
    }
    $taskTextures=@('sun.jpg','mercury.jpg','venus.jpg','earth.jpg','earth_clouds.jpg','moon.jpg','mars.jpg','ceres.jpg','jupiter.jpg','io.jpg','ganymede.jpg','callisto.jpg','saturn.jpg','saturn_ring.png','uranus.jpg','neptune.jpg')
    foreach($taskFile in $taskTextures) {
        Copy-Item -LiteralPath ('NavigationWeb/public/textures/'+$taskFile) -Destination ('Assets/SolarNavigation/textures/'+$taskFile)
    }
    foreach($taskFile in @('eros.fnm','phoebe.fnm')) {
        Copy-Item -LiteralPath ('NavigationWeb/public/models/'+$taskFile) -Destination ('Assets/SolarNavigation/models/'+$taskFile)
    }
    node NavigationWeb/node_modules/tsx/dist/cli.mjs NavigationWeb/tools/export-native-landmarks.ts
    if($LASTEXITCODE -ne 0){throw 'Native landmark asset export failed.'}
    Copy-Item -LiteralPath 'NavigationWeb/Native-Schematic-Credits.txt' -Destination 'Assets/SolarNavigation/CREDITS.txt'
    $taskFiles=@($taskTextures | ForEach-Object {'textures/'+$_}) + @('models/eros.fnm','models/phoebe.fnm','models/tycho.fnm','models/medina.fnm','models/solgate.fnm')
    $taskManifest=@($taskFiles | ForEach-Object {
        $taskPath=Join-Path $taskRoot ('Assets/SolarNavigation/'+$_)
        [pscustomobject]@{Path=$_;Bytes=(Get-Item -LiteralPath $taskPath).Length;Sha256=(Get-FileHash -LiteralPath $taskPath -Algorithm SHA256).Hash}
    })
    [pscustomobject]@{Renderer='Original native SOL / NAVIGATION schematic';BelterCommit='ba86075477fc529294a5e51cb4e1b0d183ff893a';Files=$taskManifest} | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath 'artifacts/native-solar-assets.json' -Encoding utf8
    Write-Host ('READY: '+$taskManifest.Count+' native texture/mesh assets; original web adaptation preserved in source.')
} finally { Pop-Location }
