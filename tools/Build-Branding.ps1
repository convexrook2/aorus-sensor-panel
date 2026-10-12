$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
Add-Type -AssemblyName PresentationCore,PresentationFramework,WindowsBase
$branding = Join-Path $projectRoot 'Assets\Branding'
New-Item -ItemType Directory -Path $branding -Force | Out-Null
$source = [Windows.Media.Imaging.BitmapFrame]::Create([uri](Join-Path $projectRoot 'Assets\AORUS\@Resources\GIFs\L\Comp 0.png'))
$logo = [Windows.Media.Imaging.CroppedBitmap]::new($source,[Windows.Int32Rect]::new(16,466,112,106))
$frames = @()
foreach ($size in @(16,24,32,48,64,128,256)) {
    $visual = [Windows.Media.DrawingVisual]::new()
    $drawing = $visual.RenderOpen()
    $drawing.PushTransform([Windows.Media.ScaleTransform]::new($size/256.0,$size/256.0))
    $drawing.DrawRoundedRectangle([Windows.Media.Brushes]::Black,$null,[Windows.Rect]::new(0,0,256,256),20,20)
    $drawing.DrawImage($logo,[Windows.Rect]::new(39,12,178,168))
    $drawing.DrawRectangle([Windows.Media.SolidColorBrush]::new([Windows.Media.Color]::FromRgb(255,100,0)),$null,[Windows.Rect]::new(10,194,236,51))
    $label = [Windows.Media.FormattedText]::new('FAN MADE',[Globalization.CultureInfo]::InvariantCulture,[Windows.FlowDirection]::LeftToRight,[Windows.Media.Typeface]::new([Windows.Media.FontFamily]::new('Segoe UI'),[Windows.FontStyles]::Normal,[Windows.FontWeights]::Bold,[Windows.FontStretches]::Normal),30,[Windows.Media.Brushes]::Black,1.0)
    $drawing.DrawText($label,[Windows.Point]::new((256-$label.Width)/2,198))
    $drawing.Pop(); $drawing.Close()
    $bitmap = [Windows.Media.Imaging.RenderTargetBitmap]::new($size,$size,96,96,[Windows.Media.PixelFormats]::Pbgra32)
    $bitmap.Render($visual)
    $encoder = [Windows.Media.Imaging.PngBitmapEncoder]::new()
    $encoder.Frames.Add([Windows.Media.Imaging.BitmapFrame]::Create($bitmap))
    $stream = [IO.MemoryStream]::new(); $encoder.Save($stream)
    $frames += [pscustomobject]@{Size=$size;Bytes=$stream.ToArray()}; $stream.Dispose()
    if ($size -eq 256) { [IO.File]::WriteAllBytes((Join-Path $branding 'AorusFanMade.png'),$frames[-1].Bytes) }
}
$file = [IO.File]::Create((Join-Path $branding 'AorusFanMade.ico'))
$writer = [IO.BinaryWriter]::new($file)
try {
    $writer.Write([uint16]0); $writer.Write([uint16]1); $writer.Write([uint16]$frames.Count)
    $offset = 6 + 16*$frames.Count
    foreach ($frame in $frames) {
        $dimension = if ($frame.Size -eq 256) {0} else {$frame.Size}
        $writer.Write([byte]$dimension); $writer.Write([byte]$dimension); $writer.Write([byte]0); $writer.Write([byte]0)
        $writer.Write([uint16]1); $writer.Write([uint16]32); $writer.Write([uint32]$frame.Bytes.Length); $writer.Write([uint32]$offset)
        $offset += $frame.Bytes.Length
    }
    foreach ($frame in $frames) {$writer.Write([byte[]]$frame.Bytes)}
} finally { $writer.Dispose(); $file.Dispose() }
Write-Host 'AORUS source logo with FAN MADE badge: seven icon sizes generated.'
