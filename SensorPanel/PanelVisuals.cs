using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using Path = System.Windows.Shapes.Path;

namespace SensorPanel;

internal static class Artwork
{
    public static BitmapImage Load(string path)
    {
        if(path.StartsWith("Animations/",StringComparison.Ordinal))
            path=path.StartsWith("Animations/X/",StringComparison.Ordinal)?path.Replace("Animations/X/","GIFs/GIF1/",StringComparison.Ordinal):path.Replace("Animations/","GIFs/",StringComparison.Ordinal);
        using var stream=Rookline.AssetProtection.AppAssets.Open("AORUS/@Resources/"+path);
        var image = new BitmapImage(); image.BeginInit(); image.StreamSource=stream;
        image.CacheOption=BitmapCacheOption.OnLoad; image.EndInit();
        image.Freeze();
        return image;
    }
    public static FontFamily Body { get; } = new(new Uri("pack://application:,,,/"), "./Aorus/Fonts/#Titillium Web");
    public static FontFamily Title { get; } = new(new Uri("pack://application:,,,/"), "./Aorus/Fonts/#AORUS");
    public static SolidColorBrush Orange { get; } = new(Color.FromRgb(255, 100, 0));
    public static SolidColorBrush Gray { get; } = new(Color.FromRgb(108, 108, 108));
}

internal sealed class TemperatureGauge : Canvas
{
    private readonly TextBlock value, maximum;
    private readonly Path arc;
    private static readonly BitmapSource ringArtwork = Artwork.Load("Images/Rings/Ring.png");
    private static readonly Dictionary<string, BitmapSource> themedRings = new();
    private readonly Image originalRing;
    public TemperatureGauge()
    {
        Width = 138; Height = 138;
        originalRing = new Image { Source = ringArtwork, Width = 138, Height = 138 };
        Children.Add(originalRing);
        arc = new Path { Stroke = Artwork.Orange, StrokeThickness = 1.4 };
        Children.Add(arc);
        value = new TextBlock { Text = "—", FontFamily = Artwork.Body, FontWeight = FontWeights.SemiBold,
            FontSize = 40, Width = 138, TextAlignment = TextAlignment.Center, Foreground = Brushes.Black };
        SetTop(value, 36); Children.Add(value);
        maximum = new TextBlock { Text = "max: —", FontFamily = Artwork.Body, FontWeight = FontWeights.SemiBold,
            FontSize = 16, Width = 138, TextAlignment = TextAlignment.Center, Foreground = Artwork.Gray };
        SetTop(maximum, 87); Children.Add(maximum);
    }
    public void ApplyTheme(PanelTheme theme)
    {
        originalRing.Source = RingFor(theme);
        arc.Stroke = theme.Accent; arc.StrokeThickness = theme.Classic ? 1.4 : 2.5;
        value.Foreground = theme.Foreground; maximum.Foreground = theme.Muted;
    }
    private static BitmapSource RingFor(PanelTheme theme)
    {
        if (theme.Classic) return ringArtwork;
        if (themedRings.TryGetValue(theme.Id, out var cached)) return cached;

        // Recolour the original metallic shading, including the curved AORUS
        // lettering and segmented rim. Alpha stays intact; both gauges share
        // one frozen image per theme, with no work in the sensor polling loop.
        var source = new FormatConvertedBitmap(ringArtwork, PixelFormats.Bgra32, null, 0);
        int stride = source.PixelWidth * 4;
        var pixels = new byte[stride * source.PixelHeight];
        source.CopyPixels(pixels, stride, 0);
        Color shadow = theme.IsDark ? theme.Chrome.Color : theme.Muted.Color;
        Color middle = theme.IsDark ? theme.Divider.Color : theme.Surface.Color;
        Color highlight = theme.IsDark ? theme.Muted.Color : theme.Background.Color;
        for (int i = 0; i < pixels.Length; i += 4)
        {
            double luminance = (0.0722 * pixels[i] + 0.7152 * pixels[i + 1] + 0.2126 * pixels[i + 2]) / 255;
            Color low = luminance < 0.65 ? shadow : middle;
            Color high = luminance < 0.65 ? middle : highlight;
            double blend = luminance < 0.65 ? luminance / 0.65 : (luminance - 0.65) / 0.35;
            pixels[i] = (byte)Math.Round(low.B + (high.B - low.B) * blend);
            pixels[i + 1] = (byte)Math.Round(low.G + (high.G - low.G) * blend);
            pixels[i + 2] = (byte)Math.Round(low.R + (high.R - low.R) * blend);
        }
        var result = BitmapSource.Create(source.PixelWidth, source.PixelHeight, source.DpiX, source.DpiY,
            PixelFormats.Bgra32, null, pixels, stride);
        result.Freeze();
        themedRings.Add(theme.Id, result);
        return result;
    }
    public void Update(double? temperature, double? peak)
    {
        value.Text = temperature.HasValue ? $"{temperature:0}°C" : "—";
        maximum.Text = peak.HasValue ? $"max: {peak:0}°C" : "max: —";
        if (!temperature.HasValue) { arc.Data = null; return; }
        double fraction = Math.Clamp(temperature.Value / 100, 0, 0.9999);
        if (fraction == 0) { arc.Data = null; return; }
        double angle = fraction * 2 * Math.PI;
        var segment = new ArcSegment(new Point(69 + 54 * Math.Sin(angle), 69 - 54 * Math.Cos(angle)),
            new Size(54, 54), 0, fraction > 0.5, SweepDirection.Clockwise, true);
        arc.Data = new PathGeometry([new PathFigure(new Point(69, 15), [segment], false)]);
    }
}

