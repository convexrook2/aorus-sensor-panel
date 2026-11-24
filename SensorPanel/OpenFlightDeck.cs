using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace SensorPanel;

internal sealed partial class ShipPanel
{
    internal const string OpenFlightDeckSwitch = "X890.OpenFlightDeck";
    internal static bool OpenFlightDeckEnabled => !AppContext.TryGetSwitch(OpenFlightDeckSwitch, out bool enabled) || enabled;
    private readonly bool openFlightDeck = OpenFlightDeckEnabled;
    private static readonly FontFamily OpenDeckText = new("Segoe UI Variable Text, Segoe UI");
    private static readonly FontFamily OpenDeckDisplay = new("Segoe UI Variable Display, Segoe UI");
    private static readonly SolidColorBrush OpenDeckInk = DeckBrush(244, 247, 251);
    private static readonly SolidColorBrush OpenDeckMuted = DeckBrush(173, 190, 210);
    private static readonly SolidColorBrush OpenDeckSurface = DeckBrush(16, 32, 51);
    private static readonly SolidColorBrush OpenDeckRule = DeckBrush(51, 72, 96);
    private static readonly Color OpenDeckBase = Color.FromRgb(7, 11, 20);

    private static SolidColorBrush DeckBrush(byte red, byte green, byte blue)
    {
        var brush = new SolidColorBrush(Color.FromRgb(red, green, blue));
        brush.Freeze();
        return brush;
    }

    private static PanelTheme OpenDeckTheme(PanelTheme theme)
    {
        var (signal, secondary) = theme.ShipId switch
        {
            "Rocinante" => (DeckBrush(96, 205, 255), DeckBrush(179, 241, 255)),
            "AgathaKing" => (DeckBrush(122, 169, 255), DeckBrush(182, 218, 255)),
            "Donnager" => (DeckBrush(255, 113, 99), DeckBrush(255, 206, 135)),
            "Anubis" => (DeckBrush(187, 167, 255), DeckBrush(228, 216, 255)),
            "SolRing" => (DeckBrush(92, 241, 194), DeckBrush(186, 255, 222)),
            "Pella" => (DeckBrush(255, 110, 120), DeckBrush(255, 198, 134)),
            "Medina" => (DeckBrush(255, 208, 93), DeckBrush(119, 231, 209)),
            "Guanshiyin" => (DeckBrush(255, 203, 125), DeckBrush(255, 240, 191)),
            "Scirocco" => (DeckBrush(255, 102, 92), DeckBrush(255, 179, 162)),
            "Hammurabi" => (DeckBrush(255, 128, 119), DeckBrush(255, 203, 149)),
            _ => (theme.Accent, theme.ForGpu().Accent)
        };
        return theme with
        {
            Foreground = OpenDeckInk, Header = OpenDeckInk, Muted = OpenDeckMuted,
            Surface = OpenDeckSurface, Divider = OpenDeckRule, Accent = signal,
            SecondaryAccent = secondary, GraphBackground = Brushes.Transparent
        };
    }

    private void AddOpenDeckBackdrop(Plan plan, Color signal)
    {
        var bounds = new Rect(0, 0, plan.Width, plan.Height);
        Background = DeckBrush(OpenDeckBase.R, OpenDeckBase.G, OpenDeckBase.B);
        Put(this, new Border { IsHitTestVisible = false,
            Background = OpenDeckWash(Color.FromRgb(16, 30, 52), new(.70, .45), .64, .51) }, bounds);
        Put(this, new Border { IsHitTestVisible = false,
            Background = OpenDeckWash(Color.FromArgb(33, signal.R, signal.G, signal.B), new(.10, .41), .55, .36) }, bounds);
    }

    internal static Brush OpenDeckWash(Color color, Point center, double radiusX, double radiusY)
    {
        var brush = new RadialGradientBrush(color, Color.FromArgb(0, color.R, color.G, color.B))
        { Center = center, GradientOrigin = center, RadiusX = radiusX, RadiusY = radiusY };
        brush.Freeze();
        return brush;
    }

