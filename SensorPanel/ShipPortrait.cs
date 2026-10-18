using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Media3D;

namespace SensorPanel;

// The original painted hull, displayed upright in the dashboard's projection strip.
internal sealed class ShipPortrait : Grid
{
    private sealed record PortraitPose(int NosePolarity);
    // Sign of the nose along the source model's positive long axis, after axis alignment.
    // The yacht and Truman export with their engines at +Y; the other verified hulls face +Y.
    private static readonly IReadOnlyDictionary<string, PortraitPose> poses = new Dictionary<string, PortraitPose>
    {
        ["Rocinante"] = new(1), ["Donnager"] = new(1), ["Dropship"] = new(1),
        ["Epstein"] = new(-1), ["Razorback"] = new(1),
        ["Tynan"] = new(1), ["Morrigan"] = new(1), ["Canterbury"] = new(1), ["Knight"] = new(1),
        ["Anubis"] = new(1), ["Pella"] = new(1), ["Nauvoo"] = new(1), ["Behemoth"] = new(1),
        ["Medina"] = new(1), ["Scopuli"] = new(1), ["Scirocco"] = new(1), ["Hammurabi"] = new(1),
        ["Guanshiyin"] = new(1), ["Arboghast"] = new(1), ["AgathaKing"] = new(-1), ["BashiBazouk"] = new(1)
    };
    private readonly OrthographicCamera camera = new(new(0, 0, 6), new(0, 0, -6), new(0, 1, 0), 2);
    private readonly AxisAngleRotation3D yaw = new(new(0, 1, 0), -28);
    private readonly List<(GeometryModel3D Model, ShipModelPart Part)> hullParts = new();
    private readonly Rect3D uprightBounds;
    private readonly Vector3D portraitNose;
    private readonly double horizontalRadius;
    private bool motion = true;
    private bool loaded;

    public ShipPortrait(string ship)
    {
        ship=ShipProfile.CanonicalId(ship);
        if (!Supports(ship)) throw new ArgumentException("This ship has no original 3D model.", nameof(ship));
        ClipToBounds = true; IsHitTestVisible = false; Focusable = false;
        var hull = new Model3DGroup();
        var bounds = Rect3D.Empty;
        foreach (var part in ShipModel.Load(ship))
        {
            var model = new GeometryModel3D(part.Mesh, part.Original)
                { BackMaterial = part.DoubleSided ? part.Original : null };
            model.Freeze();
            hullParts.Add((model, part)); hull.Children.Add(model); bounds.Union(part.Mesh.Bounds);
        }
        if (bounds.IsEmpty) throw new InvalidOperationException("The ship has no visible geometry.");

        // Models arrive in different coordinate systems; align their measured long axis with screen Y.
        var upright = new Transform3DGroup();
        Vector3D sourceLongAxis = new(0, 1, 0);
        upright.Children.Add(new TranslateTransform3D(-bounds.X-bounds.SizeX/2,
            -bounds.Y-bounds.SizeY/2, -bounds.Z-bounds.SizeZ/2));
        if (bounds.SizeX > bounds.SizeY && bounds.SizeX >= bounds.SizeZ)
        {
            sourceLongAxis = new(1, 0, 0);
            upright.Children.Add(new RotateTransform3D(new AxisAngleRotation3D(new(0, 0, 1), 90)));
        }
        else if (bounds.SizeZ > bounds.SizeY)
        {
            sourceLongAxis = new(0, 0, 1);
            upright.Children.Add(new RotateTransform3D(new AxisAngleRotation3D(new(1, 0, 0), -90)));
        }
        var pose = poses[ship];
        if (pose.NosePolarity == -1)
            upright.Children.Add(new RotateTransform3D(new AxisAngleRotation3D(new(0, 0, 1), 180)));
        portraitNose = upright.Transform(sourceLongAxis * pose.NosePolarity);
        upright.Freeze();
        var visibleBounds=upright.TransformBounds(bounds);
        var emitters=ShipLighting.ForPortrait(hullParts.Select(p=>p.Part).ToArray(),upright,visibleBounds);
        visibleBounds.Union(emitters.Bounds); uprightBounds=visibleBounds;
        horizontalRadius = Math.Sqrt(uprightBounds.SizeX*uprightBounds.SizeX + uprightBounds.SizeZ*uprightBounds.SizeZ)/2;
        hull.Transform = upright; hull.Freeze();

        var rotating = new Model3DGroup { Transform = new RotateTransform3D(yaw) };
        rotating.Children.Add(hull); rotating.Children.Add(emitters);
        var scene = new Model3DGroup();
        // Lights stay fixed in view space while the hull rotates through them.
        // A low fill preserves the shaded side; the key reveals dark plating.
        scene.Children.Add(new AmbientLight(Color.FromScRgb(1,.035f,.04f,.05f)));
        float key=ship=="Pella" ? 2.4f : 1.15f;
        scene.Children.Add(new DirectionalLight(Color.FromScRgb(1,key,key*.94f,key*.9f), new(-1.5,-.65,-2)));
        scene.Children.Add(new DirectionalLight(Color.FromScRgb(1,.22f,.33f,.5f), new(1,.2,1.5)));
        scene.Children.Add(rotating);
        var view = new Viewport3D { Camera = camera, ClipToBounds = true, IsHitTestVisible = false };
        view.Children.Add(new ModelVisual3D { Content = scene }); Children.Add(view);

        double distance = Math.Max(4, horizontalRadius*3 + 1);
        camera.Position = new(0, uprightBounds.Y+uprightBounds.SizeY/2, distance); camera.LookDirection = new(0, 0, -distance);
        camera.NearPlaneDistance = .1; camera.FarPlaneDistance = distance + horizontalRadius + 2;
        SizeChanged += (_, _) => FitCamera();
        IsVisibleChanged += (_, _) => UpdateMotion();
        Loaded += (_, _) =>
        {
            if (!loaded) SystemParameters.StaticPropertyChanged += WindowsAnimationsChanged;
            loaded = true; FitCamera(); UpdateMotion();
        };
        Unloaded += (_, _) =>
        {
            if (loaded) SystemParameters.StaticPropertyChanged -= WindowsAnimationsChanged;
            loaded = false; StopMotion();
        };
        FitCamera();
    }

