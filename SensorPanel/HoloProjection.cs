using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;

namespace SensorPanel;

// Projected light behind the real readouts. None of these marks represent telemetry.
internal sealed class HoloProjection : FrameworkElement
{
    private readonly PanelTheme theme;
    private readonly DispatcherTimer timer;
    private readonly Stopwatch clock = new();
    private readonly SolidColorBrush movingHalo = Ink(Color.FromRgb(71, 209, 255), .07);
    private readonly SolidColorBrush movingGlow = Ink(Color.FromRgb(71, 209, 255), .30);
    private readonly SolidColorBrush movingCore = Ink(Color.FromRgb(205, 249, 255), .80);
    private DrawingGroup? backdrop;
    private Size drawnSize;
    private Point floorCentre;
    private double floorRadiusX, floorRadiusY, strokeScale = 1, phase;
    private bool motion = true, watchingSystem;

    public bool MotionActive => timer.IsEnabled;

    public HoloProjection(PanelTheme theme)
    {
        this.theme = theme;
        IsHitTestVisible = false;
        Focusable = false;
        ClipToBounds = true;
        timer = new DispatcherTimer(DispatcherPriority.Background, Dispatcher)
        { Interval = TimeSpan.FromMilliseconds(50) };
        timer.Tick += (_, _) =>
        {
            if (!CanMove) { UpdateMotion(); return; }
            phase = clock.Elapsed.TotalSeconds * Math.PI / 9;
            InvalidateVisual();
        };
        Loaded += (_, _) =>
        {
            if (!watchingSystem)
            {
                SystemParameters.StaticPropertyChanged += SystemPreferenceChanged;
                watchingSystem = true;
            }
            UpdateMotion();
        };
        Unloaded += (_, _) =>
        {
            timer.Stop(); clock.Stop();
            if (watchingSystem)
            {
                SystemParameters.StaticPropertyChanged -= SystemPreferenceChanged;
                watchingSystem = false;
            }
        };
        IsVisibleChanged += (_, _) => UpdateMotion();
        IsEnabledChanged += (_, _) => UpdateMotion();
        SizeChanged += (_, _) => InvalidateVisual();
    }

    public void SetMotion(bool enabled)
    {
        motion = enabled;
        UpdateMotion();
    }

    private bool CanMove => motion && IsEnabled && IsLoaded && IsVisible && SystemParameters.ClientAreaAnimation;

