using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using System.Windows.Threading;

namespace SensorPanel;

/// <summary>A schematic navigation display. Its positions and motion are illustrative, never ephemerides.</summary>
internal sealed class SolarSystemView : Grid
{
    private readonly MapSurface map;
    private readonly ComboBox selector;
    private readonly TextBlock status;
    private bool selecting;

    public event Action<string>? BodySelected;
    public string SelectedBodyId => map.SelectedBodyId;
    public bool MotionActive => map.MotionActive;

    public SolarSystemView()
    {
        ClipToBounds = true;
        Background = new SolidColorBrush(Color.FromArgb(232, 3, 12, 21));
        RowDefinitions.Add(new() { Height = GridLength.Auto });
        RowDefinitions.Add(new() { Height = new GridLength(1, GridUnitType.Star) });
        RowDefinitions.Add(new() { Height = GridLength.Auto });
        var header = new Grid { Margin = new Thickness(10, 7, 10, 0) };
        header.ColumnDefinitions.Add(new() { Width = new GridLength(1, GridUnitType.Star) });
        header.ColumnDefinitions.Add(new() { Width = GridLength.Auto });
        header.Children.Add(new TextBlock { Text = "SOL / NAVIGATION", Foreground = MapSurface.Ink,
            FontFamily = new FontFamily("Bahnschrift"), FontSize = 11, VerticalAlignment = VerticalAlignment.Center });
        selector = new ComboBox { Width = 164, FontSize = 11, Padding = new Thickness(5, 2, 5, 2),
            Foreground = Brushes.White, Background = new SolidColorBrush(Color.FromRgb(12, 36, 49)),
            BorderBrush = MapSurface.FaintInk, ToolTip = "Focus a planet, moon or station", MaxDropDownHeight = 270 };
        NavigationControls.Apply(selector);
        AutomationProperties.SetName(selector, "Navigation destination");
        foreach (var body in MapSurface.Bodies)
            selector.Items.Add(new ComboBoxItem { Content = body.Label, Tag = body.Id,
                Padding = new Thickness(8, 5, 8, 5), HorizontalContentAlignment = HorizontalAlignment.Stretch,
                Foreground = Brushes.White, Background = new SolidColorBrush(Color.FromRgb(12, 36, 49)) });
        Grid.SetColumn(selector, 1); header.Children.Add(selector); Children.Add(header);
        map = new MapSurface(); Grid.SetRow(map, 1); Children.Add(map);
        var footer = new Grid { Margin = new Thickness(10, 1, 10, 6) };
        footer.ColumnDefinitions.Add(new() { Width = new GridLength(1, GridUnitType.Star) });
        footer.ColumnDefinitions.Add(new() { Width = GridLength.Auto });
        status = new TextBlock { Text = "SCHEMATIC · NOT TO SCALE", FontSize = 9, Foreground = MapSurface.FaintInk,
            FontFamily = new FontFamily("Bahnschrift"), TextTrimming = TextTrimming.CharacterEllipsis };
        footer.Children.Add(status);
        var reset = new Button { Content = "RESET", Foreground = MapSurface.Ink, Background = Brushes.Transparent,
            BorderBrush = MapSurface.FaintInk, BorderThickness = new Thickness(.5), Padding = new Thickness(7, 1, 7, 1),
            FontSize = 9, ToolTip = "Whole system · Home key" };
        reset.Click += (_, _) => FocusBody("Sun"); Grid.SetColumn(reset, 1); footer.Children.Add(reset);
        Grid.SetRow(footer, 2); Children.Add(footer);
        map.Selected += id => { SyncSelection(); BodySelected?.Invoke(id); };
        selector.SelectionChanged += (_, _) =>
        {
            if (!selecting && selector.SelectedItem is ComboBoxItem { Tag: string id }) map.FocusBody(id, true);
        };
        SyncSelection();
    }

    public void FocusBody(string id, bool animate = true) => map.FocusBody(id, animate);
    public void SetMotion(bool enabled) => map.SetMotion(enabled);

    private void SyncSelection()
    {
        selecting = true;
        selector.SelectedIndex = Array.FindIndex(MapSurface.Bodies, b => b.Id == map.SelectedBodyId);
        selecting = false;
        status.Text = map.SelectedBodyId == "Medina" ? "RING SPACE · SEPARATE FROM SOL · SCHEMATIC"
            : map.SelectedBodyId == "Tycho" ? "TYCHO · MOBILE BELT STATION · SCHEMATIC"
            : map.SelectedBodyId == "SolGate" ? "SOL GATE · ~2 AU BEYOND URANUS · SCHEMATIC"
            : "SCHEMATIC · NOT TO SCALE";
    }

