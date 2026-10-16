using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Media3D;

namespace SensorPanel;

internal sealed class Holotable : Grid
{
    private readonly PerspectiveCamera camera = new(new(0, 2.1, 5.2), new(0, -2.1, -5.2), new(0, 1, 0), 43);
    private readonly AxisAngleRotation3D yaw = new(new(0, 1, 0), -58);
    private readonly AxisAngleRotation3D pitch = new(new(1, 0, 0), -12);
    private readonly AxisAngleRotation3D sweep = new(new(0, 1, 0), 0);
    private readonly List<HoloReadout> readouts = new();
    private readonly List<(HoloReadout Reading, TextBlock Source)> bindings = new();
    private double defaultFieldOfView=43;
    private Point? drag;
    private bool motion;
    private readonly List<(GeometryModel3D Model, ShipModelPart Part)> hullParts = new();
    private readonly Material hologramMaterial;
    private readonly DirectionalLight fill;
    private readonly Button colours;
    private bool originalColours = true;
    internal bool MotionActive => sweep.HasAnimatedProperties;

    public Holotable(PanelTheme theme, Dictionary<string, TextBlock> source, bool motion, bool readoutPulse, Action close)
    {
        this.motion = motion;
        Focusable = true; IsManipulationEnabled = true; ClipToBounds = true;
        Background = new SolidColorBrush(Color.FromRgb(4, 12, 19));
        RowDefinitions.Add(new() { Height = GridLength.Auto });
        RowDefinitions.Add(new() { Height = new GridLength(1, GridUnitType.Star) });
        RowDefinitions.Add(new() { Height = GridLength.Auto });
        var header = new StackPanel { Margin = new Thickness(22, 16, 22, 8) };
        var buttons = new StackPanel { Orientation=Orientation.Horizontal, HorizontalAlignment=HorizontalAlignment.Right, Margin=new Thickness(0,8,0,0) };
        colours = Command("ORIGINAL COLOURS", theme, () => SetOriginalColours(!originalColours));
        buttons.Children.Add(colours);
        buttons.Children.Add(Command("RESET VIEW", theme, Reset));
        buttons.Children.Add(Command("CLOSE  ×", theme, close));
        var heading = new StackPanel();
        heading.Children.Add(new TextBlock { Text = theme.Vessel!.Name + "  /  HOLOTABLE", FontSize = 22,
            FontFamily = new FontFamily("Bahnschrift"), Foreground = theme.Accent });
        if (!Supports(theme.ShipId)) throw new InvalidOperationException("This ship has no 3D holotable model.");
        heading.Children.Add(new TextBlock { Text = "3D MODEL / " + ModelCredit(theme.ShipId!) + " · CC BY 4.0",
            FontSize = 10, Foreground = theme.Muted });
        header.Children.Add(heading); header.Children.Add(buttons); Children.Add(header);

        var view = new Viewport3D { Camera = camera, ClipToBounds = true };
        var scene = new Model3DGroup();
        scene.Children.Add(new AmbientLight(Color.FromRgb(110, 110, 110)));
        scene.Children.Add(new DirectionalLight(Colors.White, new Vector3D(-1, -2, -3)));
        fill=new DirectionalLight(Colors.White, new Vector3D(2, -1, 1)); scene.Children.Add(fill);
        var hull = new Model3DGroup();
        var hullColour=theme.Accent.Color;
        var hullInk=new SolidColorBrush(Color.FromRgb((byte)(hullColour.R*.48), (byte)(hullColour.G*.65), (byte)(hullColour.B*.65)));
        var materials=new MaterialGroup(); materials.Children.Add(new DiffuseMaterial(hullInk));
        materials.Children.Add(new EmissiveMaterial(new SolidColorBrush(Color.FromRgb(9, 28, 34))));
        materials.Freeze(); hologramMaterial=materials;
        foreach (var part in ShipModel.Load(theme.ShipId!))
        {
            var model=new GeometryModel3D(part.Mesh, part.Original) { BackMaterial=part.DoubleSided ? part.Original : null };
            hullParts.Add((model, part)); hull.Children.Add(model);
        }
        var transforms = new Transform3DGroup();
        transforms.Children.Add(new RotateTransform3D(yaw)); transforms.Children.Add(new RotateTransform3D(pitch));
        transforms.Children.Add(new TranslateTransform3D(0, .18, 0)); hull.Transform = transforms; scene.Children.Add(hull);
        scene.Children.Add(Ring(1.94, -.48, 0, 360, theme.Accent, .008));
        scene.Children.Add(Ring(2.1, -.48, 0, 360, theme.Divider, .007));
        var scanning = new Model3DGroup { Transform = new RotateTransform3D(sweep) };
        scanning.Children.Add(Ring(2.05, -.46, 0, 65, theme.SecondaryAccent ?? theme.Accent, .035));
        scanning.Children.Add(Ring(2.05, -.46, 180, 245, theme.Accent, .018));
        scene.Children.Add(scanning);
        view.Children.Add(new ModelVisual3D { Content = scene });
        var stage = new Grid();
        stage.ColumnDefinitions.Add(new() { Width = new GridLength(1, GridUnitType.Star) });
        stage.ColumnDefinitions.Add(new() { Width = GridLength.Auto });
        stage.Children.Add(view); SetRow(stage, 1); Children.Add(stage);

        var footer = new StackPanel { Margin = new Thickness(22, 0, 22, 18) };
        var readings = new System.Windows.Controls.Primitives.UniformGrid { Columns = 3 };
        foreach (var item in new[] { ("CPU", "cpuTemperature"), ("GPU", "gpuTemperature"), ("DISPLAY FPS", "fps"), ("CPU POWER", "cpuPower"), ("GPU POWER", "gpuPower"), ("MEMORY", "memory") })
        {
            var cell = new StackPanel { Margin = new Thickness(6, 3, 6, 5) };
            cell.Children.Add(new TextBlock { Text = item.Item1, FontSize = 10, Foreground = theme.Muted });
            var reading = new HoloReadout(item.Item1.StartsWith("GPU") ? theme.SecondaryAccent ?? theme.Accent : theme.Accent, readoutPulse)
                { FontSize = 25, FontFamily = new FontFamily("Consolas") };
            reading.SetBinding(TextBlock.TextProperty, new Binding(nameof(TextBlock.Text)) { Source = source[item.Item2], NotifyOnTargetUpdated = true });
            readouts.Add(reading); bindings.Add((reading, source[item.Item2])); cell.Children.Add(reading); readings.Children.Add(cell);
        }
        footer.Children.Add(readings);
        footer.Children.Add(new TextBlock { Text = "DRAG / TOUCH TO ORBIT   ·   SCROLL / PINCH TO ZOOM   ·   ARROWS TO ROTATE   ·   HOME TO RESET   ·   ESC TO CLOSE",
            FontSize = 9, Foreground = theme.Muted, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(6, 8, 0, 0) });
        SetRow(footer, 2); Children.Add(footer);
        bool? previousPortrait=null;
        SizeChanged += (_, _) =>
        {
            bool portrait=ActualWidth<ActualHeight;
            if (previousPortrait==portrait) return;
            previousPortrait=portrait;
            if (readings.Parent is Panel parent) parent.Children.Remove(readings);
            readings.Columns=portrait ? 3 : 1;
            readings.Width=portrait ? double.NaN : Math.Clamp(ActualWidth*.2, 180, 280);
            readings.VerticalAlignment=portrait ? VerticalAlignment.Stretch : VerticalAlignment.Center;
            readings.Margin=portrait ? new Thickness(0) : new Thickness(12, 4, 24, 4);
            if (portrait) footer.Children.Insert(0, readings);
            else { SetColumn(readings, 1); stage.Children.Add(readings); }
        };
        view.SizeChanged += (_, _) =>
        {
            defaultFieldOfView = Math.Clamp(2*Math.Atan(Math.Tan(23*Math.PI/180)*view.ActualWidth/Math.Max(1,view.ActualHeight))*180/Math.PI, 43, 110);
            camera.FieldOfView=defaultFieldOfView;
        };
        view.MouseLeftButtonDown += (_, e) => { drag = e.GetPosition(this); CaptureMouse(); Focus(); e.Handled = true; };
        MouseMove += (_, e) => { if (drag is not { } before) return; var now = e.GetPosition(this); Orbit((now.X-before.X)*.45, (now.Y-before.Y)*.3); drag=now; e.Handled=true; };
        MouseLeftButtonUp += (_, e) => { if (drag is null) return; drag=null; ReleaseMouseCapture(); e.Handled=true; };
        LostMouseCapture += (_, _) => drag=null;
        MouseWheel += (_, e) => { Zoom(-e.Delta / 120.0 * 3); e.Handled=true; };
        ManipulationStarting += (_, e) => { e.ManipulationContainer=this; e.Mode=ManipulationModes.Translate | ManipulationModes.Scale; e.Handled=true; };
        ManipulationDelta += (_, e) => { Orbit(e.DeltaManipulation.Translation.X*.45, e.DeltaManipulation.Translation.Y*.3); Zoom((1-e.DeltaManipulation.Scale.X)*20); e.Handled=true; };
        KeyDown += (_, e) =>
        {
            switch (e.Key)
            {
                case Key.Escape: close(); break;
                case Key.Home: Reset(); break;
                case Key.Left: Orbit(-8, 0); break;
                case Key.Right: Orbit(8, 0); break;
                case Key.Up: Orbit(0, -5); break;
                case Key.Down: Orbit(0, 5); break;
                case Key.Add: case Key.OemPlus: Zoom(-3); break;
                case Key.Subtract: case Key.OemMinus: Zoom(3); break;
                default: return;
            }
            e.Handled=true;
        };
        IsVisibleChanged += (_, _) => UpdateMotion(); Loaded += (_, _) => UpdateMotion();
        Unloaded += (_, _) => sweep.BeginAnimation(AxisAngleRotation3D.AngleProperty, null);
    }

