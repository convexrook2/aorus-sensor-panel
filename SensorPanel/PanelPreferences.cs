using System.IO;
using System.Text.Json;

namespace SensorPanel;

internal static class PanelPreferences
{
    private sealed record Preferences(string DisplayName = "", string MonitorId = "", int PollingIntervalMs = 1000,
        bool? FitDisplay = null, string Layout = "Auto", string Scaling = "Fit", string Theme = "Classic", bool HolographicMotion = true,
        bool ReadoutPulse = false, bool GaugeTransitions = true);
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
    public static bool ReadoutPulse => Load().ReadoutPulse;
    public static bool GaugeTransitions => Load().GaugeTransitions;
    public static void SaveHolographicMotion(bool enabled) => Write(Load() with { HolographicMotion = enabled });
    public static void SaveReadoutPulse(bool enabled) => Write(Load() with { ReadoutPulse = enabled });
    public static void SaveGaugeTransitions(bool enabled) => Write(Load() with { GaugeTransitions = enabled });
    public static void SaveTheme(string theme) => Write(Load() with { Theme = PanelTheme.Resolve(theme).Id });
    public static void SaveLayout(string layout) => Write(Load() with { Layout = layout });
    public static void SaveScaling(string scaling) => Write(Load() with { Scaling = scaling });
    public static bool AutomaticFit(DisplayInfo? display)
    {
        if (display is not { Primary: false }) return false;
        var saved = Load();
        bool selected = (!string.IsNullOrEmpty(saved.MonitorId) && string.Equals(saved.MonitorId, display.MonitorId, StringComparison.OrdinalIgnoreCase))
            || (!string.IsNullOrEmpty(saved.DisplayName) && string.Equals(saved.DisplayName, display.Name, StringComparison.OrdinalIgnoreCase));
        return selected && saved.FitDisplay.HasValue ? saved.FitDisplay.Value
            : display.MonitorId.Contains("EXAMPLE-SECONDARY", StringComparison.OrdinalIgnoreCase);
    }
    public static bool ShowInitially(DisplayInfo? display)
    {
        if (display is null) return false;
        var saved = Load();
        return display.MonitorId.Contains("EXAMPLE-SECONDARY", StringComparison.OrdinalIgnoreCase)
            || (!string.IsNullOrWhiteSpace(saved.MonitorId) && string.Equals(saved.MonitorId,display.MonitorId,StringComparison.OrdinalIgnoreCase))
            || (!string.IsNullOrWhiteSpace(saved.DisplayName) && string.Equals(saved.DisplayName,display.Name,StringComparison.OrdinalIgnoreCase));
    }
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
        return saved ?? displays.FirstOrDefault(d => d.MonitorId.Contains("EXAMPLE-SECONDARY", StringComparison.OrdinalIgnoreCase))
            ?? displays.FirstOrDefault(d => !d.Primary) ?? displays.FirstOrDefault();
    }
    internal static void CheckDisplaySelection()
    {
        var primary = new DisplayInfo("primary", 0, 0, 3840, 2160, true, "DISPLAY\\EXAMPLE-PRIMARY");
        var sensor = new DisplayInfo("sensor", 3840, 0, 1920, 1080, false, "DISPLAY\\EXAMPLE-SECONDARY");
        DisplayInfo[] screens = [primary, sensor];
        if (SelectDisplay(screens, "", "") != sensor ||
            SelectDisplay(screens, "removed-monitor", "removed-display") != sensor ||
            SelectDisplay(screens, "display\\EXAMPLE-SECONDARY", "") != sensor ||
            SelectDisplay(screens, primary.MonitorId, "") != primary ||
            SelectDisplay([primary], sensor.MonitorId, sensor.Name) != primary ||
            SelectDisplay([], "", "") is not null)
            throw new InvalidOperationException("Empty/stale preferences must fall back to an available display while respecting an explicit connected selection.");
    }
    public static void Save(DisplayInfo display, bool? fit = null)
    {
        var saved = Load();
        Write(saved with { DisplayName = display.Name, MonitorId = display.MonitorId, FitDisplay = fit ?? saved.FitDisplay });
    }
}