    internal void Check() => map.Check();
    internal JsonElement Diagnose() => JsonSerializer.SerializeToElement(map.Diagnose());
    internal object CheckAssets() => SolarBodyAssets.Check();

    private sealed class MapSurface : Grid
    {
        internal sealed record Body(string Id, string Label, double Radius, double Angle, double Size, Color Colour,
            string? Parent = null, bool Station = false, bool Planet = false);

        internal static readonly Body[] Bodies =
        [
            new("Sun", "Sol", 0, 0, 10, Color.FromRgb(255, 205, 104)),
            new("Mercury", "Mercury", .14, .4, 3, Color.FromRgb(163, 161, 151), Planet: true),
            new("Venus", "Venus", .22, 2.35, 4.5, Color.FromRgb(224, 183, 112), Planet: true),
            new("Earth", "Earth", .31, 3.65, 6, Color.FromRgb(58, 147, 231), Planet: true),
            new("Luna", "Luna", .043, -.85, 2.5, Color.FromRgb(192, 199, 205), "Earth"),
            new("Mars", "Mars", .41, 5.28, 5, Color.FromRgb(222, 109, 66), Planet: true),
            new("Ceres", "Ceres", .50, 2.65, 3.1, Color.FromRgb(159, 164, 153)),
            new("Eros", "Eros", .46, 4.14, 2.4, Color.FromRgb(174, 148, 111)),
            new("Tycho", "Tycho · mobile station", .54, .50, 4, Color.FromRgb(90, 235, 237), Station: true),
            new("Jupiter", "Jupiter", .66, 1.1, 11, Color.FromRgb(205, 169, 131), Planet: true),
            new("Io", "Io", .049, .15, 2.6, Color.FromRgb(226, 183, 73), "Jupiter"),
            new("Ganymede", "Ganymede", .079, 3.4, 3.1, Color.FromRgb(163, 172, 174), "Jupiter"),
            new("Callisto", "Callisto", .105, 4.85, 3, Color.FromRgb(126, 144, 151), "Jupiter"),
            new("Saturn", "Saturn", .82, 3.0, 9, Color.FromRgb(207, 191, 139), Planet: true),
            new("Phoebe", "Phoebe", .068, 1.05, 2.5, Color.FromRgb(151, 153, 156), "Saturn"),
            new("Uranus", "Uranus", .96, 4.4, 6.5, Color.FromRgb(114, 207, 215), Planet: true),
            new("Neptune", "Neptune", 1.10, 5.8, 6.5, Color.FromRgb(58, 106, 216), Planet: true),
            // Schematic order: the stationary gate is outside Uranus, inside Neptune.
            new("SolGate", "Sol Ring", 1.03, .20, 5, Color.FromRgb(99, 229, 255), Station: true),
            new("Medina", "Medina · Ring Space", 0, 0, 5, Color.FromRgb(255, 189, 98), Station: true)
        ];