internal sealed class HistoryGraph : FrameworkElement
{
    private readonly Queue<double?> history = new();
    private PanelTheme theme = PanelTheme.Resolve("Classic");
    public void ApplyTheme(PanelTheme value) { theme = value; InvalidateVisual(); }
    public void Add(double? value)
    {
        history.Enqueue(value);
        while (history.Count > 60) history.Dequeue();
        InvalidateVisual();
    }
    protected override void OnRender(DrawingContext drawing)
    {
        base.OnRender(drawing);
        drawing.DrawRectangle(theme.GraphBackground, null, new Rect(0, 0, ActualWidth, ActualHeight));
        var points = history.ToArray();
        double max = Math.Max(1, points.Where(x => x.HasValue).Select(x => x!.Value).DefaultIfEmpty(1).Max() * 1.15);
        var pen = new Pen(theme.Accent, 1.5);
        Point? previous = null;
        for (int i = 0; i < points.Length; i++)
        {
            if (!points[i].HasValue) { previous = null; continue; }
            var point = new Point(ActualWidth * (60 - points.Length + i) / 59.0,
                ActualHeight - 3 - Math.Clamp(points[i]!.Value / max, 0, 1) * (ActualHeight - 6));
            if (previous.HasValue)
            {
                if (theme.Detailed)
                {
                    drawing.PushOpacity(0.12);
                    drawing.DrawLine(new Pen(theme.Accent, 4), previous.Value, point);
                    drawing.Pop();
                }
                drawing.DrawLine(pen, previous.Value, point);
            }
            else drawing.DrawEllipse(theme.Accent, null, point, 1.5, 1.5);
            previous = point;
        }
    }
}

internal sealed class UsageBar : Canvas
{
    private readonly Image fill;
    private readonly Image originalTrack;
    private readonly Border track = new() { Width = 109, Height = 5, CornerRadius = new CornerRadius(2) };
    private readonly Border themedFill = new() { Height = 5, CornerRadius = new CornerRadius(2) };
    public UsageBar()
    {
        Width = 109; Height = 9;
        originalTrack = new Image { Source = Artwork.Load("Images/Bars/empty.png"), Width = 109, Height = 9 };
        Children.Add(originalTrack);
        fill = new Image { Source = Artwork.Load("Images/Bars/full.png"), Width = 109, Height = 9 };
        Children.Add(fill);
        SetTop(track, 2); SetTop(themedFill, 2);
        track.Visibility = themedFill.Visibility = Visibility.Collapsed;
        Children.Add(track); Children.Add(themedFill);
        Update(null);
    }
    public void ApplyTheme(PanelTheme theme)
    {
        originalTrack.Visibility = fill.Visibility = theme.Classic ? Visibility.Visible : Visibility.Collapsed;
        track.Visibility = themedFill.Visibility = theme.Classic ? Visibility.Collapsed : Visibility.Visible;
        track.Background = theme.Divider; themedFill.Background = theme.Accent;
    }
    public void Update(double? percent)
    {
        double width = 109 * Math.Clamp((percent ?? 0) / 100, 0, 1);
        fill.Clip = new RectangleGeometry(new Rect(0, 0, width, 9)); themedFill.Width = width;
    }
}
