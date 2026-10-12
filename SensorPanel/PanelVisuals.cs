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
        var image = new BitmapImage(new Uri("pack://application:,,,/Aorus/" + path));
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
    public TemperatureGauge()
    {
        Width = 138; Height = 138;
        Children.Add(new Image { Source = Artwork.Load("Images/Rings/Ring.png"), Width = 138, Height = 138 });
        arc = new Path { Stroke = Artwork.Orange, StrokeThickness = 1.4 };
        Children.Add(arc);
        value = new TextBlock { Text = "—", FontFamily = Artwork.Body, FontWeight = FontWeights.SemiBold,
            FontSize = 40, Width = 138, TextAlignment = TextAlignment.Center, Foreground = Brushes.Black };
        SetTop(value, 36); Children.Add(value);
        maximum = new TextBlock { Text = "max: —", FontFamily = Artwork.Body, FontWeight = FontWeights.SemiBold,
            FontSize = 16, Width = 138, TextAlignment = TextAlignment.Center, Foreground = Artwork.Gray };
        SetTop(maximum, 87); Children.Add(maximum);
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
    public void Add(double? value)
    {
        history.Enqueue(value);
        while (history.Count > 60) history.Dequeue();
        InvalidateVisual();
    }
    protected override void OnRender(DrawingContext drawing)
    {
        base.OnRender(drawing);
        drawing.DrawRectangle(new SolidColorBrush(Color.FromRgb(67, 67, 67)), null, new Rect(0, 0, ActualWidth, ActualHeight));
        var points = history.ToArray();
        double max = Math.Max(1, points.Where(x => x.HasValue).Select(x => x!.Value).DefaultIfEmpty(1).Max() * 1.15);
        var pen = new Pen(Artwork.Orange, 1.5);
        Point? previous = null;
        for (int i = 0; i < points.Length; i++)
        {
            if (!points[i].HasValue) { previous = null; continue; }
            var point = new Point(ActualWidth * (60 - points.Length + i) / 59.0,
                ActualHeight - 3 - Math.Clamp(points[i]!.Value / max, 0, 1) * (ActualHeight - 6));
            if (previous.HasValue) drawing.DrawLine(pen, previous.Value, point);
            previous = point;
        }
    }
}

internal sealed class UsageBar : Canvas
{
    private readonly Image fill;
    public UsageBar()
    {
        Width = 109; Height = 9;
        Children.Add(new Image { Source = Artwork.Load("Images/Bars/empty.png"), Width = 109, Height = 9 });
        fill = new Image { Source = Artwork.Load("Images/Bars/full.png"), Width = 109, Height = 9 };
        Children.Add(fill);
        Update(null);
    }
    public void Update(double? percent) => fill.Clip = new RectangleGeometry(new Rect(0, 0, 109 * Math.Clamp((percent ?? 0) / 100, 0, 1), 9));
}