    private void SystemPreferenceChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is null or "ClientAreaAnimation")
        {
            if (Dispatcher.CheckAccess()) UpdateMotion();
            else _ = Dispatcher.BeginInvoke(new Action(UpdateMotion));
        }
    }

    private void UpdateMotion()
    {
        if (CanMove) { clock.Start(); timer.Start(); }
        else { timer.Stop(); clock.Stop(); }
    }

    protected override void OnRender(DrawingContext context)
    {
        base.OnRender(context);
        if (ActualWidth <= 1 || ActualHeight <= 1) return;
        var size = new Size(ActualWidth, ActualHeight);
        if (backdrop is null || drawnSize != size) CreateBackdrop(size);
        context.DrawDrawing(backdrop!);

        // A single moving light follows the projection base independently of sensor updates.
        var light = new Point(floorCentre.X + Math.Cos(phase) * floorRadiusX,
            floorCentre.Y + Math.Sin(phase) * floorRadiusY);
        context.DrawEllipse(movingHalo, null, light, 11 * strokeScale, 5 * strokeScale);
        context.DrawEllipse(movingGlow, null, light, 4.5 * strokeScale, 2.5 * strokeScale);
        context.DrawEllipse(movingCore, null, light, 1.7 * strokeScale, 1.7 * strokeScale);
    }

    private void CreateBackdrop(Size size)
    {
        drawnSize = size;
        double w = size.Width, h = size.Height;
        strokeScale = Math.Clamp(Math.Sqrt(w * h / (880 * 460)), .55, 1.6);
        floorCentre = new Point(w * .51, h * .91);
        floorRadiusX = w * .425;
        floorRadiusY = h * .044;
        Color blue = Color.FromRgb(64, 155, 255), cyan = Color.FromRgb(76, 224, 242);
        Color teal = Color.FromRgb(57, 228, 179), amber = theme.SecondaryAccent?.Color ?? Color.FromRgb(255, 189, 92);
        var drawing = new DrawingGroup();
        using (var dc = drawing.Open())
        {
            // Soft light at the table's base; the middle remains clear for sensor values.
            var spill = new RadialGradientBrush
            {
                Center = new Point(.51, .94), GradientOrigin = new Point(.51, .94),
                RadiusX = .52, RadiusY = .18,
                GradientStops = new GradientStopCollection
                {
                    new(Color.FromArgb(22, cyan.R, cyan.G, cyan.B), 0),
                    new(Color.FromArgb(8, blue.R, blue.G, blue.B), .48),
                    new(Colors.Transparent, 1)
                }
            };
            spill.Freeze();
            dc.DrawRectangle(spill, null, new Rect(size));

            foreach (double scale in new[] { .35, .55, .77, 1.0 })
            {
                var arc = Arc(floorCentre, w * .43 * scale, h * .79 * scale, 184, 356);
                DrawLight(dc, arc, blue, .19, 1.0 * strokeScale);
                // A brighter short segment makes the projection feel dimensional without a grid.
                double start = scale < .6 ? 204 : 279;
                var highlight = Arc(floorCentre, w * .43 * scale, h * .79 * scale, start, start + 12);
                dc.DrawGeometry(null, Stroke(cyan, .31, .9 * strokeScale), highlight);
            }

            foreach (double scale in new[] { .72, .87, 1.0 })
            {
                var ellipse = new EllipseGeometry(floorCentre, floorRadiusX * scale, floorRadiusY * scale);
                ellipse.Freeze();
                DrawLight(dc, ellipse, cyan, .15, .8 * strokeScale);
            }
            // Short rim strokes, not a floor grid.
            for (int i = 0; i < 11; i++)
            {
                double angle = (202 + i * 13.5) * Math.PI / 180;
                var outer = new Point(floorCentre.X + Math.Cos(angle) * floorRadiusX,
                    floorCentre.Y + Math.Sin(angle) * floorRadiusY);
                var inner = new Point(floorCentre.X + Math.Cos(angle) * floorRadiusX * .975,
                    floorCentre.Y + Math.Sin(angle) * floorRadiusY * .75);
                dc.DrawLine(Stroke(cyan, .28, .8 * strokeScale), inner, outer);
            }

            // Sparse beacons stay on the periphery rather than crossing the readings.
            foreach (var point in new[]
            {
                new Point(.035, .20), new Point(.075, .31), new Point(.025, .47),
                new Point(.065, .66), new Point(.95, .15), new Point(.975, .37),
                new Point(.94, .57), new Point(.975, .76)
            })
            {
                var p = new Point(point.X * w, point.Y * h);
                dc.DrawEllipse(Ink(teal, .07), null, p, 6 * strokeScale, 6 * strokeScale);
                dc.DrawEllipse(null, Stroke(teal, .35, .8 * strokeScale), p, 2.2 * strokeScale, 2.2 * strokeScale);
                dc.DrawEllipse(Ink(teal, .50), null, p, .85 * strokeScale, .85 * strokeScale);
            }

            DrawLeader(dc, w, h, .025, .13, .115, .10, cyan);
            DrawLeader(dc, w, h, .975, .26, .905, .23, amber);
            DrawLeader(dc, w, h, .035, .78, .125, .75, amber);
            DrawLeader(dc, w, h, .965, .85, .89, .80, cyan);

            foreach (var point in new[] { new Point(.055, .105), new Point(.955, .65), new Point(.865, .935) })
            {
                double x = point.X * w, y = point.Y * h, r = 2.8 * strokeScale;
                var diamond = new StreamGeometry();
                using (var shape = diamond.Open())
                {
                    shape.BeginFigure(new Point(x, y - r), true, true);
                    shape.PolyLineTo([new Point(x + r, y), new Point(x, y + r), new Point(x - r, y)], true, false);
                }
                diamond.Freeze();
                dc.DrawGeometry(Ink(amber, .39), Stroke(amber, .20, 4 * strokeScale), diamond);
            }
        }
        drawing.Freeze();
        backdrop = drawing;
    }

    private void DrawLeader(DrawingContext dc, double w, double h, double startX, double startY,
        double endX, double endY, Color color)
    {
        var start = new Point(w * startX, h * startY);
        var end = new Point(w * endX, h * endY);
        var line = new StreamGeometry();
        using (var path = line.Open())
        {
            path.BeginFigure(start, false, false);
            path.PolyLineTo([new Point((start.X + end.X) * .5, end.Y), end], true, false);
        }
        line.Freeze();
        dc.DrawGeometry(null, Stroke(color, .18, .8 * strokeScale), line);
        dc.DrawEllipse(null, Stroke(color, .38, .8 * strokeScale), start, 2.2 * strokeScale, 2.2 * strokeScale);
        dc.DrawLine(Stroke(color, .32, .9 * strokeScale), new Point(end.X, end.Y - 3 * strokeScale),
            new Point(end.X, end.Y + 3 * strokeScale));
    }

    private static StreamGeometry Arc(Point centre, double rx, double ry, double startDegrees, double endDegrees)
    {
        static Point Position(Point centre, double rx, double ry, double degrees)
        {
            double angle = degrees * Math.PI / 180;
            return new Point(centre.X + Math.Cos(angle) * rx, centre.Y + Math.Sin(angle) * ry);
        }
        var path = new StreamGeometry();
        using (var context = path.Open())
        {
            context.BeginFigure(Position(centre, rx, ry, startDegrees), false, false);
            context.ArcTo(Position(centre, rx, ry, endDegrees), new Size(rx, ry), 0,
                endDegrees - startDegrees > 180, SweepDirection.Clockwise, true, false);
        }
        path.Freeze();
        return path;
    }

    private static void DrawLight(DrawingContext context, Geometry path, Color color, double opacity, double width)
    {
        context.DrawGeometry(null, Stroke(color, opacity * .12, width * 7), path);
        context.DrawGeometry(null, Stroke(color, opacity * .35, width * 2.8), path);
        context.DrawGeometry(null, Stroke(color, opacity, width), path);
    }

    private static SolidColorBrush Ink(Color color, double opacity)
    {
        var brush = new SolidColorBrush(color) { Opacity = opacity };
        brush.Freeze(); return brush;
    }

    private static Pen Stroke(Color color, double opacity, double width)
    {
        var pen = new Pen(Ink(color, opacity), width) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
        pen.Freeze(); return pen;
    }
}
