using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;

namespace SensorPanel;

internal sealed class RingSpaceView : Grid
{
    internal const string OverviewId = "RingSpace", StationId = "RingStation", MedinaId = "Medina",
        SolGateId = "RingSpaceSolGate", IlusGateId = "IlusGate";
    internal static readonly (string Id, string Label)[] Targets =
    [
        (OverviewId, "Ring Space overview"), (StationId, "Ring Station"), (MedinaId, "Medina Station"),
        (SolGateId, "Sol gate"), (IlusGateId, "Ilus gate")
    ];

    private readonly RingChart chart;
    private readonly TextBlock name = new() { FontSize = 14, FontWeight = FontWeights.SemiBold, TextTrimming = TextTrimming.CharacterEllipsis };
    private readonly TextBlock detail = new() { FontSize = 10, TextTrimming = TextTrimming.CharacterEllipsis };
    private readonly Grid stage = new() { Background = Brushes.Transparent, ClipToBounds = true, Cursor = Cursors.SizeAll };
    private readonly OrthographicCamera camera = new(new(0, 0, 5), new(0, 0, -1), new(0, 1, 0), 3.1);
    private readonly Model3DGroup scene = new();
    private readonly Model3DGroup inspected = new();
    private readonly AxisAngleRotation3D yaw = new(new(0, 1, 0), -28), pitch = new(new(1, 0, 0), 19);
    private readonly Viewport3D viewport = new() { IsHitTestVisible = false };
    private readonly RadialGradientBrush inspectionLight = new(Color.FromArgb(60, 45, 164, 207), Colors.Transparent);
    private Model3DGroup? station, gateMarker;
    private ShipPoster? medina;
    private Point? drag;
    private double magnification = 1;
    private string selected = OverviewId;

    internal event Action<string>? Selected;

    internal RingSpaceView()
    {
        ClipToBounds = true;
        ColumnDefinitions.Add(new() { Width = new GridLength(2, GridUnitType.Star) });
        ColumnDefinitions.Add(new() { Width = new GridLength(1, GridUnitType.Star) });
        chart = new() { Margin = new Thickness(8, 0, 3, 0) };
        chart.Selected += id => Selected?.Invoke(id);
        Children.Add(chart);

        var inspection = new Grid { Margin = new Thickness(11, 1, 10, 3) };
        inspection.ColumnDefinitions.Add(new() { Width = new GridLength(1, GridUnitType.Star) });
        inspection.ColumnDefinitions.Add(new() { Width = new GridLength(0) });
        inspection.RowDefinitions.Add(new() { Height = GridLength.Auto });
        inspection.RowDefinitions.Add(new() { Height = new GridLength(1, GridUnitType.Star) });
        inspection.RowDefinitions.Add(new() { Height = GridLength.Auto });
        Grid.SetColumn(inspection, 1); Children.Add(inspection);
        inspection.Children.Add(name);
        SetRow(stage, 1); inspection.Children.Add(stage);
        inspectionLight.Freeze();
        stage.Children.Add(new Border { IsHitTestVisible = false, Background = inspectionLight });
        viewport.Camera = camera;
        scene.Children.Add(new AmbientLight(Color.FromRgb(102, 126, 156)));
        scene.Children.Add(new DirectionalLight(Color.FromRgb(208, 233, 250), new(.5, -.6, -1)));
        scene.Children.Add(new DirectionalLight(Color.FromRgb(61, 140, 172), new(-1, .2, .4)));
        inspected.Transform = new Transform3DGroup { Children = new Transform3DCollection { new RotateTransform3D(yaw), new RotateTransform3D(pitch) } };
        scene.Children.Add(inspected);
        viewport.Children.Add(new ModelVisual3D { Content = scene });
        stage.Children.Add(viewport);
        stage.SizeChanged += (_, _) => Frame();
        stage.ToolTip = "Drag to rotate the 3D model. Scroll to zoom.";
        AutomationProperties.SetName(stage, "Ring Space model inspection");
        stage.MouseLeftButtonDown += (_, args) => { if (selected == MedinaId) return; drag = args.GetPosition(stage); stage.CaptureMouse(); args.Handled = true; };
        stage.MouseMove += (_, args) =>
        {
            if (drag is not { } before) return;
            Point after = args.GetPosition(stage);
            yaw.Angle = (yaw.Angle + (after.X - before.X) * .65) % 360;
            pitch.Angle = Math.Clamp(pitch.Angle - (after.Y - before.Y) * .65, -85, 85);
            drag = after;
        };
        stage.MouseLeftButtonUp += (_, _) => stage.ReleaseMouseCapture();
        stage.LostMouseCapture += (_, _) => drag = null;
        stage.MouseWheel += (_, args) =>
        {
            if (selected == MedinaId) return;
            magnification = Math.Clamp(magnification * Math.Pow(1.13, args.Delta / 120.0), .7, 2.4);
            Frame(); args.Handled = true;
        };
        SetRow(detail, 2); inspection.Children.Add(detail);
        inspection.SizeChanged += (_, args) =>
        {
            bool compact = args.NewSize.Height < 72;
            inspection.ColumnDefinitions[0].Width = new GridLength(compact ? .38 : 1, GridUnitType.Star);
            inspection.ColumnDefinitions[1].Width = compact ? new GridLength(.62, GridUnitType.Star) : new GridLength(0);
            SetColumn(name, compact ? 1 : 0); SetColumn(detail, compact ? 1 : 0);
            SetRow(stage, compact ? 0 : 1); SetRowSpan(stage, compact ? 3 : 1);
            name.FontSize = compact ? 12 : 14;
            detail.FontSize = compact ? 9 : 10;
        };
    }