    public static bool Supports(string? ship) => ship is not null && poses.ContainsKey(ShipProfile.CanonicalId(ship));
    public bool MotionActive => yaw.HasAnimatedProperties;
    public void SetMotion(bool enabled) { motion = enabled; UpdateMotion(); }

    private double WidthForAspect(double aspect) => Math.Max(horizontalRadius*2, uprightBounds.SizeY*aspect)*1.16;
    private void FitCamera()
    {
        if (ActualWidth <= 0 || ActualHeight <= 0) return;
        camera.Width = Math.Max(.01, WidthForAspect(ActualWidth/ActualHeight));
    }
    private void WindowsAnimationsChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is null or nameof(SystemParameters.ClientAreaAnimation)) UpdateMotion();
    }
    private void StopMotion()
    {
        double angle = yaw.Angle;
        yaw.BeginAnimation(AxisAngleRotation3D.AngleProperty, null);
        yaw.Angle = ((angle % 360) + 360) % 360;
    }
    private void UpdateMotion()
    {
        if (!motion || !loaded || !IsVisible || !SystemParameters.ClientAreaAnimation) { StopMotion(); return; }
        if (MotionActive) return;
        var turn = new DoubleAnimation(yaw.Angle, yaw.Angle + 360, TimeSpan.FromSeconds(30))
            { RepeatBehavior = RepeatBehavior.Forever };
        Timeline.SetDesiredFrameRate(turn, 20);
        yaw.BeginAnimation(AxisAngleRotation3D.AngleProperty, turn);
    }

    internal void Check()
    {
        if (poses.Count != ShipProfile.All.Count || ShipProfile.All.Any(ship => !poses.ContainsKey(ship.Id))
            || poses["Rocinante"].NosePolarity != 1 || poses["Donnager"].NosePolarity != 1
            || poses["Dropship"].NosePolarity != 1 || poses["AgathaKing"].NosePolarity != -1 || poses["Epstein"].NosePolarity != -1
            || poses["Razorback"].NosePolarity != 1
            || (portraitNose - new Vector3D(0, 1, 0)).Length > 1e-8)
            throw new InvalidOperationException("Portrait pose must retain the verified per-ship nose/engine polarity.");
        if (uprightBounds.SizeY + 1e-8 < Math.Max(uprightBounds.SizeX, uprightBounds.SizeZ)
            || hullParts.Any(p => !p.Model.IsFrozen || !p.Part.Mesh.IsFrozen || !p.Part.Original.IsFrozen
                || !ReferenceEquals(p.Model.Material, p.Part.Original)))
            throw new InvalidOperationException("Portrait hull must stay upright with frozen original materials.");
        // Every corner must fit at every yaw, including unusually narrow and wide display strips.
        foreach (double aspect in new[] { .15, .3, .75, 2.0 })
        {
            double halfWidth = WidthForAspect(aspect)/2, halfHeight = halfWidth/aspect;
            for (int degrees = 0; degrees < 360; degrees += 30)
            {
                double angle = degrees*Math.PI/180;
                foreach (int x in new[] { -1, 1 }) foreach (int z in new[] { -1, 1 })
                {
                    double projected = x*uprightBounds.SizeX/2*Math.Cos(angle) + z*uprightBounds.SizeZ/2*Math.Sin(angle);
                    if (Math.Abs(projected) > halfWidth || uprightBounds.SizeY/2 > halfHeight)
                        throw new InvalidOperationException("Portrait camera clipped the rotating ship envelope.");
                }
            }
        }
        bool previous = motion; SetMotion(false);
        if (MotionActive) throw new InvalidOperationException("Portrait rotation did not stop.");
        SetMotion(previous);
    }
}
