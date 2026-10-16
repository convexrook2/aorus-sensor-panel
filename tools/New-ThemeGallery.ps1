param([string]$PreviewRoot = (Join-Path (Split-Path $PSScriptRoot -Parent) 'artifacts\theme-previews\1280x720'))
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$themes = @(
    @('cyber', 'CYBER NEON', 'Cyan + pink / soft colour wash'),
    @('synthwave', 'SYNTHWAVE', 'Plum + neon / horizon'),
    @('matrix', 'MATRIX TERMINAL', 'Phosphor + lime / clean surface'),
    @('carbon', 'CARBON GOLD', 'Charcoal + gold / clean surface'),
    @('crimson', 'CRIMSON', 'Coral + amber / clean surface'),
    @('nord', 'NORD AURORA', 'Frost + sage / arctic wash'),
    @('dracula', 'DRACULA', 'Lavender + pink / soft colour wash'),
    @('mocha', 'MOCHA', 'Mauve + teal / pastel wash'),
    @('dark', 'AORUS DARK', 'Your existing theme / reference')
)
$canvas = [Drawing.Bitmap]::new(1536, 1074)
$version = ([xml](Get-Content -LiteralPath (Join-Path (Split-Path $PSScriptRoot -Parent) 'SensorPanel\SensorPanel.csproj') -Raw)).Project.PropertyGroup.Version
$graphics = [Drawing.Graphics]::FromImage($canvas)
$titleFont = [Drawing.Font]::new('Segoe UI', 18, [Drawing.FontStyle]::Bold)
$nameFont = [Drawing.Font]::new('Segoe UI', 12, [Drawing.FontStyle]::Bold)
$detailFont = [Drawing.Font]::new('Segoe UI', 10)
$white = [Drawing.SolidBrush]::new([Drawing.Color]::FromArgb(235, 242, 250))
$muted = [Drawing.SolidBrush]::new([Drawing.Color]::FromArgb(158, 177, 197))
try {
    $graphics.Clear([Drawing.Color]::FromArgb(10, 15, 23))
    $graphics.InterpolationMode = [Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $graphics.DrawString('AORUS SENSOR PANEL / THEME COLLECTION', $titleFont, $white, 20, 12)
    $graphics.DrawString(('FAN MADE  |  v'+$version+'  |  Actual saved-reading panel renders; click Settings > Theme in the app.'), $detailFont, $muted, 22, 45)
    for ($index = 0; $index -lt $themes.Count; $index++) {
        $theme = $themes[$index]
        $x = 18 + ($index % 3) * 504
        $y = 80 + [Math]::Floor($index / 3) * 328
        $file = Join-Path $PreviewRoot ($theme[0] + '.png')
        $source = [Drawing.Image]::FromFile($file)
        try { $graphics.DrawImage($source, [Drawing.Rectangle]::new($x, $y, 492, 277)) }
        finally { $source.Dispose() }
        $graphics.DrawString($theme[1], $nameFont, $white, $x + 2, $y + 280)
        $graphics.DrawString($theme[2], $detailFont, $muted, $x + 2, $y + 302)
    }
    $output = Join-Path (Split-Path $PreviewRoot -Parent) ('theme-gallery-'+$version+'.png')
    $canvas.Save($output, [Drawing.Imaging.ImageFormat]::Png)
    Write-Output $output
} finally {
    $graphics.Dispose(); $canvas.Dispose(); $titleFont.Dispose(); $nameFont.Dispose()
    $detailFont.Dispose(); $white.Dispose(); $muted.Dispose()
}
