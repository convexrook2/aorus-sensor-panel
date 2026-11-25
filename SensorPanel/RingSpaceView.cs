using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;
using System.Windows.Threading;

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

    private const double ChamberRadius = 120, CameraLimit = 118, FlightSeconds = .95;
    private static readonly Vector3D WorldUp = new(0, 1, 0);
    private static readonly Point3D Center = new();
    private static readonly Point3D SolGate = new(0, 0, ChamberRadius);
    private static readonly Point3D IlusGate = Along(new(.76, .23, -.61), ChamberRadius);
    private static readonly Point3D Medina = new(20, -8, 76);
    private static readonly CameraPose Arrival = Aim(new(0, 0, 108), Center);
    private static readonly (string Id, string Label, Point3D Position)[] Landmarks =
    [
        (StationId, "Ring Station", Center), (MedinaId, "Medina", Medina),
        (SolGateId, "Sol gate", SolGate), (IlusGateId, "Ilus gate", IlusGate)
    ];
    private static readonly Pen[] GateInk =
    [
        FrozenPen(Color.FromArgb(139, 137, 206, 225), .75),
        FrozenPen(Color.FromArgb(172, 153, 219, 234), .9),
        FrozenPen(Color.FromArgb(210, 172, 234, 244), 1.1)
    ];
    private static readonly WallGate[] WallGates = BuildWallGates();
    private static readonly Lazy<(GeometryModel3D Boundary, GeometryModel3D Wisps)> Shell = new(BuildShell);
    private static readonly Brush StationGlow = Glow(Color.FromArgb(66, 41, 150, 176));
    private static readonly double[] LabelOffsets = [0, -1, 1, -2, 2];
    private readonly PerspectiveCamera camera = new() { NearPlaneDistance = .3, FarPlaneDistance = 270 };
    private readonly SkySurface sky;
    private readonly Canvas labels = new();
    private readonly (string Id, string Label, Point3D Position, Button Button)[] marks;
    private readonly MatrixTransform3D medinaTransform = new();
    private readonly AxisAngleRotation3D wispRotation = new(new(.24, 1, -.39), 0);
    private readonly DispatcherTimer shellTimer = new(DispatcherPriority.Background) { Interval = TimeSpan.FromMilliseconds(83) };
    private readonly Stopwatch shellClock = new();
    private readonly Model3DGroup station, sol, ilus;
    private SolidColorBrush accent = Brushes.LightCyan;
    private CameraPose pose = Arrival, from = Arrival, destination = Arrival;
    private Point? drag;
    private Point press;
    private bool moved, motion = true, rendering;
    private long started, frames, shellFrames;
    private double focal;
    private int visibleGates;
    private string selected = OverviewId;

    private readonly record struct CameraPose(Point3D Eye, double Yaw, double Pitch, double Distance)
    {
        internal Vector3D Back => new(Math.Sin(Yaw) * Math.Cos(Pitch), Math.Sin(Pitch), Math.Cos(Yaw) * Math.Cos(Pitch));
        internal Vector3D Forward => -Back;
        internal Vector3D Right => new(Math.Cos(Yaw), 0, -Math.Sin(Yaw));
        internal Vector3D Up => Vector3D.CrossProduct(Right, Forward);
        internal Point3D Pivot => Eye + Forward * Distance;
    }

    private readonly record struct WallGate(Point3D Position, Vector3D Across, Vector3D Up);
    internal event Action<string>? Selected;

    internal RingSpaceView()
    {
        ClipToBounds = true;
        Background = Brushes.Transparent;
        Focusable = true;
        Cursor = Cursors.SizeAll;
        ToolTip = "Drag to orbit. Scroll to zoom. Select a gate or station to fly there.";
        AutomationProperties.SetName(this, "Ring Space navigation");

        var shellViewport = new Viewport3D { Camera = camera, IsHitTestVisible = false, ClipToBounds = false };
        shellViewport.Children.Add(new ModelVisual3D { Content = Shell.Value.Boundary });
        shellViewport.Children.Add(new ModelVisual3D { Content = Shell.Value.Wisps, Transform = new RotateTransform3D(wispRotation) });
        Children.Add(shellViewport);
        sky = new SkySurface(this) { IsHitTestVisible = false };
        Children.Add(sky);
        var scene = new Model3DGroup();
        scene.Children.Add(new AmbientLight(Color.FromRgb(115, 142, 166)));
        scene.Children.Add(new DirectionalLight(Color.FromRgb(219, 235, 250), new(.5, -.6, -1)));
        scene.Children.Add(new DirectionalLight(Color.FromRgb(60, 159, 185), new(-1, -.5, 1)));
        station = RingStationModel.Build();
        sol = SolarGateModel.FrozenModel;
        ilus = RingStationModel.BuildGateMarker();
        scene.Children.Add(Place(station, Center, 12));
        scene.Children.Add(Place(sol, SolGate, 9));
        scene.Children.Add(Place(ilus, IlusGate, 6.8));
        scene.Children.Add(MedinaPortrait());

        var viewport = new Viewport3D { Camera = camera, IsHitTestVisible = false, ClipToBounds = false };
        viewport.Children.Add(new ModelVisual3D { Content = scene });
        Children.Add(viewport);
        Children.Add(labels);
        marks = Landmarks.Select(mark => (mark.Id, mark.Label, mark.Position, CreateLabel(mark.Id, mark.Label))).ToArray();

        SizeChanged += (_, _) => UpdateView();
        Loaded += (_, _) => UpdateShellMotion();
        IsVisibleChanged += (_, _) =>
        {
            if (!IsVisible) FinishFlight();
            UpdateShellMotion();
        };
        Unloaded += (_, _) => { FinishFlight(); shellTimer.Stop(); };
        shellTimer.Tick += (_, _) =>
        {
            wispRotation.Angle = (wispRotation.Angle + shellClock.Elapsed.TotalSeconds * 1.15) % 360;
            shellClock.Restart();
            shellFrames++;
        };
        MouseLeftButtonDown += (_, args) =>
        {
            Focus();
            FinishFlight(settle: false);
            press = args.GetPosition(this);
            drag = press;
            moved = false;
            CaptureMouse();
            args.Handled = true;
        };
        MouseMove += (_, args) =>
        {
            if (drag is not { } before || args.LeftButton != MouseButtonState.Pressed) return;
            Point after = args.GetPosition(this);
            if (!moved && (after - press).Length < 4) return;
            moved = true;
            drag = after;
            Orbit((after.X - before.X) * .008, (after.Y - before.Y) * .008);
            args.Handled = true;
        };
        MouseLeftButtonUp += (_, args) =>
        {
            if (drag is null) return;
            Point point = args.GetPosition(this);
            ReleaseMouseCapture();
            if (!moved) SelectAt(point);
            args.Handled = true;
        };
        LostMouseCapture += (_, _) => drag = null;
        MouseWheel += (_, args) => { Zoom(Math.Pow(1.001, args.Delta)); args.Handled = true; };
        KeyDown += NavigateKey;
        UpdateView();
    }

    internal static bool Contains(string id) => Targets.Any(target => target.Id == id);
    internal bool MotionActive => rendering || shellTimer.IsEnabled;

    internal void SetMotion(bool enabled)
    {
        motion = enabled;
        if (!enabled) FinishFlight();
        UpdateShellMotion();
    }

    private void UpdateShellMotion()
    {
        bool active = motion && IsLoaded && IsVisible && SystemParameters.ClientAreaAnimation;
        if (active == shellTimer.IsEnabled) return;
        if (!active) { shellTimer.Stop(); return; }
        shellClock.Restart();
        shellTimer.Start();
    }

    internal void ApplyTheme(SolidColorBrush color)
    {
        accent = color;
        UpdateLabels();
    }

    internal void FocusTarget(string id, bool animate)
    {
        selected = id;
        CameraPose next;
        if (id == OverviewId) next = Arrival;
        else
        {
            Point3D target = Landmarks.Single(mark => mark.Id == id).Position;
            Vector3D approach = id switch
            {
                SolGateId or IlusGateId => Unit(Center - target),
                StationId when (pose.Eye - target).Length > 1 => Unit(pose.Eye - target),
                _ => Unit(Arrival.Eye - target)
            };
            double range = id switch { StationId => 38, MedinaId => 20, SolGateId => 28, _ => 23 };
            next = Aim(target + approach * range, target);
        }
        FlyTo(next, animate);
    }

    internal object Diagnose() => new
    {
        selected, scene = "RingSpace", schematic = true, toScale = false, renderActive = rendering,
        frameCount = frames, labelOverlaps = LabelOverlaps(), gateMarkers = WallGates.Length, visibleGates,
        cameraProjection = "Perspective", cameraPosition = pose.Eye, distance = pose.Distance,
        shellRadius = ChamberRadius, stationPosition = Center, shellTriangles = Triangles(Shell.Value.Boundary),
        shellFrames, shellActive = shellTimer.IsEnabled, wispAngle = wispRotation.Angle,
        maximumGateRadiusError = WallGates.Select(gate => gate.Position).Append(SolGate).Append(IlusGate)
            .Max(position => Math.Abs((position - Center).Length - ChamberRadius)),
        modelTriangles = Triangles(selected switch { SolGateId => sol, IlusGateId => ilus, MedinaId => null, _ => station }),
        hasMedinaPortrait = true
    };

    private static int Triangles(Model3D? model) => model switch
    {
        GeometryModel3D { Geometry: MeshGeometry3D mesh } => mesh.TriangleIndices.Count / 3,
        Model3DGroup group => group.Children.Sum(Triangles),
        _ => 0
    };

    private int LabelOverlaps()
    {
        var bounds = marks.Where(mark => mark.Button.Visibility == Visibility.Visible)
            .Select(mark => new Rect(new Point(Canvas.GetLeft(mark.Button), Canvas.GetTop(mark.Button)), mark.Button.DesiredSize))
            .ToArray();
        return bounds.SelectMany((rect, i) => bounds.Skip(i + 1).Select(other => Rect.Intersect(rect, other)))
            .Count(overlap => overlap.Width > 0 && overlap.Height > 0);
    }

    private void FlyTo(CameraPose next, bool animate)
    {
        if (!animate || !motion || !IsVisible || !SystemParameters.ClientAreaAnimation || pose == next)
        {
            FinishFlight(settle: false);
            pose = destination = next;
            UpdateView();
            return;
        }
        from = pose;
        destination = next;
        started = Stopwatch.GetTimestamp();
        if (!rendering) CompositionTarget.Rendering += AnimateCamera;
        rendering = true;
        UpdateLabels();
    }

    private void AnimateCamera(object? sender, EventArgs args)
    {
        double progress = Stopwatch.GetElapsedTime(started).TotalSeconds / FlightSeconds;
        if (progress >= 1) { FinishFlight(); return; }
        double t = progress * progress * (3 - 2 * progress);
        double turn = Math.IEEERemainder(destination.Yaw - from.Yaw, Math.Tau);
        pose = new(from.Eye + (destination.Eye - from.Eye) * t, from.Yaw + turn * t,
            from.Pitch + (destination.Pitch - from.Pitch) * t, from.Distance + (destination.Distance - from.Distance) * t);
        frames++;
        UpdateView();
    }

    private void FinishFlight(bool settle = true)
    {
        if (!rendering) return;
        CompositionTarget.Rendering -= AnimateCamera;
        rendering = false;
        if (!settle) return;
        pose = destination;
        UpdateView();
    }

    private void Orbit(double horizontal, double vertical)
    {
        FinishFlight(settle: false);
        Point3D pivot = pose.Pivot;
        double yaw = Math.IEEERemainder(pose.Yaw + horizontal, Math.Tau);
        double pitch = Math.Clamp(pose.Pitch + vertical, -1.49, 1.49);
        var orbit = pose with { Yaw = yaw, Pitch = pitch };
        Vector3D back = orbit.Back;
        Vector3D radius = pivot - Center;
        if (radius.Length > 108)
        {
            Vector3D outwards = Unit(radius);
            double outward = Vector3D.DotProduct(back, outwards);
            if (outward > -.28) back = Unit(back - outwards * (outward + .28));
        }
        double range = KeepInsideShell(pivot, back, pose.Distance);
        pose = Aim(pivot + back * range, pivot);
        UpdateView();
    }

    private void Zoom(double factor)
    {
        CameraPose basis = rendering ? destination : pose;
        Point3D pivot = basis.Pivot;
        double minimum = selected switch { OverviewId or StationId => 17, SolGateId => 12, _ => 9 };
        double range = KeepInsideShell(pivot, basis.Back, Math.Max(minimum, basis.Distance / factor));
        FlyTo(basis with { Eye = pivot + basis.Back * range, Distance = range }, animate: true);
    }

    private static double KeepInsideShell(Point3D pivot, Vector3D direction, double distance)
    {
        Vector3D radius = pivot - Center;
        double along = Vector3D.DotProduct(radius, direction);
        double halfChord = Math.Sqrt(Math.Max(0, along * along + CameraLimit * CameraLimit - radius.LengthSquared));
        double closest = Math.Max(1, -along - halfChord), furthest = Math.Max(closest, -along + halfChord);
        return Math.Clamp(distance, closest, furthest);
    }

    private void NavigateKey(object sender, KeyEventArgs args)
    {
        switch (args.Key)
        {
            case Key.Home: Selected?.Invoke(OverviewId); break;
            case Key.Left: Orbit(-.14, 0); break;
            case Key.Right: Orbit(.14, 0); break;
            case Key.Up: Orbit(0, -.09); break;
            case Key.Down: Orbit(0, .09); break;
            case Key.Add or Key.OemPlus: Zoom(1.2); break;
            case Key.Subtract or Key.OemMinus: Zoom(1 / 1.2); break;
            default: return;
        }
        args.Handled = true;
    }

    private void UpdateView()
    {
        if (ActualWidth < 1 || ActualHeight < 1) return;
        focal = Math.Max(ActualWidth * .135, ActualHeight * 1.12);
        camera.FieldOfView = 2 * Math.Atan(ActualWidth / (2 * focal)) * (180 / Math.PI);
        camera.Position = pose.Eye;
        camera.LookDirection = pose.Forward;
        camera.UpDirection = pose.Up;
        Vector3D right = pose.Right, up = pose.Up, normal = pose.Back;
        medinaTransform.Matrix = new(right.X, right.Y, right.Z, 0, up.X, up.Y, up.Z, 0, normal.X, normal.Y, normal.Z, 0,
            Medina.X, Medina.Y, Medina.Z, 1);
        sky.InvalidateVisual();
        UpdateLabels();
    }

    private Point? Project(Point3D world, out double depth)
    {
        Vector3D relative = world - pose.Eye;
        depth = Vector3D.DotProduct(relative, pose.Forward);
        if (depth <= camera.NearPlaneDistance) return null;
        return new(ActualWidth / 2 + focal * Vector3D.DotProduct(relative, pose.Right) / depth,
            ActualHeight / 2 - focal * Vector3D.DotProduct(relative, pose.Up) / depth);
    }

    private bool OnScreen(Point at, double padding = 0) => at.X >= -padding && at.Y >= -padding
        && at.X <= ActualWidth + padding && at.Y <= ActualHeight + padding;

    private bool BehindStation(Point3D position)
    {
        if (position == Center) return false;
        Vector3D towards = position - pose.Eye, toCenter = Center - pose.Eye;
        double fraction = Vector3D.DotProduct(toCenter, towards) / towards.LengthSquared;
        return fraction is > 0 and < 1 && (toCenter - fraction * towards).LengthSquared < 12 * 12;
    }

    private Button CreateLabel(string id, string label)
    {
        var chrome = new FrameworkElementFactory(typeof(Border));
        chrome.SetValue(Border.BackgroundProperty, new SolidColorBrush(Color.FromArgb(200, 6, 15, 24)));
        chrome.SetValue(Border.PaddingProperty, new Thickness(4, 1, 4, 1));
        chrome.SetValue(Border.CornerRadiusProperty, new CornerRadius(3));
        chrome.AppendChild(new FrameworkElementFactory(typeof(ContentPresenter)));
        var button = new Button { Content = label, Template = new ControlTemplate(typeof(Button)) { VisualTree = chrome },
            FontSize = 11, Cursor = Cursors.Hand };
        AutomationProperties.SetName(button, label);
        button.Click += (_, args) => { args.Handled = true; Selected?.Invoke(id); };
        labels.Children.Add(button);
        return button;
    }

    private void UpdateLabels()
    {
        if (ActualWidth < 1 || ActualHeight < 1) return;
        var occupied = new List<Rect>(marks.Length);
        foreach (var (id, label, position, button) in marks.OrderBy(mark => mark.Id != selected))
        {
            Point? at = BehindStation(position) ? null : Project(position, out _);
            bool returnLabel = id == SolGateId && (at is not Point point || !OnScreen(point)) && selected == OverviewId;
            if (!returnLabel && (at is not Point onScreen || !OnScreen(onScreen)))
            {
                button.Visibility = Visibility.Collapsed;
                continue;
            }
            button.Visibility = Visibility.Visible;
            button.Foreground = id == selected ? accent : Brushes.LightCyan;
            button.FontSize = ActualHeight < 115 ? 9 : 11;
            button.Content = returnLabel ? "Sol gate ↶" : label;
            button.Measure(new(double.PositiveInfinity, double.PositiveInfinity));
            Size size = button.DesiredSize;
            Point anchor = returnLabel ? new(1, ActualHeight) : at!.Value;
            double offset = 6;
            if (!returnLabel)
            {
                double radius = id switch { StationId => 12, MedinaId => 4, SolGateId => 9, _ => 6.8 };
                if (Project(position + pose.Right * radius, out _) is Point rim) offset += (rim - anchor).Length;
            }
            bool placed = false;
            foreach (double step in LabelOffsets)
            {
                double y = Math.Clamp(anchor.Y - size.Height / 2 + step * (size.Height + 2), 1, Math.Max(1, ActualHeight - size.Height - 1));
                double x = anchor.X + offset;
                if (x + size.Width > ActualWidth) x = anchor.X - offset - size.Width;
                x = Math.Clamp(x, 1, Math.Max(1, ActualWidth - size.Width - 1));
                var box = new Rect(x, y, size.Width, size.Height);
                if (occupied.Any(rect => rect.IntersectsWith(box))) continue;
                occupied.Add(box);
                Canvas.SetLeft(button, x);
                Canvas.SetTop(button, y);
                placed = true;
                break;
            }
            if (!placed) button.Visibility = Visibility.Collapsed;
        }
    }

    private void SelectAt(Point point)
    {
        foreach (var mark in Landmarks.OrderBy(mark => (mark.Position - pose.Eye).LengthSquared))
        {
            if (BehindStation(mark.Position) || Project(mark.Position, out var depth) is not Point at) continue;
            double radius = mark.Id switch { StationId => 12, MedinaId => 4, SolGateId => 9, _ => 6.8 };
            if ((at - point).Length > Math.Max(7, radius * focal / depth)) continue;
            Selected?.Invoke(mark.Id);
            return;
        }
    }

    private GeometryModel3D MedinaPortrait()
    {
        using var source = new MemoryStream(Rookline.AssetProtection.AppAssets.Read("SolarNavigation/textures/medina-parked.png"), false);
        var image = BitmapDecoder.Create(source, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad).Frames[0];
        double h = 4.5, w = h * image.PixelWidth / image.PixelHeight;
        var mesh = new MeshGeometry3D
        {
            Positions = [new(-w, -h, 0), new(w, -h, 0), new(w, h, 0), new(-w, h, 0)],
            TextureCoordinates = [new(0, 1), new(1, 1), new(1, 0), new(0, 0)],
            TriangleIndices = [0, 1, 2, 0, 2, 3]
        };
        var material = new EmissiveMaterial(new ImageBrush(image));
        mesh.Freeze();
        material.Freeze();
        return new(mesh, material) { Transform = medinaTransform };
    }

    private static Model3DGroup Place(Model3D model, Point3D position, double scale)
    {
        var transform = new Transform3DGroup();
        transform.Children.Add(new ScaleTransform3D(scale, scale, scale));
        if (position != Center)
        {
            Vector3D normal = Unit(position - Center), right = Unit(Vector3D.CrossProduct(WorldUp, normal));
            Vector3D up = Vector3D.CrossProduct(normal, right);
            transform.Children.Add(new MatrixTransform3D(new(right.X, right.Y, right.Z, 0,
                up.X, up.Y, up.Z, 0, normal.X, normal.Y, normal.Z, 0, 0, 0, 0, 1)));
        }
        transform.Children.Add(new TranslateTransform3D(position.X, position.Y, position.Z));
        var group = new Model3DGroup { Transform = transform };
        group.Children.Add(model);
        group.Freeze();
        return group;
    }

    private static CameraPose Aim(Point3D eye, Point3D target)
    {
        Vector3D back = eye - target;
        double distance = back.Length;
        back /= distance;
        return new(eye, Math.Atan2(back.X, back.Z), Math.Asin(Math.Clamp(back.Y, -1, 1)), distance);
    }

    private static Vector3D Unit(Vector3D vector) { vector.Normalize(); return vector; }
    private static Point3D Along(Vector3D direction, double length) => Center + Unit(direction) * length;

    private static WallGate[] BuildWallGates()
    {
        const int sampleCount = 126, gateCount = 112;
        var candidates = Enumerable.Range(0, sampleCount).Select(index =>
        {
            double y = 2 * (index + .5) / sampleCount - 1, ring = Math.Sqrt(1 - y * y);
            double longitude = index * Math.PI * (3 - Math.Sqrt(5));
            var normal = new Vector3D(Math.Cos(longitude) * ring, y, Math.Sin(longitude) * ring);
            var right = Unit(Vector3D.CrossProduct(WorldUp, normal));
            return new WallGate(Along(normal, ChamberRadius), right * 1.14, Vector3D.CrossProduct(normal, right) * 1.14);
        }).Where(gate => (gate.Position - SolGate).Length > 16 && (gate.Position - IlusGate).Length > 14).ToArray();
        return Enumerable.Range(0, gateCount).Select(index => candidates[(int)((index + .5) * candidates.Length / gateCount)]).ToArray();
    }

    private static (GeometryModel3D Boundary, GeometryModel3D Wisps) BuildShell()
    {
        const int width = 512, height = 256;
        var boundary = new byte[width * height * 4];
        var wisps = new byte[boundary.Length];
        for (int row = 0; row < height; row++)
        {
            double latitude = Math.PI * row / (height - 1), y = Math.Cos(latitude), ring = Math.Sin(latitude);
            for (int column = 0; column < width; column++)
            {
                double longitude = Math.Tau * column / (width - 1), x = ring * Math.Cos(longitude), z = ring * Math.Sin(longitude);
                var point = new Vector3D(x * 4, y * 4, z * 4);
                // Sample continuous 3D noise on the sphere to avoid UV seams and pole pinching.
                var distortion = new Vector3D(Noise(point + new Vector3D(13, 5, -4)),
                    Noise(point + new Vector3D(-7, 17, 4)), Noise(point + new Vector3D(5, -8, 19)));
                Vector3D flow = point + (distortion - new Vector3D(.5, .5, .5)) * 2.45;
                double field = Noise(flow) * .60 + Noise(flow * 2.12) * .27 + Noise(flow * 4.7) * .13;
                double cloud = Smooth(.37, .80, field);
                double filament = (1 - Smooth(.009, .10, Math.Abs(field - .55)))
                    * Smooth(.34, .77, Noise(point * .72));
                int offset = (row * width + column) * 4;
                boundary[offset] = (byte)(25 + 30 * cloud + 16 * filament);
                boundary[offset + 1] = (byte)(9 + 49 * cloud + 35 * filament);
                boundary[offset + 2] = (byte)(4 + 8 * cloud + 11 * filament);
                boundary[offset + 3] = 255;

                double vapor = Noise(flow * 1.48 + new Vector3D(8, 16, 9)) * .76 + Noise(flow * 3.35) * .24;
                double veil = Smooth(.48, .78, vapor) * Smooth(.29, .68, Noise(point + new Vector3D(3, 5, 9)));
                wisps[offset] = 98;
                wisps[offset + 1] = 113;
                wisps[offset + 2] = 13;
                wisps[offset + 3] = (byte)(veil * 109);
            }
        }
        var sphere = SolarBodyAssets.Sphere();
        return (Layer(boundary, ChamberRadius), Layer(wisps, ChamberRadius - .16));

        GeometryModel3D Layer(byte[] pixels, double radius)
        {
            var texture = BitmapSource.Create(width, height, 96, 96, PixelFormats.Bgra32, null, pixels, width * 4);
            var surface = new EmissiveMaterial(new ImageBrush(texture));
            var shell = new GeometryModel3D(sphere, surface)
            {
                BackMaterial = surface,
                Transform = new ScaleTransform3D(radius, radius, radius)
            };
            shell.Freeze();
            return shell;
        }
    }

    private static double Smooth(double lower, double upper, double value)
    {
        double t = Math.Clamp((value - lower) / (upper - lower), 0, 1);
        return t * t * (3 - 2 * t);
    }

    private static double Noise(Vector3D point)
    {
        int x = (int)Math.Floor(point.X), y = (int)Math.Floor(point.Y), z = (int)Math.Floor(point.Z);
        double a = Smooth(0, 1, point.X - x), b = Smooth(0, 1, point.Y - y), c = Smooth(0, 1, point.Z - z);
        double lower = Lerp(Lerp(Hash(x, y, z), Hash(x + 1, y, z), a),
            Lerp(Hash(x, y + 1, z), Hash(x + 1, y + 1, z), a), b);
        double upper = Lerp(Lerp(Hash(x, y, z + 1), Hash(x + 1, y, z + 1), a),
            Lerp(Hash(x, y + 1, z + 1), Hash(x + 1, y + 1, z + 1), a), b);
        return Lerp(lower, upper, c);

        static double Lerp(double from, double to, double fraction) => from + (to - from) * fraction;
        static double Hash(int x, int y, int z)
        {
            unchecked
            {
                uint value = (uint)x * 1_597_334_677u ^ (uint)y * 3_812_015_801u ^ (uint)z * 958_689_577u;
                value ^= value >> 16;
                value *= 2_246_822_519u;
                value ^= value >> 13;
                return (value ^ value >> 16) / (double)uint.MaxValue;
            }
        }
    }

    private static Pen FrozenPen(Color color, double width)
    {
        var pen = new Pen(new SolidColorBrush(color), width);
        pen.Freeze();
        return pen;
    }

    private static Brush Glow(Color color)
    {
        var brush = new RadialGradientBrush();
        brush.GradientStops.Add(new(color, 0));
        brush.GradientStops.Add(new(Color.FromArgb((byte)(color.A / 3), color.R, color.G, color.B), .53));
        brush.GradientStops.Add(new(Color.FromArgb(0, color.R, color.G, color.B), 1));
        brush.Freeze();
        return brush;
    }

    private sealed class SkySurface(RingSpaceView view) : FrameworkElement
    {
        private static readonly (double X, double Y)[] Ring = Enumerable.Range(0, 12)
            .Select(index => (Math.Cos(index * Math.Tau / 12), Math.Sin(index * Math.Tau / 12))).ToArray();

        protected override void OnRender(DrawingContext drawing)
        {
            double size = Math.Max(ActualWidth, ActualHeight);
            if (size < 1) return;
            if (view.Project(Center, out double stationDepth) is Point core && view.OnScreen(core, 20))
            {
                double halo = view.focal * 27 / stationDepth;
                drawing.DrawEllipse(StationGlow, null, core, halo, halo);
            }
            view.visibleGates = 0;
            foreach (var gate in WallGates)
            {
                if (view.Project(gate.Position, out double depth) is not Point at || depth < 4 || !view.OnScreen(at, 10)) continue;
                view.visibleGates++;
                double radius = view.focal / depth;
                Pen pen = GateInk[radius > 5 ? 2 : radius > 2 ? 1 : 0];
                if (radius < 1.55) { drawing.DrawEllipse(null, pen, at, 1.25, .9); continue; }
                var outline = new StreamGeometry();
                using (var path = outline.Open())
                {
                    for (int i = 0; i < Ring.Length; i++)
                    {
                        var (x, y) = Ring[i];
                        var onShell = Along(gate.Position - Center + gate.Across * x + gate.Up * y, ChamberRadius);
                        Point edge = view.Project(onShell, out _) ?? at;
                        if (i == 0) path.BeginFigure(edge, false, true);
                        else path.LineTo(edge, true, false);
                    }
                }
                outline.Freeze();
                drawing.DrawGeometry(null, pen, outline);
            }
        }
    }
}