    internal static bool Contains(string id) => Targets.Any(target => target.Id == id);

    internal void ApplyTheme(SolidColorBrush accent, SolidColorBrush muted)
    {
        name.Foreground = accent;
        detail.Foreground = muted;
        chart.ApplyTheme(accent);
    }

    internal void FocusTarget(string id)
    {
        selected = id;
        chart.FocusTarget(id == OverviewId ? StationId : id);
        name.Text = id == OverviewId ? "Ring Station" : Targets.Single(row => row.Id == id).Label;
        detail.Text = id switch
        {
            MedinaId => "Nauvoo / Behemoth hull · V4 portrait",
            SolGateId => "Connection to the Sol system",
            IlusGateId => "Connection to the Ilus system",
            _ => "Alien control station · 3D inspection"
        };
        viewport.Visibility = id == MedinaId ? Visibility.Collapsed : Visibility.Visible;
        if (id == MedinaId)
        {
            if (medina is null)
            {
                var vignette = new RadialGradientBrush { RadiusX = .5, RadiusY = .51 };
                vignette.GradientStops.Add(new(Colors.White, .58));
                vignette.GradientStops.Add(new(Colors.Transparent, 1));
                vignette.Freeze();
                medina = new ShipPoster("Medina") { Margin = new Thickness(4), HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center, OpacityMask = vignette };
                stage.Children.Add(medina);
            }
            medina.Visibility = Visibility.Visible;
        }
        else if (medina is not null) medina.Visibility = Visibility.Collapsed;

        inspected.Children.Clear();
        if (id != MedinaId) inspected.Children.Add(id switch
        {
            SolGateId => SolarGateModel.FrozenModel,
            IlusGateId => gateMarker ??= RingStationModel.BuildGateMarker(),
            _ => station ??= RingStationModel.Build()
        });
        stage.Cursor = id == MedinaId ? Cursors.Arrow : Cursors.SizeAll;
        magnification = 1; yaw.Angle = -28; pitch.Angle = 19;
        Frame();
    }

    internal int ModelTriangles => inspected.Children.OfType<Model3DGroup>().SelectMany(group => group.Children.OfType<GeometryModel3D>())
        .Sum(geometry => (geometry.Geometry as MeshGeometry3D)?.TriangleIndices.Count / 3 ?? 0);

    internal object Diagnose() => new
    {
        selected, scene = "RingSpace", schematic = true, toScale = false, renderActive = false,
        frameCount = 0L, labelOverlaps = chart.LabelOverlaps, gateMarkers = RingChart.GateMarkerCount,
        modelTriangles = ModelTriangles, hasMedinaPortrait = medina?.IsVisible == true
    };

    private void Frame() => camera.Width = 2.8 * Math.Max(1, stage.ActualWidth / Math.Max(1, stage.ActualHeight)) / magnification;

    private sealed class RingChart : Canvas
    {
        internal const int GateMarkerCount = 112;
        private static readonly Brush tint = ShipPanel.OpenDeckWash(Color.FromArgb(77, 45, 117, 158), new(.49, .48), .5, .58);
        private static readonly Pen boundary = new(new SolidColorBrush(Color.FromArgb(175, 99, 205, 228)), .85);
        private static readonly Pen meridian = new(new SolidColorBrush(Color.FromArgb(62, 101, 170, 218)), .65);
        private static readonly Pen distant = new(new SolidColorBrush(Color.FromArgb(88, 109, 197, 222)), .64);
        private static readonly Pen near = new(new SolidColorBrush(Color.FromArgb(188, 139, 231, 245)), .85);
        private static readonly (double X, double Y, double Depth)[] gates = Enumerable.Range(0, GateMarkerCount).Select(index =>
        {
            double y = 2 * (index + .5) / GateMarkerCount - 1, radius = Math.Sqrt(1 - y * y);
            double angle = index * Math.PI * (3 - Math.Sqrt(5));
            return (Math.Cos(angle) * radius, y, Math.Sin(angle) * radius);
        }).ToArray();
        private readonly (string Id, Point Offset, Button Button)[] marks;
        private SolidColorBrush accent = Brushes.LightCyan;
        private string selected = StationId;
        private Point center;
        private double rx, ry;
        internal event Action<string>? Selected;
        internal int LabelOverlaps
        {
            get
            {
                var bounds = marks.Where(mark => mark.Button.Visibility == Visibility.Visible)
                    .Select(mark => new Rect(new Point(GetLeft(mark.Button), GetTop(mark.Button)), mark.Button.DesiredSize)).ToArray();
                return bounds.SelectMany((rect, i) => bounds.Skip(i + 1).Select(other => Rect.Intersect(rect, other)))
                    .Count(overlap => overlap.Width > 0 && overlap.Height > 0);
            }
        }

