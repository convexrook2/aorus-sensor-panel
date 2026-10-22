using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;
using System.IO;

namespace SensorPanel;

internal sealed record ShipProfile(string Id, string Name, string Faction, string Class, string Role, string Artist)
{
    public static IReadOnlyList<ShipProfile> All { get; } =
    [
        new("Rocinante", "ROCINANTE", "ROCI", "CORVETTE", "FLIGHT ENGINEERING", "MikeMac999"),
        new("Donnager", "MCRN DONNAGER", "MCRN", "BATTLESHIP", "TACTICAL ENGINEERING", "Ryan Begemann"),
        new("AgathaKing", "UNN AGATHA KING", "UNN", "TRUMAN CLASS", "FLEET ENGINEERING", "mohamedhussien"),
        new("Tynan", "OPAS TYNAN", "OPA", "SALVAGE SHIP", "SALVAGE ENGINEERING", "Frank Yang"),
        new("Razorback", "RAZORBACK", "RACE", "PINNACE", "PERFORMANCE TELEMETRY", "Ryan Begemann"),
        new("Morrigan", "MCRN MORRIGAN", "MCRN", "PATROL DESTROYER", "PATROL ENGINEERING", "Ryan Begemann"),
        new("Dropship", "MCRN DROPSHIP", "MCRN", "DROPSHIP", "LANDING ENGINEERING", "Ryan Begemann"),
        new("Epstein", "EPSTEIN'S YACHT", "MARS", "YACHT", "DRIVE ENGINEERING", "Chris Kuhn / owlstraw"),
        new("Canterbury", "CANTERBURY", "PUR'N'KLEEN", "ICE HAULER", "HAULAGE ENGINEERING", "Original fan model"),
        new("Knight", "KNIGHT", "PUR'N'KLEEN", "SHUTTLE", "SHUTTLE ENGINEERING", "Original fan model"),
        new("Anubis", "ANUBIS", "PROTOGEN", "STEALTH FRIGATE", "SYSTEMS ENGINEERING", "Original fan model"),
        new("Pella", "PELLA", "FREE NAVY", "LIGHT CRUISER", "FLEET ENGINEERING", "Martin Taichl / CC BY 4.0 · adapted"),
        new("Nauvoo", "LDSS NAUVOO", "LDSS", "GENERATION SHIP", "DRUM ENGINEERING", "Original fan model"),
        new("Behemoth", "OPAS BEHEMOTH", "OPA", "CONVERTED WARSHIP", "FLEET ENGINEERING", "Original fan model"),
        new("Medina", "MEDINA STATION", "OPA", "STATION / NAUVOO HULL", "STATION ENGINEERING", "Original fan model"),
        new("Scopuli", "SCOPULI", "OPA", "LIGHT FREIGHTER", "CARGO ENGINEERING", "Original fan model"),
        new("Scirocco", "MCRN SCIROCCO", "MCRN", "ASSAULT CRUISER", "MARINE ENGINEERING", "Original fan model"),
        new("Hammurabi", "MCRN HAMMURABI", "MCRN", "SCIROCCO CLASS", "FLEET ENGINEERING", "Original fan model"),
        new("Guanshiyin", "GUANSHIYIN", "MAO-KWIK", "YACHT", "YACHT ENGINEERING", "Original fan model"),
        new("Arboghast", "UNS ARBOGHAST", "UN", "SCIENCE VESSEL", "SURVEY ENGINEERING", "Original fan model"),
        new("BashiBazouk", "BASHI BAZOUK", "FREE NAVY", "BELTER SHIP", "CARGO ENGINEERING", "Reference: Lee Fitzgerald / North Front"),
        new("SolRing", "SOL RING", "RING BUILDERS", "GATE", "SYSTEMS MONITORING", "Original v3 fan reconstruction")
    ];
    internal static string CanonicalId(string id) => id == "Truman" ? "AgathaKing" : id;
    public static ShipProfile Resolve(string id) => All.Single(s => s.Id == CanonicalId(id));
}
// Crop one ship directly from the original embedded pixels. No regenerated art.
// Decode once at background resolution; selecting a theme does not decode again.
internal sealed class ShipArtwork : Image
{
    private static readonly Dictionary<string, BitmapSource> images = new();
    public ShipArtwork(PanelTheme theme, bool portrait)
    {
        var image = GetImage(theme.ShipId ?? throw new InvalidOperationException("A ship image needs its identity."));
        bool horizontal = image.PixelWidth >= image.PixelHeight;
        if (horizontal == portrait)
        {
            var rotated = new TransformedBitmap(image, new RotateTransform(90)); rotated.Freeze(); Source = rotated;
        }
        else Source = image;
        Stretch = Stretch.Uniform;
        Opacity = 0.42; IsHitTestVisible = false;
        OpacityMask = new RadialGradientBrush
        {
            Center = new Point(.5, .5), GradientOrigin = new Point(.5, .5), RadiusX = .68, RadiusY = .8,
            GradientStops = new GradientStopCollection { new(Colors.White, 0), new(Colors.White, .55), new(Colors.Transparent, 1) }
        };
        RenderOptions.SetBitmapScalingMode(this, BitmapScalingMode.HighQuality);
        ToolTip = theme.Vessel!.Name + " · " + theme.Vessel.Artist;
    }
    internal static Int32Rect Crop(string ship) => ship switch
    {
        "Donnager" => new(295, 1410, 635, 1640),
        "Truman" => new(285, 1410, 590, 1630),
        "Razorback" => new(250, 1450, 650, 1540),
        "Morrigan" => new(250, 1210, 580, 1790),
        "Dropship" => new(200, 2230, 1550, 730),
        "Rocinante" => new(280, 200, 850, 2650),
        "Tynan" => new(500, 160, 2720, 1950),
        _ => throw new InvalidOperationException("Missing ship crop: " + ship)
    };
    internal static BitmapSource GetImage(string ship)
    {
        ship=ShipProfile.CanonicalId(ship);
        if (images.TryGetValue(ship, out var existing)) return existing;
        images.Clear();
        if (ship == "Epstein" || ship is not ("Rocinante" or "Donnager" or "Truman" or "Tynan" or "Razorback" or "Morrigan" or "Dropship"))
        {
            // Render the actual downloaded hull once for its faded backdrop.
            // No invented blueprint or continuously running background scene.
            var scene = new Model3DGroup();
            scene.Children.Add(new AmbientLight(Color.FromRgb(110, 110, 110)));
            scene.Children.Add(new DirectionalLight(Colors.White, new Vector3D(-1, -2, -3)));
            scene.Children.Add(new DirectionalLight(Colors.White, new Vector3D(2, -1, 1)));
            var hull = new Model3DGroup();
            foreach (var part in ShipModel.Load(ship,cacheResult:false))
                hull.Children.Add(new GeometryModel3D(part.Mesh, part.Original) { BackMaterial = part.DoubleSided ? part.Original : null });
            hull.Transform = new RotateTransform3D(new AxisAngleRotation3D(new Vector3D(0, 1, 0), -58));
            scene.Children.Add(hull);
            var view = new Viewport3D { Width = 1600, Height = 900,
                Camera = new PerspectiveCamera(new Point3D(0, 2.1, 5.2), new Vector3D(0, -2.1, -5.2), new Vector3D(0, 1, 0), 43) };
            view.Children.Add(new ModelVisual3D { Content = scene });
            view.Measure(new Size(1600, 900)); view.Arrange(new Rect(0, 0, 1600, 900)); view.UpdateLayout();
            var rendered = new RenderTargetBitmap(1600, 900, 96, 96, PixelFormats.Pbgra32);
            rendered.Render(view); rendered.Freeze(); images.Add(ship, rendered); return rendered;
        }
        using var asset=Rookline.AssetProtection.AppAssets.Open("Ships/"+ship+".jpg");
        var crop = Crop(ship);
        var image = new BitmapImage();
        image.BeginInit(); image.StreamSource = asset;
        image.SourceRect = crop;
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.DecodePixelWidth = Math.Min(crop.Width, (int)Math.Round(1600.0 * crop.Width / Math.Max(crop.Width, crop.Height)));
        image.EndInit(); image.Freeze();
        // Remove the blueprint's blue wash so each faction palette survives
        // behind the original white ship lines, without redrawing anything.
        var neutral = new FormatConvertedBitmap(image, PixelFormats.Gray8, null, 0); neutral.Freeze();
        images.Add(ship, neutral); return neutral;
    }
    internal static void Check()
    {
        var themes = PanelTheme.All.Where(t => t.ShipConsole && !t.Showcase).ToArray();
        if (themes.Length != ShipProfile.All.Count || themes.Select(t => t.ShipId).Distinct().Count() != themes.Length)
            throw new InvalidOperationException("Every ship must have exactly one theme.");
        foreach (var theme in themes)
        {
              // Sol Ring uses only the full GLB viewport, never a legacy .ship
              // conversion or a pre-rendered image substitute.
              if(theme.ShipId=="SolRing")
              {
                  if(!Rookline.AssetProtection.AppAssets.Exists("FleetViewer/models/SolRing.glb.gz"))
                      throw new InvalidOperationException("The full gate model is missing.");
                  continue;
              }
            var image = GetImage(theme.ShipId!);
            if (!image.IsFrozen || image.PixelWidth <= 0 || image.PixelHeight <= 0)
                throw new InvalidOperationException("Ship image failed to load: " + theme.ShipId);
        }
        if (PanelTheme.Resolve("MCRN").ShipId != "Donnager" || PanelTheme.Resolve("UNN").ShipId != "AgathaKing"
            || PanelTheme.Resolve("Belter").ShipId != "Tynan")
            throw new InvalidOperationException("Legacy faction theme compatibility failed.");
    }
}