        internal static readonly SolidColorBrush Ink = Frozen(Color.FromRgb(132, 233, 251));
        internal static readonly SolidColorBrush FaintInk = Frozen(Color.FromRgb(92, 141, 160));
        private static readonly SolidColorBrush Amber = Frozen(Color.FromRgb(255, 199, 110));
        private static readonly SolidColorBrush LabelBackground = Frozen(Color.FromArgb(205, 3, 14, 23));
        private static readonly SolidColorBrush RingSpaceBackground = Frozen(Color.FromArgb(220, 6, 20, 30));
        private static readonly Pen Orbit = Pen(Color.FromArgb(72, 74, 182, 225), .75);
        private static readonly Pen OrbitBright = Pen(Color.FromArgb(145, 82, 219, 247), 1);
        private static readonly Pen Leader = Pen(Color.FromArgb(130, 80, 182, 199), .65);
        private static readonly Pen Highlight = Pen(Color.FromArgb(235, 130, 242, 255), 1);
        private static readonly Pen RingSpaceBorder = Pen(Color.FromArgb(120, 255, 191, 104), .75);
        private static readonly Pen Link = DashedPen();
        private static readonly Typeface Typeface = new("Bahnschrift");
        private readonly DispatcherTimer timer = new(DispatcherPriority.Render) { Interval = TimeSpan.FromMilliseconds(50) };
        private readonly Stopwatch clock = new();
        private readonly Viewport3D viewport = new() { IsHitTestVisible = false, ClipToBounds = true };
        private readonly OrthographicCamera camera = new() { Position = new(0, 0, 2000), LookDirection = new(0, 0, -1), UpDirection = new(0, 1, 0), NearPlaneDistance = 1, FarPlaneDistance = 4000 };
        private readonly Dictionary<string, MatrixTransform3D> transforms = new();
        private readonly Overlay backdrop, overlay;
        private readonly Dictionary<string, FormattedText> text = new();
        private readonly List<(Body Body, Point Point, double Radius)> hitTargets = new();
        private readonly List<(Body Body, Rect Bounds)> labelTargets = new();
        private readonly List<Rect> labelBounds = new();
        private readonly (Point Point, double Radius, double Opacity)[] stars;
        private bool motion = true, loaded, moving, checking;
        private double time, lastTime, yaw = -.24, tilt = .35, zoom = 1, targetZoom = 1;
        private long frames;
        private Vector pan, targetPan;
        private Point? drag;
        private Point dragStart;
        private string selected = "Sun";
        internal event Action<string>? Selected;
        internal string SelectedBodyId => selected;
        internal bool MotionActive => timer.IsEnabled;

        internal MapSurface()
        {
            Focusable = true; ClipToBounds = true; Cursor = Cursors.Hand; IsManipulationEnabled = true; Background = Brushes.Transparent;
            ToolTip = "Select a body · Drag to rotate · Wheel to zoom · Arrows to rotate · Home to reset";
            var random = new Random(1977);
            stars = Enumerable.Range(0, 70).Select(_ => (new Point(random.NextDouble(), random.NextDouble()),
                .35 + random.NextDouble() * .65, .10 + random.NextDouble() * .23)).ToArray();
            var models = new Model3DGroup();
            models.Children.Add(new AmbientLight(Color.FromRgb(91, 95, 105)));
            models.Children.Add(new DirectionalLight(Color.FromRgb(255, 244, 227), new Vector3D(.7, -.4, -1)));
            foreach (var body in Bodies)
            {
                var transform = new MatrixTransform3D(); transforms.Add(body.Id, transform);
                var instance = new Model3DGroup { Transform = transform }; instance.Children.Add(SolarBodyAssets.Load(body.Id)); models.Children.Add(instance);
            }
            viewport.Camera = camera; viewport.Children.Add(new ModelVisual3D { Content = models });
            RenderOptions.SetEdgeMode(viewport, EdgeMode.Unspecified);
            backdrop = new Overlay(this, false); overlay = new Overlay(this, true);
            Children.Add(backdrop); Children.Add(viewport); Children.Add(overlay);
            timer.Tick += Tick;
            Loaded += (_, _) => { loaded = true; SystemParameters.StaticPropertyChanged += ParametersChanged; UpdateTimer(); Refresh(); };
            Unloaded += (_, _) =>
            {
                loaded = false; SystemParameters.StaticPropertyChanged -= ParametersChanged;
                UpdateTimer(); drag = null; ReleaseMouseCapture();
            };
            IsVisibleChanged += (_, _) => UpdateTimer();
            IsEnabledChanged += (_, _) => UpdateTimer();
            SizeChanged += (_, _) => { text.Clear(); if (!moving) SnapCamera(); Refresh(); };
            MouseLeftButtonDown += Down; MouseMove += Move; MouseLeftButtonUp += Up;
            LostMouseCapture += (_, _) => drag = null;
            MouseWheel += Wheel; KeyDown += Key;
            ManipulationStarting += (_, e) =>
            {
                e.ManipulationContainer = this;
                e.Mode = ManipulationModes.Translate | ManipulationModes.Scale | ManipulationModes.Rotate;
                Focus(); e.Handled = true;
            };
            ManipulationDelta += (_, e) =>
            {
                yaw += e.DeltaManipulation.Translation.X * .008 + e.DeltaManipulation.Rotation * Math.PI / 180;
                tilt = Math.Clamp(tilt + e.DeltaManipulation.Translation.Y * .002, .16, .67);
                ChangeZoom(Math.Sqrt(e.DeltaManipulation.Scale.X * e.DeltaManipulation.Scale.Y), false);
                e.Handled = true;
            };
        }