        internal RingChart()
        {
            var chrome = new FrameworkElementFactory(typeof(Border));
            chrome.SetValue(Border.BackgroundProperty, new SolidColorBrush(Color.FromArgb(205, 9, 20, 32)));
            chrome.SetValue(Border.PaddingProperty, new Thickness(3, 1, 3, 1));
            chrome.SetValue(Border.CornerRadiusProperty, new CornerRadius(3));
            chrome.AppendChild(new FrameworkElementFactory(typeof(ContentPresenter)));
            var template = new ControlTemplate(typeof(Button)) { VisualTree = chrome };
            marks = [Mark(StationId, "Ring Station", new(0, 0)), Mark(MedinaId, "Medina", new(.40, .29)),
                Mark(SolGateId, "Sol gate", new(-.92, .392)), Mark(IlusGateId, "Ilus gate", new(.86, -.51))];
            SizeChanged += (_, _) => ArrangeMarkers();

            (string, Point, Button) Mark(string id, string label, Point offset)
            {
                var item = new Button { Content = label, Template = template, FontSize = 11, Cursor = Cursors.Hand };
                AutomationProperties.SetName(item, label);
                item.Click += (_, args) => { args.Handled = true; Selected?.Invoke(id); };
                Children.Add(item);
                return (id, offset, item);
            }
        }

        internal void ApplyTheme(SolidColorBrush color) { accent = color; FocusTarget(selected); }

        internal void FocusTarget(string id)
        {
            selected = id;
            foreach (var mark in marks) mark.Button.Foreground = mark.Id == id ? accent : Brushes.LightCyan;
            InvalidateVisual();
        }

        private Point Anchor(Point offset) => new(center.X + offset.X * rx, center.Y + offset.Y * ry);

        private void ArrangeMarkers()
        {
            center = new(ActualWidth * .49, ActualHeight * .49);
            rx = Math.Max(0, Math.Min(ActualWidth * .37, ActualHeight * 1.35));
            ry = Math.Max(0, ActualHeight * .40);
            var occupied = new List<Rect>(marks.Length);
            foreach (var (id, offset, button) in marks)
            {
                button.Visibility = Visibility.Visible;
                button.FontSize = ActualHeight < 105 ? 9 : 11;
                button.Measure(new(double.PositiveInfinity, double.PositiveInfinity));
                Point at = Anchor(offset);
                var size = button.DesiredSize;
                double left = at.X - size.Width - 7, right = at.X + 8;
                double[] sides = id == SolGateId ? [left, right] : [right, left];
                var positions = from step in new[] { 0, -1, 1, -2, 2 }
                                from x in sides
                                let y = at.Y - size.Height / 2 + step * (size.Height + 2)
                                select new Rect(Math.Clamp(x, 0, Math.Max(0, ActualWidth - size.Width)),
                                    Math.Clamp(y, 0, Math.Max(0, ActualHeight - size.Height)), size.Width, size.Height);
                var place = positions.Where(rect => occupied.All(used => !used.IntersectsWith(rect))).Select(rect => (Rect?)rect).FirstOrDefault();
                if (place is not Rect box)
                {
                    button.Visibility = Visibility.Collapsed;
                    continue;
                }
                occupied.Add(box);
                SetLeft(button, box.X);
                SetTop(button, box.Y);
            }
            InvalidateVisual();
        }

        protected override void OnRender(DrawingContext drawing)
        {
            base.OnRender(drawing);
            if (rx < 2 || ry < 2) return;
            drawing.DrawRectangle(tint, null, new Rect(RenderSize));
            drawing.DrawEllipse(null, boundary, center, rx, ry);
            drawing.DrawEllipse(null, meridian, center, rx * .68, ry);
            drawing.DrawEllipse(null, meridian, center, rx * .34, ry);
            drawing.DrawEllipse(null, meridian, center, rx, ry * .34);
            foreach (var (x, y, depth) in gates)
            {
                Point at = Anchor(new(x, y));
                double radius = depth > 0 ? 1.25 + depth * 1.45 : 1;
                drawing.DrawEllipse(null, depth > 0 ? near : distant, at, radius, radius * .72);
            }

            foreach (var (id, offset, _) in marks)
            {
                Point at = Anchor(offset);
                var ink = id == selected ? new Pen(accent, 1.25) : boundary;
                if (id == StationId) drawing.DrawEllipse(new SolidColorBrush(Color.FromRgb(22, 75, 98)), ink, at, 6.2, 6.2);
                else if (id == MedinaId) drawing.DrawRoundedRectangle(null, ink, new Rect(at.X - 3.5, at.Y - 3.5, 7, 7), 1, 1);
                else drawing.DrawEllipse(null, ink, at, 5.5, 4.1);
            }
        }
    }
}
