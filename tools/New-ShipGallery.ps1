param([string]$PreviewRoot = (Join-Path (Split-Path $PSScriptRoot -Parent) 'artifacts\theme-previews\1280x720'))
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$themes = @(
    @('rocinante', 'ROCINANTE', 'Flight engineering / ice blue + amber'),
    @('mcrn', 'DONNAGER', 'Battleship / coral + warm brass'),
    @('unn', 'TRUMAN', 'Dreadnought / steel blue + sea glass'),
    @('belter', 'TYNAN', 'Salvage ship / amber + sage'),
    @('razorback', 'RAZORBACK', 'Racing pinnace / rose + cyan'),
    @('morrigan', 'MORRIGAN', 'Patrol destroyer / green + warm gold'),
    @('dropship', 'MCRN DROPSHIP', 'Landing console / sand gold + pale blue')
)
$canvas = [Drawing.Bitmap]::new(1536, 1930)
$graphics = [Drawing.Graphics]::FromImage($canvas)
$titleFont = [Drawing.Font]::new('Segoe UI', 18, [Drawing.FontStyle]::Bold)
$nameFont = [Drawing.Font]::new('Segoe UI', 13, [Drawing.FontStyle]::Bold)
$detailFont = [Drawing.Font]::new('Segoe UI', 10)
$white = [Drawing.SolidBrush]::new([Drawing.Color]::FromArgb(235, 242, 250))
$muted = [Drawing.SolidBrush]::new([Drawing.Color]::FromArgb(158, 177, 197))
try {
    $graphics.Clear([Drawing.Color]::FromArgb(8, 13, 20))
    $graphics.InterpolationMode = [Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $graphics.DrawString('THE EXPANSE / ENGINEERING CONSOLES', $titleFont, $white, 20, 12)
    $graphics.DrawString('FAN MADE  |  v1.4.0  |  Actual saved-reading renders / original single-ship crops / faded full-app backgrounds', $detailFont, $muted, 22, 45)
    for ($index = 0; $index -lt $themes.Count; $index++) {
        $theme = $themes[$index]
        $x = 18 + ($index % 2) * 758
        $y = 80 + [Math]::Floor($index / 2) * 458
        $source = [Drawing.Image]::FromFile((Join-Path $PreviewRoot ($theme[0] + '.png')))
        try { $graphics.DrawImage($source, [Drawing.Rectangle]::new($x, $y, 742, 417)) }
        finally { $source.Dispose() }
        $graphics.DrawString($theme[1], $nameFont, $white, $x + 2, $y + 419)
        $graphics.DrawString($theme[2], $detailFont, $muted, $x + 2, $y + 439)
    }
    $output = Join-Path (Split-Path $PreviewRoot -Parent) 'expanse-gallery-1.4.0.png'
    $canvas.Save($output, [Drawing.Imaging.ImageFormat]::Png)
    Write-Output $output
} finally {
    $graphics.Dispose(); $canvas.Dispose(); $titleFont.Dispose(); $nameFont.Dispose()
    $detailFont.Dispose(); $white.Dispose(); $muted.Dispose()
}