    private static Brush OpenDeckShipGlow(Color signal)
    {
        var glow = new RadialGradientBrush
        {
            Center = new Point(.49, .45), GradientOrigin = new Point(.49, .45),
            RadiusX = .49, RadiusY = .44
        };
        glow.GradientStops.Add(new GradientStop(Color.FromArgb(56, signal.R, signal.G, signal.B), 0));
        glow.GradientStops.Add(new GradientStop(Color.FromArgb(29, signal.R, signal.G, signal.B), .53));
        glow.GradientStops.Add(new GradientStop(Color.FromArgb(0, signal.R, signal.G, signal.B), 1));
        glow.Freeze();
        return glow;
    }

    private static Plan OpenDeckPlan(Plan plan)
    {
        plan = PellaPlan(plan);
        if (plan.Mode == "Portrait") return plan;
        const double reclaimed = 40;
        return plan with
        {
            Cpu = new(plan.Cpu.X, plan.Cpu.Y, plan.Cpu.Width, plan.Cpu.Height - reclaimed),
            Gpu = new(plan.Gpu.X, plan.Gpu.Y, plan.Gpu.Width, plan.Gpu.Height - reclaimed),
            Navigation = new(plan.Navigation.X, plan.Navigation.Y - reclaimed, plan.Navigation.Width, plan.Navigation.Height + reclaimed)
        };
    }

    private void AddOpenDeckIdentity(Plan plan, PanelTheme theme, ShipProfile vessel)
    {
        bool narrow = plan.Mode == "Portrait";
        var sidebar = plan.Sidebar;
        string name = vessel.Name.StartsWith(vessel.Faction + " ", StringComparison.Ordinal)
            ? vessel.Name[(vessel.Faction.Length + 1)..] : vessel.Name;
        string title = CultureInfo.InvariantCulture.TextInfo.ToTitleCase(name.ToLowerInvariant());
        if (!narrow && vessel.Name.StartsWith(vessel.Faction + " ", StringComparison.Ordinal)) title = vessel.Faction + "\n" + title;
        faction = Label(narrow ? vessel.Faction : vessel.Faction + " · " + vessel.Class,
            narrow ? 10 : 12, theme.Accent, true);
        Put(this, faction, new(sidebar.X+4, sidebar.Y+8, sidebar.Width-12, 16));

        heading = Label(title, narrow ? 19 : 38, theme.Foreground, true);
        heading.FontFamily = OpenDeckDisplay;
        heading.TextWrapping = TextWrapping.Wrap;
        heading.LineHeight = narrow ? 22 : 39;
        heading.LineStackingStrategy = LineStackingStrategy.BlockLineHeight;
        heading.ToolTip = vessel.Name;
        Put(this, heading, new(sidebar.X+4, sidebar.Y+27, sidebar.Width-12, narrow ? 53 : 82));

        string roleText = vessel.Id == "Scirocco" ? "Flight operations" : OpenDeckCaption(vessel.Role);
        var role = Label(roleText, narrow ? 10 : 14, theme.Muted);
        role.ToolTip = vessel.Role;
        double roleY = narrow ? 80 : title.Contains('\n') ? 111 : 80;
        Put(this, role, new(sidebar.X+4, sidebar.Y+roleY, sidebar.Width-12, 21));
        if (!narrow)
            Put(this, new Border { Background = theme.Divider, Opacity = .62, IsHitTestVisible = false },
                new(sidebar.Right+4, sidebar.Y+4, 1, sidebar.Height-20));

        if (!narrow)
        {
            Put(this, Label("X890", 22, theme.Accent, true), new(22, 20, 62, 29));
            Put(this, new Border { Background = theme.Divider }, new(87, 25, 1, 18));
            Put(this, Label("Sensor panel", 15, theme.Foreground, true), new(99, 23, 150, 22));
            Put(this, new Border { Background = theme.Divider }, new(22, 58, plan.Width-44, 1));
        }
        Put(this, new Border { Background = theme.Divider, IsHitTestVisible = false },
            new(plan.Cpu.X+16, plan.Navigation.Y-5, plan.Navigation.Width-32, 1));
        Put(this, new Border { Background = theme.Divider, IsHitTestVisible = false },
            new(plan.Board.X+16, plan.Board.Y+2, plan.Board.Width-32, 1));
    }