    internal static Button Command(string title, PanelTheme theme, Action action)
    {
        var button = new Button { Content = title, Foreground = theme.Accent, Background = theme.Surface, BorderBrush = theme.Accent,
            BorderThickness = new Thickness(1), Padding = new Thickness(12, 6, 12, 6), Margin = new Thickness(5, 0, 0, 0),
            FontSize = 11, Cursor = Cursors.Hand, Focusable = true };
        button.Click += (_, e) => { e.Handled = true; action(); }; return button;
    }
    internal void Orbit(double horizontal, double vertical) { yaw.Angle = (yaw.Angle + horizontal) % 360; pitch.Angle = Math.Clamp(pitch.Angle + vertical, -70, 70); }
    internal void Zoom(double amount) => camera.FieldOfView = Math.Clamp(camera.FieldOfView + amount, 25, 110);
    internal void Reset() { yaw.Angle=-58; pitch.Angle=-12; camera.FieldOfView=defaultFieldOfView; }
    internal void SetOriginalColours(bool enabled)
    {
        originalColours=enabled; colours.Content=enabled ? "ORIGINAL COLOURS" : "HOLOGRAM";
        fill.Color=enabled ? Colors.White : ((DiffuseMaterial)((MaterialGroup)hologramMaterial).Children[0]).Brush is SolidColorBrush brush ? brush.Color : Colors.Cyan;
        foreach (var entry in hullParts)
        {
            entry.Model.Material=enabled ? entry.Part.Original : hologramMaterial;
            entry.Model.BackMaterial=entry.Part.DoubleSided ? entry.Model.Material : null;
        }
    }
    public void SetMotion(bool enabled) { motion=enabled; UpdateMotion(); }
    public void SetReadoutPulse(bool enabled) { foreach (var reading in readouts) reading.SetPulse(enabled); }
    private void UpdateMotion()
    {
        sweep.BeginAnimation(AxisAngleRotation3D.AngleProperty, null);
        if (!motion || !IsVisible || !SystemParameters.ClientAreaAnimation) return;
        var rotation = new DoubleAnimation(0, 360, TimeSpan.FromSeconds(18)) { RepeatBehavior=RepeatBehavior.Forever };
        Timeline.SetDesiredFrameRate(rotation, 20); sweep.BeginAnimation(AxisAngleRotation3D.AngleProperty, rotation);
    }
    internal void CheckInteraction()
    {
        Orbit(120, 300); Zoom(-200);
        if (pitch.Angle != 70 || camera.FieldOfView != 25) throw new InvalidOperationException("Holotable controls did not clamp.");
        Reset();
        if (yaw.Angle != -58 || pitch.Angle != -12 || camera.FieldOfView != defaultFieldOfView) throw new InvalidOperationException("Holotable reset failed.");
        SetMotion(false);
        if (MotionActive) throw new InvalidOperationException("Holotable motion did not stop.");
        SetOriginalColours(false);
        if (hullParts.Any(p => !ReferenceEquals(p.Model.Material, hologramMaterial))) throw new InvalidOperationException("Hologram materials did not apply.");
        SetOriginalColours(true);
        if (hullParts.Any(p => !ReferenceEquals(p.Model.Material, p.Part.Original))) throw new InvalidOperationException("Original ship materials did not restore.");
        CheckBindings();
    }
    internal void CheckBindings()
    {
        if (bindings.Any(pair => pair.Reading.Text != pair.Source.Text)) throw new InvalidOperationException("Holotable readings diverged from the sensor presentation.");
    }
    private static GeometryModel3D Ring(double radius, double y, int start, int end, Brush ink, double thickness)
    {
        var mesh = new MeshGeometry3D();
        for (int degree=start; degree<=end; degree+=2)
        {
            double a=degree*Math.PI/180;
            mesh.Positions.Add(new((radius-thickness)*Math.Cos(a), y, (radius-thickness)*Math.Sin(a)));
            mesh.Positions.Add(new((radius+thickness)*Math.Cos(a), y, (radius+thickness)*Math.Sin(a)));
            int i=mesh.Positions.Count-2;
            if (i==0) continue;
            foreach (int index in new[] { i-2, i, i-1, i-1, i, i+1 }) mesh.TriangleIndices.Add(index);
        }
        mesh.Freeze(); var material=new EmissiveMaterial(ink); material.Freeze();
        return new(mesh, material) { BackMaterial=material };
    }
    internal static bool Supports(string? ship) => ship is "Rocinante" or "Donnager" or "Dropship" or "Truman" or "Epstein";
    internal static string ModelCredit(string ship) => ship switch { "Rocinante" => "JAKUB.VILDOMEC", "Truman" => "MOHAMEDHUSSIEN", _ => "CHRIS KUHN / OWLSTRAW" };
    internal static void CheckResources()
    {
        foreach (string ship in new[] { "Rocinante", "Donnager", "Dropship", "Truman", "Epstein" })
        {
            var parts=ShipModel.Load(ship);
            if (parts.Any(p => !p.Mesh.IsFrozen || !p.Original.IsFrozen || p.Mesh.Bounds.IsEmpty || p.Mesh.TextureCoordinates.Count!=p.Mesh.Positions.Count)
                || parts.Sum(p => p.Mesh.TriangleIndices.Count/3)>150000) throw new InvalidDataException("Holotable geometry exceeded its render budget: "+ship);
        }
    }
}
