using System.IO;
using System.Text.Json;

namespace SensorPanel;

internal static class PanelPreferences
{
    private sealed record Preferences(string DisplayName = "", string MonitorId = "", int PollingIntervalMs = 1000,
        bool? FitDisplay = null, string Layout = "Auto", string Scaling = "Fit", string Theme = "Classic", bool HolographicMotion = true,
        bool ReadoutPulse = false, bool GaugeTransitions = true, bool ExpanseCycle = true,
        int ExpanseSceneSeconds = 45, string ExpanseShip = "Rocinante", bool ShowcaseRotation = true,
        string ExpanseRendering = "FullFidelity", string AorusTheme = "", string ExpanseTheme = "");
    private static string FilePath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "X890Sensor", "settings.json");
    private static Preferences Load()
    {
        try { return File.Exists(FilePath) ? JsonSerializer.Deserialize<Preferences>(File.ReadAllText(FilePath)) ?? new() : new(); }
        catch (IOException) { return new(); }
        catch (JsonException) { return new(); }
    }
    public static int PollingInterval => ValidPollingInterval(Load().PollingIntervalMs);
    public static string Layout => Load().Layout;
    public static string Scaling => Load().Scaling;
    public static string Theme => PanelTheme.Resolve(Load().Theme).Id;
    public static bool HolographicMotion => Load().HolographicMotion;
    public static bool ShowcaseRotation => Load().ShowcaseRotation;
    public static ExpanseGraphicsMode GraphicsMode => Enum.TryParse<ExpanseGraphicsMode>(Load().ExpanseRendering,out var mode)&&Enum.IsDefined(mode)?mode:ExpanseGraphicsMode.FullFidelity;
    public static string ExperienceTheme(bool expanse)
    {
        var saved=Load();string id=expanse?saved.ExpanseTheme:saved.AorusTheme;
        if(!string.IsNullOrEmpty(id)&&PanelTheme.Resolve(id).ShipConsole==expanse)return PanelTheme.Resolve(id).Id;
        return PanelTheme.Resolve(saved.Theme).ShipConsole==expanse?PanelTheme.Resolve(saved.Theme).Id:expanse?"Expanse":"Classic";
    }
    public static void SaveGraphicsMode(ExpanseGraphicsMode mode)=>Write(Load() with {ExpanseRendering=mode.ToString()});
    internal static void CheckExperiencePersistence()
    {
        var legacy=JsonSerializer.Deserialize<Preferences>("{\"Theme\":\"MCRN\",\"PollingIntervalMs\":2000,\"ShowcaseRotation\":false}")!;
        var roundTrip=JsonSerializer.Deserialize<Preferences>(JsonSerializer.Serialize(legacy with {ExpanseRendering="Efficient",AorusTheme="Dark",ExpanseTheme="MCRN"}))!;
        if(legacy.ExpanseRendering!="FullFidelity"||roundTrip.ExpanseRendering!="Efficient"||roundTrip.AorusTheme!="Dark"||roundTrip.ExpanseTheme!="MCRN"
            ||roundTrip.Theme!="MCRN"||roundTrip.PollingIntervalMs!=2000||roundTrip.ShowcaseRotation)
            throw new InvalidOperationException("Experience preferences failed migration/serialization or altered unrelated settings.");
    }
    public static bool ReadoutPulse => Load().ReadoutPulse;
    public static bool GaugeTransitions => Load().GaugeTransitions;
    public static bool ExpanseCycle => Load().ExpanseCycle;
    public static int ExpanseSceneSeconds => Load().ExpanseSceneSeconds is >= 15 and <= 120 ? Load().ExpanseSceneSeconds : 45;
    public static string ExpanseShip => ExpanseMedia.Ships.Contains(ShipProfile.CanonicalId(Load().ExpanseShip)) ? ShipProfile.CanonicalId(Load().ExpanseShip) : "Rocinante";
    public static void SaveExpanseCycle(bool enabled) => Write(Load() with { ExpanseCycle = enabled });
    public static void SaveExpanseShip(string ship) => Write(Load() with { ExpanseShip = ExpanseMedia.Ships.Contains(ShipProfile.CanonicalId(ship)) ? ShipProfile.CanonicalId(ship) : "Rocinante" });
    public static void SaveExpanseSceneSeconds(int value) => Write(Load() with { ExpanseSceneSeconds = value is >= 15 and <= 120 ? value : 45 });
    public static void SaveHolographicMotion(bool enabled) => Write(Load() with { HolographicMotion = enabled });
    public static void SaveShowcaseRotation(bool enabled) => Write(Load() with { ShowcaseRotation = enabled });
    public static void SaveReadoutPulse(bool enabled) => Write(Load() with { ReadoutPulse = enabled });
    public static void SaveGaugeTransitions(bool enabled) => Write(Load() with { GaugeTransitions = enabled });
    public static void SaveTheme(string theme)
    {
        var choice=PanelTheme.Resolve(theme);var saved=Load();
        Write(saved with {Theme=choice.Id,AorusTheme=choice.ShipConsole?saved.AorusTheme:choice.Id,ExpanseTheme=choice.ShipConsole?choice.Id:saved.ExpanseTheme});
    }
    public static void SaveLayout(string layout) => Write(Load() with { Layout = layout });
    public static void SaveScaling(string scaling) => Write(Load() with { Scaling = scaling });
    public static bool AutomaticFit(DisplayInfo? display)
    {
        if (display is not { Primary: false }) return false;
        var saved = Load();
        return ShouldFit(display,saved.MonitorId,saved.DisplayName,saved.FitDisplay);
    }
    public static bool ShowInitially(DisplayInfo? display)
    {
        var saved = Load();
        return ShouldShow(display,saved.MonitorId,saved.DisplayName);
    }
    internal static bool ShouldShow(DisplayInfo? display,string savedId,string savedName) => display is not null &&
        ((!string.IsNullOrWhiteSpace(savedId) && string.Equals(savedId,display.MonitorId,StringComparison.OrdinalIgnoreCase))
        || (!string.IsNullOrWhiteSpace(savedName) && string.Equals(savedName,display.Name,StringComparison.OrdinalIgnoreCase)));
    internal static bool ShouldFit(DisplayInfo? display,string savedId,string savedName,bool? fit) => display is { Primary:false }
        && ShouldShow(display,savedId,savedName) && fit==true;
    internal static int ValidPollingInterval(int value) => value is >= 500 and <= 5000 ? value : 1000;
    private static void Write(Preferences value)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        File.WriteAllText(FilePath + ".tmp", JsonSerializer.Serialize(value));
        File.Move(FilePath + ".tmp", FilePath, true);
    }
    public static void SavePollingInterval(int value)
    {
        if (value is < 500 or > 5000) throw new ArgumentOutOfRangeException(nameof(value));
        Write(Load() with { PollingIntervalMs = value });
    }
    public static DisplayInfo? Preferred(IReadOnlyList<DisplayInfo> displays)
    {
        var saved = Load();
        return SelectDisplay(displays, saved.MonitorId, saved.DisplayName);
    }
    internal static DisplayInfo? SelectDisplay(IReadOnlyList<DisplayInfo> displays, string savedMonitorId, string savedName)
    {
        var saved = (!string.IsNullOrWhiteSpace(savedMonitorId)
            ? displays.FirstOrDefault(d => string.Equals(d.MonitorId, savedMonitorId, StringComparison.OrdinalIgnoreCase)) : null)
            ?? (!string.IsNullOrWhiteSpace(savedName)
                ? displays.FirstOrDefault(d => string.Equals(d.Name, savedName, StringComparison.OrdinalIgnoreCase)) : null);
        return saved ?? displays.FirstOrDefault(d => !d.Primary) ?? displays.FirstOrDefault();
    }
    internal static void CheckDisplaySelection()
    {
        var primary = new DisplayInfo("primary", 0, 0, 3840, 2160, true, "DISPLAY\\EXAMPLE-PRIMARY");
        var sensor = new DisplayInfo("sensor", 3840, 0, 1920, 1080, false, "DISPLAY\\EXAMPLE-SECONDARY");
        DisplayInfo[] screens = [primary, sensor];
        if (SelectDisplay(screens, "", "") != sensor ||
            SelectDisplay(screens, "removed-monitor", "removed-display") != sensor ||
            SelectDisplay(screens, "display\\example-secondary", "") != sensor ||
            SelectDisplay(screens, primary.MonitorId, "") != primary ||
            SelectDisplay([primary], sensor.MonitorId, sensor.Name) != primary ||
            SelectDisplay([], "", "") is not null || ShouldShow(sensor,"","") || ShouldFit(sensor,"","",true)
            || !ShouldShow(sensor,sensor.MonitorId,"") || !ShouldFit(sensor,sensor.MonitorId,"",true)
            || ShouldFit(primary,primary.MonitorId,"",true) || ShouldFit(sensor,sensor.MonitorId,"",false))
            throw new InvalidOperationException("Empty/stale preferences must fall back to an available display while respecting an explicit connected selection.");
    }
    public static void Save(DisplayInfo display, bool? fit = null)
    {
        var saved = Load();
        Write(saved with { DisplayName = display.Name, MonitorId = display.MonitorId, FitDisplay = fit ?? saved.FitDisplay });
    }
}
