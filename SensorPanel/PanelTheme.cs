using System.Windows;
using System.Windows.Media;

namespace SensorPanel;

internal sealed record PanelTheme(string Id, string Name, SolidColorBrush Background, SolidColorBrush Foreground,
    SolidColorBrush Muted, SolidColorBrush Accent, SolidColorBrush Divider, SolidColorBrush Surface,
    SolidColorBrush GraphBackground, SolidColorBrush Chrome, SolidColorBrush Header)
{
    public bool Classic => Id == "Classic";
    public bool IsDark => Luminance(Background.Color) < 0.25;
    public string Texture { get; init; } = "Plain";
    public string? ConsoleStyle { get; init; }
    public string? ShipId { get; init; }
    public bool Showcase => Id == "Expanse";
    public ShipProfile? Vessel => ShipId is null ? null : ShipProfile.Resolve(ShipId);
    public bool ShipConsole => ConsoleStyle is not null;
    public SolidColorBrush? SecondaryAccent { get; init; }
    public bool Detailed => Texture != "Plain";
    public PanelTheme ForGpu() => SecondaryAccent is null ? this : this with { Accent = SecondaryAccent };
    private static readonly Dictionary<string, Brush> backdrops = new();
    public Brush Backdrop
    {
        get
        {
            if (!Detailed) return Background;
            if (backdrops.TryGetValue(Id, out var existing)) return existing;
            var result = CreateBackdrop(); backdrops.Add(Id, result); return result;
        }
    }
    public static IReadOnlyList<PanelTheme> All { get; } =
    [
        Create("Classic", "AORUS Classic", "#E4E4E4", "#000000", "#6C6C6C", "#FF6400", "#B1B1B1", "#E4E4E4", "#434343", "#B1B1B1", "#494949"),
        Create("Dark", "AORUS Dark", "#151B24", "#ECF2F8", "#A3B1C2", "#FF7A1A", "#344353", "#202C3A", "#0C1118", "#0E141D", "#ECF2F8"),
        Create("ICE", "AORUS ICE", "#F1F5F8", "#142B40", "#526A7D", "#007BB5", "#BFCCD7", "#E3EDF4", "#D9E5ED", "#D4E0E9", "#25465F"),
        Create("Cyber", "Cyber Neon", "#080F1D", "#E5FAFF", "#9DB8CA", "#49E7FF", "#28425A", "#112333", "#060B14", "#06101D", "#73EDFF")
            with { Texture = "Grid", SecondaryAccent = Brush("#F875D8") },
        Create("Synthwave", "Synthwave", "#20152F", "#FFF1FB", "#CBB3DB", "#FF75C8", "#513960", "#302040", "#140E20", "#160E24", "#FF92D1")
            with { Texture = "Horizon", SecondaryAccent = Brush("#6EE9F4") },
        Create("Matrix", "Matrix Terminal", "#07110C", "#DBFFE6", "#96BDA3", "#62F790", "#254834", "#10251A", "#030B06", "#040D08", "#62F790")
            with { Texture = "Scanlines", SecondaryAccent = Brush("#D9F783") },
        Create("Carbon", "Carbon Gold", "#141414", "#FFF4DD", "#C3B79F", "#E9BE63", "#494132", "#24211B", "#0B0B0A", "#0C0C0B", "#F1CB80")
            with { Texture = "Carbon", SecondaryAccent = Brush("#F5DDA8") },
        Create("Crimson", "Crimson", "#180D12", "#FFF0F2", "#D4ADB5", "#FF6B81", "#57303D", "#2B1720", "#10070C", "#0F070B", "#FF8398")
            with { Texture = "Circuit", SecondaryAccent = Brush("#FFB179") },
        Create("Nord", "Nord Aurora", "#2E3440", "#ECEFF4", "#B9C6D8", "#88C0D0", "#4C566A", "#3B4252", "#252B35", "#242A34", "#D8DEE9")
            with { Texture = "Aurora", SecondaryAccent = Brush("#A3BE8C") },
        Create("Dracula", "Dracula", "#282A36", "#F8F8F2", "#C0BAD7", "#BD93F9", "#55516B", "#343746", "#191A21", "#21222C", "#D2B3FF")
            with { Texture = "Dots", SecondaryAccent = Brush("#FF79C6") },
        Create("Mocha", "Mocha", "#1E1E2E", "#CDD6F4", "#BAC2DE", "#CBA6F7", "#45475A", "#313244", "#181825", "#11111B", "#F5E0DC")
            with { Texture = "Soft", SecondaryAccent = Brush("#94E2D5") },
        Create("Expanse", "The Expanse", "#050A13", "#EDF7FA", "#B0C0D0", "#91E7FF", "#344D65", "#101C2D", "#060B0F", "#05090D", "#F1F8FF")
            with { Texture = "Soft", ConsoleStyle = "Roci", ShipId = "Rocinante", SecondaryAccent = Brush("#F3B56C") },
        Create("Rocinante", "Expanse · Rocinante", "#080D12", "#EDF7FA", "#A1B6C2", "#79D9EB", "#2B424D", "#101C24", "#060B0F", "#05090D", "#D9F5FA")
            with { Texture = "Soft", ConsoleStyle = "Roci", ShipId = "Rocinante", SecondaryAccent = Brush("#F3B56C") },
        Create("MCRN", "Expanse · Donnager", "#130D0C", "#FFF3E8", "#C8ADA0", "#FF9272", "#51372D", "#251A15", "#0C0806", "#090605", "#FFAC81")
            with { Texture = "Soft", ConsoleStyle = "Mars", ShipId = "Donnager", SecondaryAccent = Brush("#E9C080") },
        Create("UNN", "Expanse · Truman", "#0D151D", "#E9F1FA", "#ADBED2", "#97C9FF", "#354C63", "#1A2838", "#090F18", "#080E15", "#E9F1FA")
            with { Texture = "Soft", ConsoleStyle = "Earth", ShipId = "Truman", SecondaryAccent = Brush("#B9DCD7") },
        Create("Belter", "Expanse · Tynan", "#15130E", "#FAF0D9", "#C1B597", "#ECCC81", "#4D4534", "#242117", "#0D0C08", "#0C0B08", "#F3DFA7")
            with { Texture = "Soft", ConsoleStyle = "Belt", ShipId = "Tynan", SecondaryAccent = Brush("#AFC79C") },
        Create("Razorback", "Expanse · Razorback", "#100D17", "#FFF2FB", "#CDB5CC", "#F8A2C8", "#4A3452", "#211829", "#0B0810", "#0C0912", "#FFD0E4")
            with { Texture = "Soft", ConsoleStyle = "Race", ShipId = "Razorback", SecondaryAccent = Brush("#8BE6EB") },
        Create("Morrigan", "Expanse · Morrigan", "#0B1413", "#EAF8EF", "#ABC5B8", "#9DD7B2", "#304B40", "#16241F", "#060E0B", "#070F0C", "#C2E9C9")
            with { Texture = "Soft", ConsoleStyle = "Patrol", ShipId = "Morrigan", SecondaryAccent = Brush("#F0C685") },
        Create("Dropship", "Expanse · Dropship", "#16130D", "#FFF6E7", "#CDBD9E", "#F5C16C", "#50432D", "#282116", "#100D07", "#100D08", "#FFE1A6")
            with { Texture = "Soft", ConsoleStyle = "Landing", ShipId = "Dropship", SecondaryAccent = Brush("#A7CBE2") },
        Create("Epstein", "Expanse · Epstein's Yacht", "#08131E", "#F0F6FF", "#A8BCD0", "#9CCFFF", "#2C4A68", "#14283B", "#060E18", "#070F18", "#E0EFFF")
            with { Texture = "Soft", ConsoleStyle = "Roci", ShipId = "Epstein", SecondaryAccent = Brush("#F2AE69") }
    ];
    public static PanelTheme Resolve(string? id) => All.FirstOrDefault(t => string.Equals(t.Id, id, StringComparison.OrdinalIgnoreCase)) ?? All[0];
    private static SolidColorBrush Brush(string value)
    {
        var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(value));
        brush.Freeze(); return brush;
    }
    private static PanelTheme Create(string id, string name, params string[] colors)
    {
        var brushes = colors.Select(Brush).ToArray();
        return new(id, name, brushes[0], brushes[1], brushes[2], brushes[3], brushes[4], brushes[5], brushes[6], brushes[7], brushes[8]);
    }

    private Brush CreateBackdrop()
    {
        var bounds = new Rect(0, 0, 1024, 576);
        var drawing = new DrawingGroup { ClipGeometry = new RectangleGeometry(bounds) };
        using (var context = drawing.Open())
        {
            var wash = new LinearGradientBrush(Background.Color, Surface.Color, new Point(0, 0), new Point(1, 1));
            context.DrawRectangle(wash, null, new Rect(0, 0, 1024, 576));
            context.PushOpacity(0.12);
            if (Texture is "Aurora" or "Soft" or "Grid" or "Circuit" or "Horizon" or "Carbon" or "Scanlines" or "Dots")
            {
                var glow = new RadialGradientBrush(Accent.Color, Colors.Transparent)
                { Center = new Point(0.8, 0.05), GradientOrigin = new Point(0.8, 0.05), RadiusX = 0.85, RadiusY = 0.75 };
                context.DrawRectangle(glow, null, new Rect(0, 0, 1024, 576));
            }
            context.Pop();
            if (Texture == "Horizon")
            {
                context.PushOpacity(0.06);
                context.DrawEllipse(SecondaryAccent, null, new Point(840, 85), 190, 190);
                context.Pop();
            }
        }
        var result = new DrawingBrush(drawing) { Stretch = Stretch.Fill, ViewboxUnits = BrushMappingMode.Absolute, Viewbox = bounds };
        result.Freeze(); return result;
    }

    private static double Luminance(Color color)
    {
        static double Linear(byte value) { double c = value / 255.0; return c <= 0.04045 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4); }
        return 0.2126 * Linear(color.R) + 0.7152 * Linear(color.G) + 0.0722 * Linear(color.B);
    }
    internal static void Check()
    {
        if (All.Select(t => t.Id).Distinct(StringComparer.OrdinalIgnoreCase).Count() != All.Count)
            throw new InvalidOperationException("Theme IDs must be unique.");
        foreach (var theme in All)
        {
            if (theme.ShipConsole != (theme.Vessel is not null))
                throw new InvalidOperationException($"Theme {theme.Id} is missing its ship identity.");
            foreach (var background in new[] { theme.Background, theme.Surface })
            foreach (var foreground in new[] { theme.Foreground, theme.Muted })
            {
                double a = Luminance(foreground.Color), b = Luminance(background.Color);
                // Classic retains the supplied skin's original grey labels.
                // New palettes must meet 4.5:1 on both plain and menu surfaces.
                if (!theme.Classic && (Math.Max(a, b) + 0.05) / (Math.Min(a, b) + 0.05) < 4.5)
                    throw new InvalidOperationException($"Theme {theme.Id} has unreadable text contrast.");
            }
            if (!theme.Backdrop.IsFrozen || !theme.ForGpu().Accent.IsFrozen || Resolve(theme.Id.ToLowerInvariant()) != theme)
                throw new InvalidOperationException($"Theme {theme.Id} resources or lookup failed.");
            if (theme.Backdrop is DrawingBrush backdrop && backdrop.Drawing.Bounds != new Rect(0, 0, 1024, 576))
                throw new InvalidOperationException($"Theme {theme.Id} decoration escaped the background canvas.");
        }
        ShipArtwork.Check();
    }
}
