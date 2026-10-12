using System.IO;
using System.Text.Json;

namespace SensorPanel;

internal static class PanelPreferences
{
    private sealed record Preferences(string DisplayName = "", string MonitorId = "", int PollingIntervalMs = 1000);
    private static string FilePath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "X890Sensor", "settings.json");
    private static Preferences Load()
    {
        try { return File.Exists(FilePath) ? JsonSerializer.Deserialize<Preferences>(File.ReadAllText(FilePath)) ?? new() : new(); }
        catch (IOException) { return new(); }
        catch (JsonException) { return new(); }
    }
    public static int PollingInterval => ValidPollingInterval(Load().PollingIntervalMs);
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
        try
        {
            if (File.Exists(FilePath))
            {
                var saved = JsonSerializer.Deserialize<Preferences>(File.ReadAllText(FilePath));
                if (saved is not null)
                    return displays.FirstOrDefault(d => d.MonitorId.Length > 0 && d.MonitorId == saved.MonitorId)
                        ?? displays.FirstOrDefault(d => d.Name == saved.DisplayName);
            }
        }
        catch (IOException) { }
        catch (JsonException) { }
        return displays.FirstOrDefault(d => d.MonitorId.Contains("EXAMPLE-SECONDARY", StringComparison.OrdinalIgnoreCase))
            ?? displays.FirstOrDefault(d => !d.Primary) ?? displays.FirstOrDefault();
    }
    public static void Save(DisplayInfo display)
    {
        Write(Load() with { DisplayName = display.Name, MonitorId = display.MonitorId });
    }
}