    private void OpenDeckHardware(Rect bounds, PanelTheme theme, bool gpu)
    {
        var group = MakeCard(bounds, theme);
        string processor = gpu ? "gpu" : "cpu";
        Put(group, Label(gpu ? "GPU" : "CPU", 15, theme.Accent, true), new(16, 5, 36, 21));
        Put(group, Bind(processor+"Name", 14, theme.Muted), new(59, 5, bounds.Width-75, 21));

        double traceX = bounds.Width < 420 ? 156 : 178;
        var temperature = Bind(processor+"Temperature", bounds.Width < 420 ? 48 : 57, theme.Foreground);
        temperature.FontFamily = OpenDeckDisplay;
        temperature.FontWeight = FontWeights.SemiBold;
        Typography.SetNumeralAlignment(temperature, FontNumeralAlignment.Tabular);
        var temperatureBox = new Viewbox { Child = temperature, Stretch = Stretch.Uniform,
            StretchDirection = StretchDirection.DownOnly, HorizontalAlignment = HorizontalAlignment.Left };
        Put(group, temperatureBox, new(16, 28, traceX-25, 66));

        var graph = gpu ? gpuGraph : cpuGraph;
        System.Windows.Automation.AutomationProperties.SetName(graph, gpu ? "Recent GPU temperatures" : "Recent CPU temperatures");
        Put(group, graph, new(traceX, 29, bounds.Width-traceX-16, 42));
        Put(group, Label("Peak", 12, theme.Muted), new(traceX, 75, 32, 18));
        Put(group, Bind(processor+"TemperatureMax", 12, theme.Muted), new(traceX+34, 75, 67, 18));
        var history = Label("Recent readings", 11, theme.Muted);
        history.TextAlignment = TextAlignment.Right;
        Put(group, history, new(traceX+101, 75, bounds.Width-traceX-117, 18));

        var fields = gpu
            ? new[] { ("Load", "gpuUsage", ""), ("Clock", "gpuClock", ""), ("Frame rate", "fps", ""),
                ("Power", "gpuPower", ""), ("Peak power", "gpuPowerMax", ""), ("VRAM clock", "gpuMemory", " MHz") }
            : new[] { ("Load", "cpuUsage", ""), ("Clock", "cpuClock", " MHz"), ("Power", "cpuPower", ""),
                ("Vcore", "voltage", ""), ("Peak power", "cpuPowerMax", ""), ("CCD hotspot", "cpuCcdMax", "") };
        double column = (bounds.Width-32)/3;
        for (int index = 0; index < fields.Length; index++)
        {
            var (label, key, suffix) = fields[index];
            Metric(group, label, key, suffix, new(16+index%3*column, 109+index/3*45, column-8, 42),
                theme, key == "fps" ? 28 : 22);
        }
    }

    private static string OpenDeckCaption(string caption)
    {
        string sentence = char.ToUpperInvariant(caption[0])+caption[1..].ToLowerInvariant();
        return sentence.Replace("Fps", "FPS", StringComparison.Ordinal)
            .Replace("Ram", "RAM", StringComparison.Ordinal)
            .Replace("Cpu", "CPU", StringComparison.Ordinal)
            .Replace("Gpu", "GPU", StringComparison.Ordinal)
            .Replace("Pch", "PCH", StringComparison.Ordinal)
            .Replace("Vrm mos", "VRM MOS", StringComparison.Ordinal);
    }
}
