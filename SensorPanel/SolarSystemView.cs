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

/// <summary>The original navigation diagram, with schematic and physical NASA-vector projections.</summary>
internal sealed class SolarSystemView : Grid
{
    private readonly MapSurface map;
    private readonly TextBlock heading;
    private readonly ComboBox selector;
    private readonly TextBlock status;
    private readonly Button scaleMode;
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
        heading = new TextBlock { Text = "SOL / NAVIGATION", Foreground = MapSurface.Ink,
            FontFamily = new FontFamily("Bahnschrift"), FontSize = 11, VerticalAlignment = VerticalAlignment.Center };
        header.Children.Add(heading);
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
        footer.ColumnDefinitions.Add(new() { Width = GridLength.Auto });
        footer.ColumnDefinitions.Add(new() { Width = GridLength.Auto });
        status = new TextBlock { Text = "SCHEMATIC · NOT TO SCALE", FontSize = 9, Foreground = MapSurface.FaintInk,
            FontFamily = new FontFamily("Bahnschrift"), TextTrimming = TextTrimming.CharacterEllipsis };
        footer.Children.Add(status);
        scaleMode = new Button { Content = "SCHEMATIC", FontSize = 9, Padding = new Thickness(5, 1, 5, 1),
            Foreground = MapSurface.Ink, Background = Brushes.Transparent, BorderBrush = MapSurface.FaintInk,
            Margin = new Thickness(3, 0, 3, 0), IsEnabled = map.HasEphemeris,
            ToolTip = "Switch schematic / true scale. True scale uses physical radii and NASA/JPL positions; dots are selection markers." };
        AutomationProperties.SetName(scaleMode, "Navigation scale mode");
        scaleMode.Click += (_, _) => SetTrueScale(!map.TrueScale);
        Grid.SetColumn(scaleMode, 1); footer.Children.Add(scaleMode);
        var rate = new ComboBox { Width = 67, FontSize = 9, Margin = new Thickness(0, 0, 3, 0),
            Foreground = Brushes.White, Background = new SolidColorBrush(Color.FromRgb(12, 36, 49)),
            ToolTip = "Simulation speed. 6 H/S = six simulated hours each second. Does not affect monitoring." };
        NavigationControls.Apply(rate); AutomationProperties.SetName(rate, "Orbital time speed");
        foreach (var (label, seconds) in new[] { ("PAUSED", 0d), ("1×", 1d), ("6 H/S", 21600d), ("4 D/S", 345600d) })
            rate.Items.Add(new ComboBoxItem { Content = label, Tag = seconds, Foreground = Brushes.White,
                Background = new SolidColorBrush(Color.FromRgb(12, 36, 49)) });
        rate.SelectedIndex = 2;
        rate.SelectionChanged += (_, _) => { if (rate.SelectedItem is ComboBoxItem { Tag: double seconds }) map.SetTimeRate(seconds); };
        Grid.SetColumn(rate, 2); footer.Children.Add(rate);
        var reset = new Button { Content = "RESET", Foreground = MapSurface.Ink, Background = Brushes.Transparent,
            BorderBrush = MapSurface.FaintInk, BorderThickness = new Thickness(.5), Padding = new Thickness(7, 1, 7, 1),
            FontSize = 9, ToolTip = "Whole system · Home key" };
        reset.Click += (_, _) => FocusBody("Sun"); Grid.SetColumn(reset, 3); footer.Children.Add(reset);
        Grid.SetRow(footer, 2); Children.Add(footer);
        map.Selected += id => { SyncSelection(); BodySelected?.Invoke(id); };
        map.StatusChanged += SyncStatus;
        selector.SelectionChanged += (_, _) =>
        {
            if (!selecting && selector.SelectedItem is ComboBoxItem { Tag: string id }) map.FocusBody(id, true);
        };
        SyncSelection();
    }

    public void FocusBody(string id, bool animate = true) => map.FocusBody(id, animate);
    public void SetMotion(bool enabled) => map.SetMotion(enabled);
    internal void SetGraphicsMode(ExpanseGraphicsMode mode)=>map.SetGraphicsMode(mode);
    internal void SetContext(string context) => heading.ToolTip = context;
    internal void SetTrueScale(bool enabled) { map.SetTrueScale(enabled); SyncStatus(); }

    private void SyncSelection()
    {
        selecting = true;
        selector.SelectedIndex = Array.FindIndex(MapSurface.Bodies, b => b.Id == map.SelectedBodyId);
        selecting = false;
        SyncStatus();
    }
    private void SyncStatus()
    {
        scaleMode.Content = map.TrueScale ? "TRUE SCALE" : "SCHEMATIC";
        status.Text = map.SelectedBodyId == "Medina" ? "RING SPACE · SEPARATE FROM SOL"
            : map.SelectedBodyId == "Tycho" ? "TYCHO · MOBILE BELT STATION · SCHEMATIC"
            : map.SelectedBodyId == "SolGate" ? "SOL GATE · FICTIONAL · ~2 AU BEYOND URANUS"
            : !map.HasEphemeris ? "SCHEMATIC · NASA DATA UNAVAILABLE"
            : (map.TrueScale ? "AU / KM · " : "NOT TO SCALE · ") + map.UtcLabel;
        status.ToolTip = map.DataDescription;
    }

    internal void Check() => map.Check();
    internal object ShowJupiterPass(bool nearSide) => map.ShowJupiterPass(nearSide);
    internal JsonElement Diagnose() => JsonSerializer.SerializeToElement(map.Diagnose());
    internal void SetTimeRateForReview(double seconds)=>map.SetTimeRate(seconds);
    internal void PrepareEfficiencyFrame(string body,bool trueScale)=>map.PrepareEfficiencyFrame(body,trueScale);
    internal event Action? ReviewDragCompleted { add=>map.ReviewDragCompleted+=value;remove=>map.ReviewDragCompleted-=value; }
    internal object CheckAssets() => new { Visuals = SolarBodyAssets.Check(), Ephemeris = map.CheckEphemeris() };

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
            new("Europa", "Europa", .063, 1.8, 2.5, Color.FromRgb(203, 185, 148), "Jupiter"),
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
        private readonly DispatcherTimer timer = new(DispatcherPriority.Render) { Interval = TimeSpan.FromMilliseconds(100) };
        private readonly Stopwatch clock = new();
        // The containing map already clips its children; avoid a second expensive 3D clip.
        private readonly Viewport3D viewport = new() { IsHitTestVisible = false, ClipToBounds = false };
        private readonly OrthographicCamera camera = new() { Position = new(0, 0, 2000), LookDirection = new(0, 0, -1), UpDirection = new(0, 1, 0), NearPlaneDistance = 1, FarPlaneDistance = 4000 };
        private readonly Model3DGroup scene = new();
        private readonly Dictionary<string, Model3DGroup> models = new();
        private readonly Dictionary<string, double> modelExtents = new();
        private readonly HashSet<string> visibleModels = new();
        private readonly Dictionary<string, MatrixTransform3D> transforms = new();
        private readonly SolarEphemeris? ephemeris;
        private readonly string? ephemerisError;
        private readonly Dictionary<string, Point3D> nasaPositions = new();
        private readonly Dictionary<string, Point3D[]> trails = new();
        private readonly PointLight sunlight = new(Colors.White, new()) { ConstantAttenuation = .65 };
        private readonly SolarEarthLighting earthLighting = new();
        private readonly Overlay backdrop, overlay;
        private readonly BeltLayer beltLayer;
        private readonly StaticMapLayer starLayer,orbitLayer;
        private readonly record struct OrbitView(Size Size,double Yaw,double Tilt,double Zoom,Vector3D Pan,string Selected,bool TrueScale);
        private OrbitView? lastOrbitView;
        private long starPaints,orbitPaints;
        private readonly Dictionary<string, FormattedText> text = new();
        private readonly List<(Body Body, Point Point, double Radius)> hitTargets = new();
        private readonly List<(Body Body, Rect Bounds)> labelTargets = new();
        private readonly List<Rect> labelBounds = new();
        private readonly (Point Point, double Radius, double Opacity)[] stars;
        private bool motion = true, loaded, moving, checking, cameraRendering;
        private double time, lastTime, yaw = -.24, tilt = .35, zoom = 1, targetZoom = 1, jd, timeRate = 21600, statusTime;
        private long frames;
        private long sceneUpdates,backgroundPaints,foregroundPaints,pointerUpdates;
        private long dragStarted,dragSceneStart,dragPaintStart,dragPointerStart;
        private object? lastDrag;
        internal event Action? ReviewDragCompleted;
        private int pathSegments, rejectedPathSegments;
        private double maximumPathCoordinate;
        private Vector3D pan, targetPan;
        private Point? drag;
        private Point? hoverPoint;
        private Point dragStart;
        private string selected = "Sun";
        internal event Action<string>? Selected;
        internal event Action? StatusChanged;
        internal string SelectedBodyId => selected;
        internal bool MotionActive => timer.IsEnabled || cameraRendering;
        internal bool TrueScale { get; private set; }
        internal bool HasEphemeris => ephemeris is not null;
        internal string UtcLabel => SolarEphemeris.Utc(jd).ToString("yyyy-MM-dd HH:mm 'UTC'", CultureInfo.InvariantCulture);
        internal string DataDescription => ephemeris is null ? ephemerisError ?? "NASA data unavailable."
            : "NASA/JPL Horizons geometric vectors, cubic Hermite interpolation. " + ephemeris.Range
                + ". Simulation loops within the bundled range; not a live NASA connection. True scale: physical radii, distances and projected trajectories; dots/labels are markers. Fictional stations are illustrative.";
        internal object? CheckEphemeris() => ephemeris?.Check();

        internal MapSurface()
        {
            Focusable = true; ClipToBounds = true; Cursor = Cursors.Hand; IsManipulationEnabled = true; Background = Brushes.Transparent;
            ephemeris = SolarEphemeris.Load(out ephemerisError);
            jd = ephemeris is null ? SolarEphemeris.JulianDate(DateTimeOffset.UtcNow)
                : Math.Clamp(SolarEphemeris.JulianDate(DateTimeOffset.UtcNow), ephemeris.StartJd, ephemeris.StopJd);
            UpdateEphemeris();
            AutomationProperties.SetHelpText(this, "Hover to identify a body. Select it here or from Navigation destination. Drag to rotate, wheel to zoom, Home to reset.");
            var random = new Random(1977);
            stars = Enumerable.Range(0, 70).Select(_ => (new Point(random.NextDouble(), random.NextDouble()),
                .35 + random.NextDouble() * .65, .10 + random.NextDouble() * .23)).ToArray();
            scene.Children.Add(new AmbientLight(Color.FromRgb(35, 40, 51)));
            scene.Children.Add(sunlight);
            foreach (var body in Bodies)
            {
                var transform = new MatrixTransform3D(); transforms.Add(body.Id, transform);
                var instance = new Model3DGroup { Transform = transform };
                var detail = SolarBodyAssets.Load(body.Id);
                if (body.Id == "Earth")
                {
                    instance.Children.Add(earthLighting.Model);
                    foreach (var layer in detail.Children.Skip(1)) instance.Children.Add(layer);
                }
                else instance.Children.Add(detail);
                models.Add(body.Id, instance);
                var bounds = detail.Bounds;
                modelExtents.Add(body.Id, Math.Sqrt(Math.Pow(Math.Max(Math.Abs(bounds.X), Math.Abs(bounds.X + bounds.SizeX)), 2)
                    + Math.Pow(Math.Max(Math.Abs(bounds.Y), Math.Abs(bounds.Y + bounds.SizeY)), 2)
                    + Math.Pow(Math.Max(Math.Abs(bounds.Z), Math.Abs(bounds.Z + bounds.SizeZ)), 2)));
            }
            viewport.Camera = camera; viewport.Children.Add(new ModelVisual3D { Content = scene });
            RenderOptions.SetEdgeMode(viewport, EdgeMode.Unspecified);
            backdrop = new Overlay(this, false); overlay = new Overlay(this, true);
            starLayer=new StaticMapLayer(this,true);orbitLayer=new StaticMapLayer(this,false);
            beltLayer=new BeltLayer(this);
            Children.Add(starLayer);Children.Add(beltLayer);Children.Add(orbitLayer);Children.Add(backdrop);Children.Add(viewport);Children.Add(overlay);
            timer.Tick += Tick;
            Loaded += (_, _) => { loaded = true; SystemParameters.StaticPropertyChanged += ParametersChanged; UpdateTimer(); Refresh(); };
            Unloaded += (_, _) =>
            {
                loaded = false; SystemParameters.StaticPropertyChanged -= ParametersChanged;
                UpdateTimer(); drag = null; hoverPoint = null; ReleaseMouseCapture();
            };
            IsVisibleChanged += (_, _) => UpdateTimer();
            IsEnabledChanged += (_, _) => UpdateTimer();
            SizeChanged += (_, _) => { text.Clear(); if (!moving) SnapCamera(); Refresh(); };
            MouseLeftButtonDown += Down; MouseMove += Move; MouseLeftButtonUp += Up;
            MouseLeave += (_, _) => { hoverPoint = null; overlay.InvalidateVisual(); };
            LostMouseCapture += (_, _) => drag = null;
            MouseWheel += Wheel; KeyDown += Key;
            ManipulationStarting += (_, e) =>
            {
                e.ManipulationContainer = this;
                e.Mode = ManipulationModes.Translate | ManipulationModes.Scale | ManipulationModes.Rotate;
                hoverPoint = null; Focus(); e.Handled = true;
            };
            ManipulationDelta += (_, e) =>
            {
                yaw += e.DeltaManipulation.Translation.X * .008 + e.DeltaManipulation.Rotation * Math.PI / 180;
                tilt = Math.Clamp(tilt + e.DeltaManipulation.Translation.Y * .002, .16, .67);
                ChangeZoom(Math.Sqrt(e.DeltaManipulation.Scale.X * e.DeltaManipulation.Scale.Y), false);
                e.Handled = true;
            };
        }

        private bool efficient;
        internal void SetGraphicsMode(ExpanseGraphicsMode mode){efficient=mode==ExpanseGraphicsMode.Efficient;UpdateTimer();}
        private bool CanAnimate => motion && loaded && IsVisible && IsEnabled && SystemParameters.ClientAreaAnimation;
        private void ParametersChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(SystemParameters.ClientAreaAnimation)) UpdateTimer();
        }
        internal void SetMotion(bool enabled) { motion = enabled; UpdateTimer(); }
        internal void SetTimeRate(double seconds) { timeRate = seconds; UpdateTimer();StatusChanged?.Invoke(); }
        internal void SetTrueScale(bool enabled)
        {
            if (enabled && ephemeris is null) return;
            TrueScale = enabled; trails.Clear(); FocusBody(selected, false);
        }
        private void UpdateEphemeris()
        {
            nasaPositions.Clear();
            if (ephemeris is null) return;
            foreach (var body in Bodies.Where(b => !b.Station)) nasaPositions[body.Id] = ephemeris.Position(body.Id, jd);
        }
        private void UpdateTimer()
        {
            if (CanAnimate)
            {
                UpdateCadence();
                if(!MotionActive)Refresh();
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
        private void UpdateCadence()
        {
            if(!CanAnimate||(timeRate<=0&&!moving)){PauseTimer();return;}
            if(!MotionActive){lastTime=0;clock.Restart();}
            var interval = TimeSpan.FromMilliseconds(moving ? 1000d / 60 : 100);
            if (timer.Interval != interval) timer.Interval = interval;
            bool interactive = (!efficient||moving) && CanAnimate;
            if (interactive)
            {
                timer.Stop();
                if(!cameraRendering){CompositionTarget.Rendering += CameraFrame;cameraRendering = true;}
            }
            else
            {
                if(cameraRendering){CompositionTarget.Rendering -= CameraFrame;cameraRendering = false;}
                if(!timer.IsEnabled)timer.Start();
            }
        }
        private void CameraFrame(object? sender, EventArgs e)
        {
            // Full Fidelity follows composition frames without an app-imposed
            // cap. Efficient keeps immediate input and up-to-60 Hz transitions.
            if (!efficient||clock.Elapsed.TotalSeconds - lastTime >= 1d / 60 - .0005) Tick(sender, e);
        }
        private void PauseTimer()
        {
            timer.Stop(); clock.Stop();
            if (cameraRendering) { CompositionTarget.Rendering -= CameraFrame; cameraRendering = false; }
        }
        private void SnapCamera()
        {
            moving = false; zoom = targetZoom;
            var p = Position(Bodies.First(b => b.Id == selected)); targetPan = -(Vector3D)p; pan = targetPan;
        }
        private void Tick(object? sender, EventArgs e)
        {
            if (!CanAnimate) { UpdateTimer(); return; }
            var now = clock.Elapsed.TotalSeconds;
            var delta = Math.Clamp(now - lastTime, 0, .15); lastTime = now;
            if (timeRate > 0) time += delta;
            if (ephemeris is not null && timeRate > 0)
            {
                jd += delta * timeRate / 86400;
                if (jd > ephemeris.StopJd) { jd = ephemeris.StartJd + (jd - ephemeris.StopJd); trails.Clear(); }
                UpdateEphemeris();
            }
            frames++;
            if (time - statusTime > .5) { statusTime = time; StatusChanged?.Invoke(); }
            if (selected != "Sun")
            {
                var focused = Position(Bodies.First(b => b.Id == selected));
                targetPan = -(Vector3D)focused;
                if (!moving) pan = targetPan;
            }
            if (moving)
            {
                var blend = 1 - Math.Exp(-delta * 7);
                zoom = TrueScale ? zoom * Math.Exp(Math.Log(targetZoom / zoom) * blend) : zoom + (targetZoom - zoom) * blend;
                pan += (targetPan - pan) * blend;
                if (Math.Abs(Math.Log(zoom / targetZoom)) < .001 && (pan - targetPan).Length < .001)
                { zoom = targetZoom; pan = targetPan; moving = false; }
            }
            UpdateCadence();
            Refresh();
        }

        internal void FocusBody(string id, bool animate)
        {
            var body = Bodies.FirstOrDefault(b => b.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
            if (body is null) throw new ArgumentException("Unknown solar-system body.", nameof(id));
            selected = body.Id;
            if (body.Id == "Sun") { yaw = -.24; tilt = .35; }
            targetZoom = body.Id == "Sun" ? 1 : body.Id == "Medina" ? 2.2 : body.Parent is not null ? 4.8 : body.Planet ? 3.3 : 3.8;
            if (TrueScale && !body.Station && body.Id != "Sun")
            {
                double extent = ephemeris!.RadiusKm(body.Id) / SolarEphemeris.AuKm * 8;
                var moons = Bodies.Where(b => b.Parent == body.Id).ToArray();
                if (moons.Length > 0) extent = moons.Max(b => ((Vector3D)ephemeris.Relative(b.Id, jd)).Length) * 1.3;
                targetZoom = Math.Clamp(Math.Min(ActualWidth, ActualHeight) * .32 / (Math.Max(1e-8, extent) / 32 * Scale), .65, 1e8);
            }
            var p = Position(body); targetPan = -(Vector3D)p;
            moving = animate && motion && SystemParameters.ClientAreaAnimation;
            if (!moving) SnapCamera();
            UpdateCadence();
            if (!checking) Selected?.Invoke(selected);
            Refresh();
        }

        private Point3D Position(Body body)
        {
            // Medina is outside Sol; its separate inset is deliberately not on a planetary orbit.
            if (body.Id == "Medina") return new Point3D(1.38, -.78, 0);
            if (body.Id == "Sun") return new Point3D();
            if (TrueScale && !body.Station) return (Point3D)((Vector3D)nasaPositions[body.Id] / 32);
            if (TrueScale && body.Id == "SolGate")
            {
                double gateAu = ((Vector3D)ephemeris!.Position("Uranus", ephemeris.StartJd)).Length + 2;
                return new(gateAu / 32 * Math.Cos(body.Angle), gateAu / 32 * Math.Sin(body.Angle), 0);
            }
            if (TrueScale && body.Id == "Tycho") return new(2.8 / 32 * Math.Cos(body.Angle), 2.8 / 32 * Math.Sin(body.Angle), 0);
            var speed = body.Station ? 0 : .007 / Math.Pow(Math.Max(.25, body.Radius), .45);
            var a = body.Angle + time * speed;
            if (!body.Station && body.Parent is null && nasaPositions.TryGetValue(body.Id, out var physical))
                a = Math.Atan2(physical.Y, physical.X);
            var radius = OrbitRadius(body);
            var point = new Point3D(radius * Math.Cos(a), radius * Math.Sin(a), 0);
            if (body.Parent is not null)
            {
                if (ephemeris is not null)
                {
                    // Use the actual parent-relative NASA direction and period; only spacing is expanded in schematic mode.
                    var relative = ephemeris.Relative(body.Id, jd);
                    double angle = Math.Atan2(relative.Y, relative.X);
                    point = new(radius * Math.Cos(angle), radius * Math.Sin(angle), 0);
                }
                var parent = Position(Bodies.First(b => b.Id == body.Parent));
                point += (Vector3D)parent;
            }
            return point;
        }
        private double Flatten => Math.Clamp(ActualHeight / Math.Max(1, ActualWidth) * .92, .20, .49);
        private double ProjectionTilt => Math.Clamp(Flatten * tilt / .35, .02, .98);
        private double Scale => Math.Max(1, Math.Min(ActualWidth / 2.95, ActualHeight / (2.8 * Flatten + .18)));
        private Point Project(Point3D p)
        {
            var x = p.X + pan.X; var y = p.Y + pan.Y;
            var c = Math.Cos(yaw); var s = Math.Sin(yaw);
            return new Point(ActualWidth * .47 + (x * c - y * s) * Scale * zoom,
                ActualHeight * .49 + ((x * s + y * c) * ProjectionTilt
                    - (p.Z + pan.Z) * Math.Sqrt(1 - ProjectionTilt * ProjectionTilt)) * Scale * zoom);
        }
        private double Depth(Point3D p) => ((p.X + pan.X) * Math.Sin(yaw) + (p.Y + pan.Y) * Math.Cos(yaw))
            * Math.Sqrt(1 - ProjectionTilt * ProjectionTilt) * Scale * zoom + (p.Z + pan.Z) * ProjectionTilt * Scale * zoom;

        private double BodyRadius(Body body)
        {
            if (TrueScale && !body.Station) return ephemeris!.RadiusKm(body.Id) / SolarEphemeris.AuKm / 32 * Scale * zoom;
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
            // Leave room along the major axis, while allowing front transits/back occultations
            // along the foreshortened minor axis. Depth(), not artificial spacing, resolves overlap.
            return Math.Max(body.Radius, (parentExtent + BodyRadius(body) + 12) / (Scale * zoom));
        }
        private static Rect MedinaArea(Point p) => new(p.X - 98, p.Y - 28, 196, 56);
        private bool IntersectsView(Point p, double radius) => p.X + radius >= 0 && p.Y + radius >= 0
            && p.X - radius <= ActualWidth && p.Y - radius <= ActualHeight;
        private void Refresh()
        {
            if (ActualWidth < 2 || ActualHeight < 2 || overlay is null) return;
            sceneUpdates++;
            camera.Width = ActualWidth;
            var sun = Project(new()); sunlight.Position = new(sun.X - ActualWidth / 2, ActualHeight / 2 - sun.Y, Depth(new()));
            double front = 0, back = 0;
            foreach (var body in Bodies)
            {
                var p = Project(Position(body)); double radius = BodyRadius(body);
                if (body.Id == "Medina") { var area = MedinaArea(p); p = new(area.Left + 28, area.Top + 28); radius = 20; }
                var matrix = Matrix3D.Identity;
                double extent = radius * modelExtents[body.Id];
                bool visible = IntersectsView(p, extent) && (!TrueScale || body.Station || radius >= 1);
                if (!visible)
                {
                    // A zero-scale model still costs scene traversal. Detach it until its mesh is visible again.
                    if (visibleModels.Remove(body.Id)) scene.Children.Remove(models[body.Id]);
                    continue;
                }
                if (visibleModels.Add(body.Id)) scene.Children.Add(models[body.Id]);
                double depth = body.Station ? 0 : Depth(Position(body));
                front = Math.Max(front, depth + extent); back = Math.Min(back, depth - extent);
                double pitch = body.Id == "SolGate" ? 65 : body.Id == "Tycho" ? 40 : body.Id == "Medina" ? -18 : 0;
                matrix.Rotate(new Quaternion(new Vector3D(1, 0, 0), pitch));
                matrix.Rotate(new Quaternion(new Vector3D(0, 1, 0), body.Station ? -25 + Math.Sin(time * .08) * 8 : 35 + time * 2));
                if (body.Id == "Earth")
                {
                    var direction = new Vector3D(sun.X - p.X, p.Y - sun.Y, Depth(new()) - Depth(Position(body)));
                    if (direction.LengthSquared < 1e-8) direction = new(1, 0, 0);
                    direction.Normalize(); var inverse = matrix; inverse.Invert();
                    earthLighting.Update(inverse.Transform(direction),(!CanAnimate||timeRate<=0)&&!moving&&drag is null);
                }
                matrix.Scale(new Vector3D(radius, radius, radius)); matrix.Translate(new Vector3D(p.X - ActualWidth / 2, ActualHeight / 2 - p.Y,
                    depth));
                transforms[body.Id].Matrix = matrix;
            }
            // Close physical views must not cut through a large planet at the old fixed 2,000-pixel camera plane.
            camera.Position = new(0, 0, front + 100);
            camera.FarPlaneDistance = front - back + 200;
            NativeRasterCache.Apply(starLayer,true);
            var orbitView=new OrbitView(RenderSize,yaw,tilt,zoom,pan,selected,TrueScale);
            bool orbitChanged=lastOrbitView!=orbitView;
            if(orbitChanged){lastOrbitView=orbitView;orbitLayer.InvalidateVisual();}
            NativeRasterCache.Apply(orbitLayer,!orbitChanged&&!moving&&drag is null);
            beltLayer.InvalidateVisual();backdrop.InvalidateVisual(); overlay.InvalidateVisual();
        }
        private void Paint(DrawingContext dc, bool foreground)
        {
            if (ActualWidth < 2 || ActualHeight < 2) return;
            if(foreground)foregroundPaints++;else backgroundPaints++;
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
                hitTargets.Add((body, p, Math.Max(2, body.Id == "Saturn" ? radius * 2.41 : radius)));
                if (p.X < -100 || p.Y < -70 || p.X > ActualWidth + 100 || p.Y > ActualHeight + 70) continue;
                DrawBody(dc, body, p, radius);
            }
            // The destination list carries the selection name; the map identifies only the body under the pointer.
            if (hoverPoint is { } pointer && drag is null && HitBody(pointer, 3) is { } hit)
                DrawLabel(dc, hit.Body, hit.Point, hit.Radius);
            DrawDirectionMarker(dc);
        }
        private void PaintOrbits(DrawingContext dc)
        {
            if (TrueScale) DrawPhysicalPaths(dc);
            else
            {
                foreach (var body in Bodies.Where(b => b.Parent is not null && (b.Id == selected || b.Parent == selected)))
                    DrawOrbit(dc, OrbitRadius(body), Orbit, Position(Bodies.First(b => b.Id == body.Parent)));
            }
            DrawLighting(dc);
            var ring = Project(Position(Bodies.First(b => b.Id == "SolGate")));
            var medina = Project(Position(Bodies.First(b => b.Id == "Medina")));
            if (ClipLine(ring, medina, new Rect(RenderSize), out var linkStart, out var linkEnd)) dc.DrawLine(Link, linkStart, linkEnd);
            var inset = MedinaArea(medina);
            if (inset.IntersectsWith(new Rect(RenderSize))) dc.DrawRoundedRectangle(RingSpaceBackground, RingSpaceBorder, inset, 4, 4);
        }

        private void DrawOrbit(DrawingContext dc, double radius, System.Windows.Media.Pen pen, Point3D center = default)
        {
            var geometry = new StreamGeometry();
            using (var context = geometry.Open())
            {
                var initial = Project(new Point3D(center.X + radius, center.Y, center.Z)); context.BeginFigure(initial, false, true);
                for (var i = 1; i < 96; i++)
                {
                    var a = i * Math.Tau / 96;
                    context.LineTo(Project(new Point3D(center.X + radius * Math.Cos(a), center.Y + radius * Math.Sin(a), center.Z)), true, false);
                }
            }
            geometry.Freeze(); dc.DrawGeometry(null, pen, geometry);
        }

        private void DrawBelt(DrawingContext dc)
        {
            if (TrueScale) return;
            dc.PushOpacity(.28);
            for (var i = 0; i < 128; i++)
            {
                var a = i * 2.39996323 + time * .0018;
                var r = .475 + (i % 9) * .007;
                var p = Project(new Point3D(r * Math.Cos(a), r * Math.Sin(a), 0));
                if (p.X > 0 && p.Y > 0 && p.X < ActualWidth && p.Y < ActualHeight)
                    dc.DrawEllipse(FaintInk, null, p, .45 + i % 3 * .15, .45 + i % 3 * .15);
            }
            dc.Pop();
        }

        private void DrawPhysicalPaths(DrawingContext dc)
        {
            pathSegments = rejectedPathSegments = 0; maximumPathCoordinate = 0;
            var clip = new Rect(RenderSize);
            foreach (var body in Bodies.Where(b => b.Planet || b.Parent is not null && (b.Id == selected || b.Parent == selected)))
            {
                if (!trails.TryGetValue(body.Id, out var path)) trails.Add(body.Id, path = ephemeris!.Trail(body.Id, jd));
                var parent = body.Parent is null ? new Vector3D() : (Vector3D)ephemeris!.Position(body.Parent, jd);
                var geometry = new StreamGeometry();
                using (var context = geometry.Open())
                {
                    var previous = Project((Point3D)(((Vector3D)path[0] + parent) / 32));
                    for (int i = 1; i < path.Length; i++)
                    {
                        var projected = Project((Point3D)(((Vector3D)path[i] + parent) / 32));
                        if (ClipLine(previous, projected, clip, out var a, out var b))
                        {
                            context.BeginFigure(a, false, false); context.LineTo(b, true, false); pathSegments++;
                            maximumPathCoordinate = Math.Max(maximumPathCoordinate, Math.Max(Math.Max(a.X, a.Y), Math.Max(b.X, b.Y)));
                        }
                        else rejectedPathSegments++;
                        previous = projected;
                    }
                }
                if (!geometry.IsEmpty()) { geometry.Freeze(); dc.DrawGeometry(null, body.Id == selected ? OrbitBright : Orbit, geometry); }
            }
            double auPixels = Scale * zoom / 32;
            double au = Math.Pow(10, Math.Floor(Math.Log10(70 / auPixels)));
            double pixels = au * auPixels;
            if (pixels < 28) { au *= 5; pixels *= 5; }
            var start = new Point(12, ActualHeight - 9);
            dc.DrawLine(Highlight, start, new(start.X + pixels, start.Y));
            string label = au < .01 ? (au * SolarEphemeris.AuKm).ToString("N0", CultureInfo.InvariantCulture) + " KM"
                : au.ToString("0.###", CultureInfo.InvariantCulture) + " AU";
            DrawText(dc, label, new(start.X, start.Y - 14), 8, FaintInk);
        }

        private static bool ClipLine(Point a, Point b, Rect view, out Point start, out Point end)
        {
            double lo = 0, hi = 1, dx = b.X - a.X, dy = b.Y - a.Y;
            bool Edge(double p, double q)
            {
                if (p == 0) return q >= 0;
                double t = q / p;
                if (p < 0) { if (t > hi) return false; lo = Math.Max(lo, t); }
                else { if (t < lo) return false; hi = Math.Min(hi, t); }
                return true;
            }
            start = end = default;
            if (!Edge(-dx, a.X - view.Left) || !Edge(dx, view.Right - a.X)
                || !Edge(-dy, a.Y - view.Top) || !Edge(dy, view.Bottom - a.Y)) return false;
            start = new(Math.Clamp(a.X + lo * dx, view.Left, view.Right), Math.Clamp(a.Y + lo * dy, view.Top, view.Bottom));
            end = new(Math.Clamp(a.X + hi * dx, view.Left, view.Right), Math.Clamp(a.Y + hi * dy, view.Top, view.Bottom));
            return true;
        }

        // Belter's corona sizes/strengths and terrestrial atmosphere colours; native gas-giant rims.
        private static readonly RadialGradientBrush SunCorona = Halo(Color.FromRgb(255, 217, 160), .55);
        private static readonly RadialGradientBrush SunOuter = Halo(Color.FromRgb(255, 217, 160), .22);
        private static readonly Dictionary<string, RadialGradientBrush> Atmospheres = new()
        {
            ["Earth"] = Rim(Color.FromRgb(106, 184, 255), .7), ["Venus"] = Rim(Color.FromRgb(232, 200, 138), .55),
            ["Mars"] = Rim(Color.FromRgb(216, 138, 90), .30), ["Jupiter"] = Rim(Color.FromRgb(226, 190, 150), .25),
            ["Saturn"] = Rim(Color.FromRgb(235, 209, 152), .3), ["Uranus"] = Rim(Color.FromRgb(125, 227, 242), .5),
            ["Neptune"] = Rim(Color.FromRgb(77, 126, 255), .5)
        };
        private static RadialGradientBrush Halo(Color colour, double alpha)
        {
            var brush = new RadialGradientBrush();
            foreach (var (offset, opacity) in new[] { (0d, alpha), (.12, alpha * .9), (.28, alpha * .35), (.65, alpha * .08), (1d, 0d) })
                brush.GradientStops.Add(new(Color.FromArgb((byte)(opacity * 255), colour.R, colour.G, colour.B), offset));
            brush.Freeze(); return brush;
        }
        private static RadialGradientBrush Rim(Color colour, double alpha)
        {
            var brush = new RadialGradientBrush();
            foreach (var (offset, opacity) in new[] { (0d, 0d), (.72, 0d), (.83, alpha * .85), (.89, alpha), (1d, 0d) })
                brush.GradientStops.Add(new(Color.FromArgb((byte)(opacity * 255), colour.R, colour.G, colour.B), offset));
            brush.Freeze(); return brush;
        }
        private void DrawLighting(DrawingContext dc)
        {
            foreach (var body in Bodies.Where(b => b.Id == "Sun" || Atmospheres.ContainsKey(b.Id)))
            {
                var point = Project(Position(body)); double r = BodyRadius(body);
                if (point.X + r * 8 < 0 || point.Y + r * 8 < 0 || point.X - r * 8 > ActualWidth || point.Y - r * 8 > ActualHeight) continue;
                if (body.Id == "Sun")
                {
                    DrawGlow(dc, SunOuter, point, r * 7.5);
                    DrawGlow(dc, SunCorona, point, r * 3.2);
                }
                else if (r > 1.5) DrawGlow(dc, Atmospheres[body.Id], point, r * 1.12);
            }
        }
        private void DrawGlow(DrawingContext dc, RadialGradientBrush source, Point center, double radius)
        {
            var bounds = Rect.Intersect(new Rect(center.X - radius, center.Y - radius, radius * 2, radius * 2), new Rect(RenderSize));
            if (bounds.IsEmpty) return;
            var brush = source.Clone(); brush.MappingMode = BrushMappingMode.Absolute;
            brush.Center = brush.GradientOrigin = center; brush.RadiusX = brush.RadiusY = radius; brush.Freeze();
            dc.DrawRectangle(brush, null, bounds);
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
            if (active && r < Math.Max(ActualWidth, ActualHeight) * 2)
            {
                dc.DrawEllipse(null, Highlight, p, r + 6, r + 6);
                dc.DrawLine(Highlight, new Point(p.X - r - 12, p.Y), new Point(p.X - r - 4, p.Y));
                dc.DrawLine(Highlight, new Point(p.X + r + 4, p.Y), new Point(p.X + r + 12, p.Y));
            }
            if (TrueScale && !body.Station && r < 1.5) dc.DrawEllipse(body.Id == "Sun" ? Amber : FaintInk, null, p, 1.5, 1.5);
            if (body.Id == "SolGate")
            {
                // Preserve the original diagram's legible portal marker at small sizes.
                dc.DrawEllipse(null, OrbitBright, p, r * .96, r * .96);
            }
        }

        private void DrawLabel(DrawingContext dc, Body body, Point p, double radius)
        {
            var label = body.Id == "Tycho" ? "TYCHO · MOBILE" : body.Label.ToUpperInvariant();
            var formatted = GetText(label, ActualWidth < 420 ? 9 : 11, Ink);
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
                if (hitTargets.Any(h => h.Body.Id != body.Id
                    && new Rect(h.Point.X - h.Radius, h.Point.Y - h.Radius, h.Radius * 2, h.Radius * 2).IntersectsWith(rect))) continue;
                dc.DrawRoundedRectangle(LabelBackground, null, rect, 2, 2);
                dc.DrawText(formatted, new Point(x + 4, y + 1));
                dc.DrawLine(Leader, p, new Point(Math.Clamp(p.X, rect.Left, rect.Right), Math.Clamp(p.Y, rect.Top, rect.Bottom)));
                labelBounds.Add(rect); labelTargets.Add((body, rect)); return;
            }
        }

        private void DrawDirectionMarker(DrawingContext dc)
        {
            if (TrueScale) return;
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
            dragStarted=Stopwatch.GetTimestamp();dragSceneStart=sceneUpdates;dragPaintStart=backgroundPaints;dragPointerStart=pointerUpdates;
            hoverPoint = null; overlay.InvalidateVisual();
            Focus(); drag = e.GetPosition(this); dragStart = drag.Value; CaptureMouse(); e.Handled = true;
        }
        private void Move(object sender, MouseEventArgs e)
        {
            if (drag is not { } previous || e.LeftButton != MouseButtonState.Pressed)
            {
                hoverPoint = e.StylusDevice is null ? e.GetPosition(this) : null;
                overlay.InvalidateVisual(); return;
            }
            hoverPoint = null;
            var next = e.GetPosition(this); var delta = next - previous; drag = next;
            pointerUpdates++;
            yaw += delta.X * .008; tilt = Math.Clamp(tilt + delta.Y * .002, .16, .67); moving = false;
            Refresh(); e.Handled = true;
        }
        private void Up(object sender, MouseButtonEventArgs e)
        {
            if (drag is null) return;
            var point = e.GetPosition(this); var click = (point - dragStart).Length < 5;
            double seconds=Stopwatch.GetElapsedTime(dragStarted).TotalSeconds;
            lastDrag=new {Seconds=seconds,PointerUpdates=pointerUpdates-dragPointerStart,SceneUpdates=sceneUpdates-dragSceneStart,
                BackgroundPaints=backgroundPaints-dragPaintStart,
                SceneUpdatesPerSecond=seconds>0?(sceneUpdates-dragSceneStart)/seconds:0};
            drag = null; ReleaseMouseCapture();
            UpdateCadence();Refresh();
            if (click)
            {
                var labelHit = labelTargets.FirstOrDefault(h => h.Bounds.Contains(point));
                if (labelHit.Body is not null) FocusBody(labelHit.Body.Id, true);
                else
                {
                    if (HitBody(point, 7) is { } hit) FocusBody(hit.Body.Id, true);
                }
            }
            e.Handled = true;
            ReviewDragCompleted?.Invoke();
        }
        private (Body Body, Point Point, double Radius)? HitBody(Point point, double padding)
        {
            var hits = hitTargets.Where(h => h.Body.Id != "Medina"
                && h.Point.X >= 0 && h.Point.X <= ActualWidth && h.Point.Y >= 0 && h.Point.Y <= ActualHeight
                && (h.Point - point).Length <= Math.Max(6, h.Radius + padding)).ToArray();
            if (hits.Length == 0) return null;
            // Resolve overlapping discs by their visible front surface, so a hidden moon cannot steal its planet's name or tap.
            var discs = hits.Where(h => (h.Point - point).Length <= h.Radius).ToArray();
            return discs.Length > 0 ? discs.OrderByDescending(h => Depth(Position(h.Body))
                + Math.Sqrt(Math.Max(0, Math.Pow(BodyRadius(h.Body), 2) - (h.Point - point).LengthSquared))).First()
                : hits.OrderBy(h => (h.Point - point).Length).First();
        }
        private void Wheel(object sender, MouseWheelEventArgs e)
        {
            ChangeZoom(Math.Pow(1.001, e.Delta), true); e.Handled = true;
        }
        private void ChangeZoom(double factor, bool animate)
        {
            if (!double.IsFinite(factor) || factor <= 0) return;
            targetZoom = Math.Clamp(targetZoom * factor, .65, TrueScale ? 1e8 : 6);
            moving = animate && CanAnimate; if (!moving) SnapCamera();
            UpdateCadence();
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
                Pan: pan, TargetPan: targetPan, Yaw: yaw, Tilt: tilt, TrueScale, Jd: jd);
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
                    if ((pan + (Vector3D)position).Length > .0001) throw new InvalidOperationException("Solar-system focus missed its body.");
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
                if (ephemeris is null) throw new InvalidOperationException("Bundled NASA ephemeris is unavailable: " + ephemerisError);
                ephemeris.Check(); SetTrueScale(true);
                var clip = new Rect(0, 0, 100, 60);
                if (!ClipLine(new(-1000, 30), new(1000, 30), clip, out var entry, out var exit)
                    || entry != new Point(0, 30) || exit != new Point(100, 30)
                    || ClipLine(new(-1000, -5), new(1000, -5), clip, out _, out _))
                    throw new InvalidOperationException("Viewport clipping lost a crossing trail or admitted an outside trail.");
                FocusBody("Sun", false);
                if (Bodies.Any(b => !b.Station && visibleModels.Contains(b.Id)))
                    throw new InvalidOperationException("Sub-pixel physical meshes remained attached at system scale.");
                FocusBody("Earth", false); ChangeZoom(35, false);
                if (!visibleModels.Contains("Earth") || visibleModels.Count >= Bodies.Length
                    || models.Any(p => scene.Children.Contains(p.Value) != visibleModels.Contains(p.Key))
                    || camera.Position.Z <= BodyRadius(Bodies.First(b => b.Id == "Earth")))
                    throw new InvalidOperationException("Close physical view failed to cull or clipped through Earth.");
                foreach (var body in Bodies.Where(b => !b.Station))
                {
                    FocusBody(body.Id, false);
                    double expected = ephemeris.RadiusKm(body.Id) / SolarEphemeris.AuKm;
                    if (Math.Abs(BodyRadius(body) / (Scale * zoom) * 32 - expected) > 1e-12
                        || ((Vector3D)Position(body) * 32 - (Vector3D)ephemeris.Position(body.Id, jd)).Length > 1e-10)
                        throw new InvalidOperationException("True-scale geometry diverged from NASA data: " + body.Id);
                }
                var gateBefore = Position(Bodies.First(b => b.Id == "SolGate"));
                foreach (var moon in Bodies.Where(b => b.Parent is not null))
                {
                    var before = Position(moon) - Position(Bodies.First(b => b.Id == moon.Parent));
                    jd = Math.Min(ephemeris.StopJd, jd + .25); UpdateEphemeris();
                    var after = Position(moon) - Position(Bodies.First(b => b.Id == moon.Parent));
                    if ((after - before).Length < 1e-10) throw new InvalidOperationException("A moon did not advance about its parent: " + moon.Id);
                }
                if (Position(Bodies.First(b => b.Id == "SolGate")) != gateBefore)
                    throw new InvalidOperationException("The fictional Sol gate moved with the simulation clock.");
            }
            finally
            {
                selected = previous.Selected; motion = previous.Motion; moving = previous.Moving;
                zoom = previous.Zoom; targetZoom = previous.TargetZoom; pan = previous.Pan; targetPan = previous.TargetPan;
                yaw = previous.Yaw; tilt = previous.Tilt; checking = false;
                TrueScale = previous.TrueScale; jd = previous.Jd; UpdateEphemeris(); trails.Clear();
                UpdateTimer(); Refresh();
            }
        }

        internal object ShowJupiterPass(bool nearSide)
        {
            // Visible validation uses real NASA phases, with the ordinary rendering path.
            SetMotion(false); SetTrueScale(false); FocusBody("Jupiter", false);
            var moon = Bodies.First(b => b.Id == "Io"); var planet = Bodies.First(b => b.Id == "Jupiter");
            var initial = Position(planet);
            // Camera drag equivalent: put the Sun on the camera-facing side for a readable lit transit.
            yaw = -Math.PI / 2 - Math.Atan2(initial.Y, initial.X); SnapCamera();
            double start = Math.Clamp(jd, ephemeris!.StartJd, ephemeris.StopJd - 2);
            double best = double.PositiveInfinity, chosen = start;
            for (int i = 0; i <= 160; i++)
            {
                jd = start + 2 * i / 160d; UpdateEphemeris(); SnapCamera();
                var p = Position(planet); var m = Position(moon); double deltaDepth = Depth(m) - Depth(p);
                if (nearSide ? deltaDepth <= 0 : deltaDepth >= 0) continue;
                double separation = (Project(m) - Project(p)).Length;
                if (separation < best) { best = separation; chosen = jd; }
            }
            jd = chosen; UpdateEphemeris(); SnapCamera(); Refresh();
            StatusChanged?.Invoke();
            double depth = Depth(Position(moon)) - Depth(Position(planet));
            double renderedDepth = transforms[moon.Id].Matrix.OffsetZ - transforms[planet.Id].Matrix.OffsetZ;
            double planetRadius = BodyRadius(planet), moonRadius = BodyRadius(moon);
            if (best >= planetRadius - moonRadius || (nearSide ? depth <= planetRadius + moonRadius : depth >= -planetRadius - moonRadius))
                throw new InvalidOperationException("Jupiter moon transit/occultation depth failed.");
            if (Math.Abs(renderedDepth - depth) > 1e-6) throw new InvalidOperationException("Moon depth was not applied to the native models.");
            return new { Moon = "Io", NearSide = nearSide, ProjectedSeparationPixels = best,
                DepthRelativeToPlanetPixels = depth, PlanetRadiusPixels = planetRadius, MoonRadiusPixels = moonRadius,
                UsesNasaPhase = true, SimulationUtc = UtcLabel };
        }

        internal void PrepareEfficiencyFrame(string body,bool trueScale)
        {
            SetMotion(false);SetTimeRate(0);time=0;
            jd=Math.Clamp(SolarEphemeris.JulianDate(new DateTimeOffset(2026,10,20,12,0,0,TimeSpan.Zero)),ephemeris!.StartJd,ephemeris.StopJd);
            UpdateEphemeris();trails.Clear();SetTrueScale(trueScale);FocusBody(body,false);Refresh();
        }
        internal object Diagnose() => new
        {
            selected, scene = selected == "Medina" ? "RingSpaceInset" : TrueScale ? "SolTrueScale" : "SolSchematic",
            frameCount = frames, renderActive = MotionActive, schematic = !TrueScale, toScale = TrueScale,
            sceneUpdates,backgroundPaints,foregroundPaints,pointerUpdates,lastDrag,
            starPaints,orbitPaints,staticCacheScale=(starLayer.CacheMode as BitmapCache)?.RenderAtScale,
            cadence=moving?"camera-transition":drag is not null?"direct-pointer":timeRate>0?"orbital-animation":"paused",
            nasaDataAvailable = HasEphemeris, simulationUtc = UtcLabel, timeRate, julianDateTdb = jd,
            earthNightLights = earthLighting.Diagnose(), sunCoronaLayers = 2, solarPointLight = true,
            moonPositions = Bodies.Where(b => b.Parent is not null).Select(b => new { b.Id, b.Parent, Position = Position(b) }),
            labelOverlaps = labelBounds.SelectMany((a, i) => labelBounds.Skip(i + 1).Select(b => a.IntersectsWith(b))).Count(overlap => overlap),
            bodyLabelCount = labelTargets.Count(h => h.Body.Id != "Medina"),
            attachedModels = visibleModels.Count, attachedBodyIds = visibleModels.OrderBy(id => id).ToArray(),
            skippedModels = Bodies.Length - visibleModels.Count, pathSegments, rejectedPathSegments, maximumPathCoordinate,
            cameraIntervalMs = efficient?(double?)timer.Interval.TotalMilliseconds:null,
            frameLimit=efficient?(int?)(moving?60:10):null,graphicsMode=efficient?"Efficient":"FullFidelity",
            selectedRadiusPixels = BodyRadius(Bodies.First(b => b.Id == selected)),
            gateOutsideUranusInsideNeptune = Bodies.First(b => b.Id == "SolGate").Radius > Bodies.First(b => b.Id == "Uranus").Radius
                && Bodies.First(b => b.Id == "SolGate").Radius < Bodies.First(b => b.Id == "Neptune").Radius,
            assets = SolarBodyAssets.Sources
        };
        private sealed class Overlay(MapSurface owner, bool foreground) : FrameworkElement
        {
            protected override void OnRender(DrawingContext drawingContext) => owner.Paint(drawingContext, foreground);
        }
        private sealed class StaticMapLayer(MapSurface owner,bool stars) : FrameworkElement
        {
            protected override void OnRender(DrawingContext dc)
            {
                if(stars)
                {
                    owner.starPaints++;
                    foreach(var star in owner.stars)
                    {
                        dc.PushOpacity(star.Opacity);dc.DrawEllipse(Ink,null,new Point(star.Point.X*ActualWidth,star.Point.Y*ActualHeight),star.Radius,star.Radius);dc.Pop();
                    }
                }
                else
                {
                    owner.orbitPaints++;
                    if(!owner.TrueScale)foreach(var body in Bodies.Where(b=>b.Planet))
                        owner.DrawOrbit(dc,body.Radius,body.Id==owner.selected?OrbitBright:Orbit);
                }
            }
        }
        private sealed class BeltLayer(MapSurface owner) : FrameworkElement
        {
            protected override void OnRender(DrawingContext dc)=>owner.DrawBelt(dc);
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