        private bool CanAnimate => motion && loaded && IsVisible && IsEnabled && SystemParameters.ClientAreaAnimation;
        private void ParametersChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(SystemParameters.ClientAreaAnimation)) UpdateTimer();
        }
        internal void SetMotion(bool enabled) { motion = enabled; UpdateTimer(); }
        private void UpdateTimer()
        {
            if (CanAnimate)
            {
                if (timer.IsEnabled) return;
                lastTime = 0; clock.Restart(); timer.Start();
            }
            else
            {
                PauseTimer();
                // Reparenting a dashboard temporarily unloads this view. Preserve its camera so a requested
                // ship-to-location transition can resume when the same view is attached again.
                if (!motion || !SystemParameters.ClientAreaAnimation) SnapCamera();
                Refresh();
            }
        }
        private void PauseTimer() { timer.Stop(); clock.Stop(); }
        private void SnapCamera()
        {
            moving = false; zoom = targetZoom;
            var p = Position(Bodies.First(b => b.Id == selected)); targetPan = new Vector(-p.X, -p.Y); pan = targetPan;
        }
        private void Tick(object? sender, EventArgs e)
        {
            if (!CanAnimate) { UpdateTimer(); return; }
            var now = clock.Elapsed.TotalSeconds;
            var delta = Math.Clamp(now - lastTime, 0, .15); lastTime = now; time += delta;
            frames++;
            if (drag is null) yaw = Math.IEEERemainder(yaw + delta * .009, Math.Tau);
            if (selected != "Sun")
            {
                var focused = Position(Bodies.First(b => b.Id == selected));
                targetPan = new Vector(-focused.X, -focused.Y);
                if (!moving) pan = targetPan;
            }
            if (moving)
            {
                var blend = 1 - Math.Exp(-delta * 7);
                zoom += (targetZoom - zoom) * blend; pan += (targetPan - pan) * blend;
                if (Math.Abs(zoom - targetZoom) < .002 && (pan - targetPan).Length < .001)
                { zoom = targetZoom; pan = targetPan; moving = false; }
            }
            Refresh();
        }

        internal void FocusBody(string id, bool animate)
        {
            var body = Bodies.FirstOrDefault(b => b.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
            if (body is null) throw new ArgumentException("Unknown solar-system body.", nameof(id));
            selected = body.Id;
            if (body.Id == "Sun") { yaw = -.24; tilt = .35; }
            targetZoom = body.Id == "Sun" ? 1 : body.Id == "Medina" ? 2.2 : body.Parent is not null ? 4.8 : body.Planet ? 3.3 : 3.8;
            var p = Position(body); targetPan = new Vector(-p.X, -p.Y);
            moving = animate && motion && SystemParameters.ClientAreaAnimation;
            if (!moving) SnapCamera();
            if (!checking) Selected?.Invoke(selected);
            Refresh();
        }

        private Point Position(Body body)
        {
            // Medina is outside Sol; its separate inset is deliberately not on a planetary orbit.
            if (body.Id == "Medina") return new Point(1.38, -.78);
            if (body.Id == "Sun") return new Point();
            var speed = body.Station ? 0 : .007 / Math.Pow(Math.Max(.25, body.Radius), .45);
            var a = body.Angle + time * speed;
            var radius = OrbitRadius(body);
            var point = new Point(radius * Math.Cos(a), radius * Math.Sin(a));
            if (body.Parent is not null)
            {
                var parent = Position(Bodies.First(b => b.Id == body.Parent));
                point.Offset(parent.X, parent.Y);
            }
            return point;
        }
        private double Flatten => Math.Clamp(ActualHeight / Math.Max(1, ActualWidth) * .92, .20, .49);
        private double Scale => Math.Max(1, Math.Min(ActualWidth / 2.95, ActualHeight / (2.8 * Flatten + .18)));
        private Point Project(Point p)
        {
            var x = p.X + pan.X; var y = p.Y + pan.Y;
            var c = Math.Cos(yaw); var s = Math.Sin(yaw);
            return new Point(ActualWidth * .47 + (x * c - y * s) * Scale * zoom,
                ActualHeight * .49 + (x * s + y * c) * Scale * zoom * Flatten * tilt / .35);
        }

        private double BodyRadius(Body body)
        {
            double radius = Math.Clamp(body.Size * Math.Pow(zoom, .55), 2, 29);
            if (body.Id == selected && body.Id != "Sun") radius = Math.Max(radius, Math.Min(32, Math.Min(ActualWidth, ActualHeight) * .16));
            if (body.Parent is not null && body.Id != selected && body.Parent != selected && zoom < 1.8) radius = Math.Min(radius, 2.5);
            return radius;
        }
        private double OrbitRadius(Body body)
        {
            if (body.Parent is null || body.Parent != selected && body.Id != selected) return body.Radius;
            var parent = Bodies.First(b => b.Id == body.Parent);
            double parentExtent = BodyRadius(parent) * (parent.Id == "Saturn" ? 2.41 : 1);
            // In this explicitly schematic diagram, leave space around an enlarged selection.
            return Math.Max(body.Radius, (parentExtent + BodyRadius(body) + 9) / (Scale * zoom * Flatten * tilt / .35));
        }
        private static Rect MedinaArea(Point p) => new(p.X - 98, p.Y - 28, 196, 56);
        private void Refresh()
        {
            if (ActualWidth < 2 || ActualHeight < 2 || overlay is null) return;
            camera.Width = ActualWidth;
            foreach (var body in Bodies)
            {
                var p = Project(Position(body)); double radius = BodyRadius(body);
                if (body.Id == "Medina") { var area = MedinaArea(p); p = new(area.Left + 28, area.Top + 28); radius = 20; }
                var matrix = Matrix3D.Identity;
                double pitch = body.Id == "SolGate" ? 65 : body.Id == "Tycho" ? 40 : body.Id == "Medina" ? -18 : 0;
                matrix.Rotate(new Quaternion(new Vector3D(1, 0, 0), pitch));
                matrix.Rotate(new Quaternion(new Vector3D(0, 1, 0), body.Station ? -25 + Math.Sin(time * .08) * 8 : 35 + time * 2));
                matrix.Scale(new Vector3D(radius, radius, radius)); matrix.Translate(new Vector3D(p.X - ActualWidth / 2, ActualHeight / 2 - p.Y, 0));
                transforms[body.Id].Matrix = matrix;
            }
            backdrop.InvalidateVisual(); overlay.InvalidateVisual();
        }
        private void Paint(DrawingContext dc, bool foreground)
        {
            if (ActualWidth < 2 || ActualHeight < 2) return;
            dc.DrawRectangle(Brushes.Transparent, null, new Rect(RenderSize));
            if (!foreground)
            {
                PaintOrbits(dc);
                return;
            }
            hitTargets.Clear(); labelBounds.Clear(); labelTargets.Clear();
            foreach (var body in Bodies.OrderBy(b => Project(Position(b)).Y))
            {
                var p = Project(Position(body)); var radius = BodyRadius(body);
                hitTargets.Add((body, p, body.Id == "Saturn" ? radius * 2.41 : radius));
                if (p.X < -100 || p.Y < -70 || p.X > ActualWidth + 100 || p.Y > ActualHeight + 70) continue;
                DrawBody(dc, body, p, radius);
            }
            foreach (var hit in hitTargets.OrderBy(h => h.Body.Id == selected ? 0 : h.Body.Parent is null ? 1 : 2))
            {
                if (hit.Body.Id == "Medina") continue;
                if (hit.Body.Parent is not null && hit.Body.Id != selected && hit.Body.Parent != selected && zoom < 1.8) continue;
                DrawLabel(dc, hit.Body, hit.Point, hit.Radius);
            }
            DrawDirectionMarker(dc);
        }
        private void PaintOrbits(DrawingContext dc)
        {
            foreach (var star in stars)
            {
                dc.PushOpacity(star.Opacity); dc.DrawEllipse(Ink, null,
                    new Point(star.Point.X * ActualWidth, star.Point.Y * ActualHeight), star.Radius, star.Radius); dc.Pop();
            }
            DrawBelt(dc);
            foreach (var body in Bodies.Where(b => b.Planet)) DrawOrbit(dc, body.Radius, body.Id == selected ? OrbitBright : Orbit);
            foreach (var body in Bodies.Where(b => b.Parent is not null && (b.Id == selected || b.Parent == selected)))
                DrawOrbit(dc, OrbitRadius(body), Orbit, Position(Bodies.First(b => b.Id == body.Parent)));
            var ring = Project(Position(Bodies.First(b => b.Id == "SolGate")));
            var medina = Project(Position(Bodies.First(b => b.Id == "Medina")));
            dc.DrawLine(Link, ring, medina);
            dc.DrawRoundedRectangle(RingSpaceBackground, RingSpaceBorder, MedinaArea(medina), 4, 4);
        }

        private void DrawOrbit(DrawingContext dc, double radius, System.Windows.Media.Pen pen, Point center = default)
        {
            var geometry = new StreamGeometry();
            using (var context = geometry.Open())
            {
                var initial = Project(new Point(center.X + radius, center.Y)); context.BeginFigure(initial, false, true);
                for (var i = 1; i < 96; i++)
                {
                    var a = i * Math.Tau / 96;
                    context.LineTo(Project(new Point(center.X + radius * Math.Cos(a), center.Y + radius * Math.Sin(a))), true, false);
                }
            }
            geometry.Freeze(); dc.DrawGeometry(null, pen, geometry);
        }

        private void DrawBelt(DrawingContext dc)
        {
            dc.PushOpacity(.28);
            for (var i = 0; i < 128; i++)
            {
                var a = i * 2.39996323 + time * .0018;
                var r = .475 + (i % 9) * .007;
                var p = Project(new Point(r * Math.Cos(a), r * Math.Sin(a)));
                if (p.X > 0 && p.Y > 0 && p.X < ActualWidth && p.Y < ActualHeight)
                    dc.DrawEllipse(FaintInk, null, p, .45 + i % 3 * .15, .45 + i % 3 * .15);
            }
            dc.Pop();
        }

        private void DrawBody(DrawingContext dc, Body body, Point p, double r)
        {
            var active = body.Id == selected;
            if (body.Id == "Medina")
            {
                var area = MedinaArea(p);
                DrawText(dc, "RING SPACE / SEPARATE", new Point(area.Left + 55, area.Top + 7), 8, Amber);
                DrawText(dc, "MEDINA STATION", new Point(area.Left + 55, area.Top + 20), 10, active ? Ink : Amber);
                DrawText(dc, "NAUVOO / BEHEMOTH", new Point(area.Left + 55, area.Top + 36), 7, FaintInk);
                labelBounds.Add(area); labelTargets.Add((body, area)); return;
            }
            if (active)
            {
                dc.DrawEllipse(null, Highlight, p, r + 6, r + 6);
                dc.DrawLine(Highlight, new Point(p.X - r - 12, p.Y), new Point(p.X - r - 4, p.Y));
                dc.DrawLine(Highlight, new Point(p.X + r + 4, p.Y), new Point(p.X + r + 12, p.Y));
            }
            if (body.Id == "Sun")
            {
                dc.PushOpacity(.06); dc.DrawEllipse(Amber, null, p, r * 3.5, r * 3.5); dc.Pop();
                dc.PushOpacity(.08); dc.DrawEllipse(Amber, null, p, r * 2.1, r * 2.1); dc.Pop();
            }
            if (body.Id == "SolGate")
            {
                // Preserve the original diagram's legible portal marker at small sizes.
                dc.DrawEllipse(null, OrbitBright, p, r * .96, r * .96);
            }
        }

        private void DrawLabel(DrawingContext dc, Body body, Point p, double radius)
        {
            var active = body.Id == selected;
            var label = body.Id == "Tycho" ? "TYCHO · MOBILE" : body.Label.ToUpperInvariant();
            var size = active ? 11 : ActualWidth < 420 ? 8 : 9;
            var formatted = GetText(label, size, active ? Ink : FaintInk);
            var labelWidth = formatted.Width + 8; var labelHeight = formatted.Height + 3;
            foreach (var side in new[] { 1, -1, 2, -2 })
            {
                var x = p.X + radius + 6;
                var y = p.Y - labelHeight / 2;
                if (side == -1) x = p.X - radius - 6 - labelWidth;
                if (side == 2) { x = p.X - labelWidth / 2; y = p.Y + radius + 5; }
                if (side == -2) { x = p.X - labelWidth / 2; y = p.Y - radius - labelHeight - 5; }
                var rect = new Rect(x, y, labelWidth, labelHeight);
                if (rect.Left < 3 || rect.Top < 2 || rect.Right > ActualWidth - 3 || rect.Bottom > ActualHeight - 2) continue;
                if (labelBounds.Any(r => r.IntersectsWith(rect))) continue;
                if (!active && hitTargets.Any(h => h.Body.Id != body.Id
                    && new Rect(h.Point.X - h.Radius, h.Point.Y - h.Radius, h.Radius * 2, h.Radius * 2).IntersectsWith(rect))) continue;
                dc.DrawRoundedRectangle(LabelBackground, null, rect, 2, 2);
                dc.DrawText(formatted, new Point(x + 4, y + 1));
                if (active) dc.DrawLine(Leader, p, new Point(Math.Clamp(p.X, rect.Left, rect.Right), Math.Clamp(p.Y, rect.Top, rect.Bottom)));
                labelBounds.Add(rect); labelTargets.Add((body, rect)); return;
            }
        }

        private void DrawDirectionMarker(DrawingContext dc)
        {
            var p = new Point(14, ActualHeight - 13);
            dc.DrawLine(Leader, p, new Point(p.X + 21, p.Y));
            dc.DrawLine(Leader, p, new Point(p.X + Math.Cos(yaw) * 13, p.Y - 11));
        }
        private FormattedText GetText(string value, double size, Brush ink)
        {
            var key = value + "|" + size.ToString(CultureInfo.InvariantCulture) + "|" + (ReferenceEquals(ink, Ink) ? "I" : ReferenceEquals(ink, Amber) ? "A" : "F");
            if (text.TryGetValue(key, out var cached)) return cached;
            var result = new FormattedText(value, CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
                Typeface, size, ink, VisualTreeHelper.GetDpi(this).PixelsPerDip);
            text.Add(key, result); return result;
        }
        private void DrawText(DrawingContext dc, string value, Point point, double size, Brush ink) => dc.DrawText(GetText(value, size, ink), point);

        private void Down(object sender, MouseButtonEventArgs e)
        {
            Focus(); drag = e.GetPosition(this); dragStart = drag.Value; CaptureMouse(); e.Handled = true;
        }
        private void Move(object sender, MouseEventArgs e)
        {
            if (drag is not { } previous || e.LeftButton != MouseButtonState.Pressed) return;
            var next = e.GetPosition(this); var delta = next - previous; drag = next;
            yaw += delta.X * .008; tilt = Math.Clamp(tilt + delta.Y * .002, .16, .67); moving = false;
            Refresh(); e.Handled = true;
        }
        private void Up(object sender, MouseButtonEventArgs e)
        {
            if (drag is null) return;
            var point = e.GetPosition(this); var click = (point - dragStart).Length < 5;
            drag = null; ReleaseMouseCapture();
            if (click)
            {
                var labelHit = labelTargets.FirstOrDefault(h => h.Bounds.Contains(point));
                if (labelHit.Body is not null) FocusBody(labelHit.Body.Id, true);
                else
                {
                    var hit = hitTargets.Where(h => (h.Point - point).Length < Math.Max(12, h.Radius + 7))
                        .OrderBy(h => (h.Point - point).Length).FirstOrDefault();
                    if (hit.Body is not null) FocusBody(hit.Body.Id, true);
                }
            }
            e.Handled = true;
        }
        private void Wheel(object sender, MouseWheelEventArgs e)
        {
            ChangeZoom(Math.Pow(1.001, e.Delta), true); e.Handled = true;
        }
        private void ChangeZoom(double factor, bool animate)
        {
            if (!double.IsFinite(factor) || factor <= 0) return;
            targetZoom = Math.Clamp(targetZoom * factor, .65, 6);
            moving = animate && CanAnimate; if (!moving) SnapCamera();
            Refresh();
        }
        private void Key(object sender, KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Home) FocusBody("Sun", true);
            else if (e.Key is System.Windows.Input.Key.Left or System.Windows.Input.Key.Right) yaw += e.Key == System.Windows.Input.Key.Left ? -.12 : .12;
            else if (e.Key is System.Windows.Input.Key.Up or System.Windows.Input.Key.Down)
                tilt = Math.Clamp(tilt + (e.Key == System.Windows.Input.Key.Up ? .04 : -.04), .16, .67);
            else if (e.Key is System.Windows.Input.Key.Add or System.Windows.Input.Key.OemPlus or System.Windows.Input.Key.Subtract or System.Windows.Input.Key.OemMinus)
            {
                ChangeZoom(e.Key is System.Windows.Input.Key.Add or System.Windows.Input.Key.OemPlus ? 1.15 : 1 / 1.15, true);
            }
            else return;
            Refresh(); e.Handled = true;
        }

        internal void Check()
        {
            var previous = (Selected: selected, Motion: motion, Moving: moving, Zoom: zoom, TargetZoom: targetZoom,
                Pan: pan, TargetPan: targetPan, Yaw: yaw, Tilt: tilt);
            checking = true;
            try
            {
                SolarBodyAssets.Check();
                SetMotion(false);
                if (MotionActive) throw new InvalidOperationException("Solar-system animation continued while disabled.");
                foreach (var body in Bodies)
                {
                    FocusBody(body.Id, false);
                    var position = Position(body); var point = Project(position);
                    if (!double.IsFinite(point.X) || !double.IsFinite(point.Y) || !double.IsFinite(zoom) || zoom is < .65 or > 6)
                        throw new InvalidOperationException("Solar-system focus produced invalid projection bounds.");
                    if ((pan + (Vector)position).Length > .0001) throw new InvalidOperationException("Solar-system focus missed its body.");
                    if (body.Parent is not null && !Bodies.Any(parent => parent.Id == body.Parent && parent.Planet))
                        throw new InvalidOperationException("Solar-system moon has no planetary parent.");
                }
                if (Bodies.Select(b => b.Id).Distinct().Count() != Bodies.Length || Bodies.First(b => b.Id == "Medina").Planet)
                    throw new InvalidOperationException("Solar-system body identities or Ring-space separation are invalid.");
                ChangeZoom(1e6, false);
                if (zoom != 6) throw new InvalidOperationException("Solar-system zoom exceeded its upper bound.");
                ChangeZoom(1e-6, false);
                if (zoom != .65) throw new InvalidOperationException("Solar-system zoom exceeded its lower bound.");
                FocusBody("Sun", false);
                motion = true;
                FocusBody("Mars", true);
                if (SystemParameters.ClientAreaAnimation)
                {
                    if (!moving || zoom != 1 || pan != default)
                        throw new InvalidOperationException("Solar-system focus did not preserve its initial camera for transition.");
                    PauseTimer();
                    if (MotionActive || !moving || zoom != 1 || pan != default)
                        throw new InvalidOperationException("Pausing the solar-system view altered its pending camera transition.");
                }
                SetMotion(false);
                if (moving || zoom != targetZoom || pan != targetPan)
                    throw new InvalidOperationException("Disabling solar-system motion failed to finish its camera transition.");
            }
            finally
            {
                selected = previous.Selected; motion = previous.Motion; moving = previous.Moving;
                zoom = previous.Zoom; targetZoom = previous.TargetZoom; pan = previous.Pan; targetPan = previous.TargetPan;
                yaw = previous.Yaw; tilt = previous.Tilt; checking = false;
                UpdateTimer(); Refresh();
            }
        }

        internal object Diagnose() => new
        {
            selected, scene = selected == "Medina" ? "RingSpaceInset" : "SolSchematic",
            frameCount = frames, renderActive = MotionActive, schematic = true, toScale = false,
            labelOverlaps = labelBounds.SelectMany((a, i) => labelBounds.Skip(i + 1).Select(b => a.IntersectsWith(b))).Count(overlap => overlap),
            selectedRadiusPixels = BodyRadius(Bodies.First(b => b.Id == selected)),
            gateOutsideUranusInsideNeptune = Bodies.First(b => b.Id == "SolGate").Radius > Bodies.First(b => b.Id == "Uranus").Radius
                && Bodies.First(b => b.Id == "SolGate").Radius < Bodies.First(b => b.Id == "Neptune").Radius,
            assets = SolarBodyAssets.Sources
        };
        private sealed class Overlay(MapSurface owner, bool foreground) : FrameworkElement
        {
            protected override void OnRender(DrawingContext drawingContext) => owner.Paint(drawingContext, foreground);
        }

        private static SolidColorBrush Frozen(Color colour) { var brush = new SolidColorBrush(colour); brush.Freeze(); return brush; }
        private static System.Windows.Media.Pen Pen(Color colour, double thickness)
        { var pen = new System.Windows.Media.Pen(Frozen(colour), thickness); pen.Freeze(); return pen; }
        private static System.Windows.Media.Pen DashedPen()
        {
            var pen = new System.Windows.Media.Pen(Frozen(Color.FromArgb(96, 107, 202, 222)), .7)
            { DashStyle = new DashStyle([3, 6], 0) }; pen.Freeze(); return pen;
        }
    }
}
