using Microsoft.Win32;

namespace SensorCollector;

public static class ManualGameRegistration
{
    private const string Key = @"Software\X890Sensor\FpsGames";
    internal const string Source = "Manually registered game";
    private static int revision;
    internal static int Revision => Volatile.Read(ref revision);

    public static IReadOnlyList<InstalledGame> List()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(Key);
            if (key is null) return [];
            return key.GetValueNames().Select(path => OtherGameCatalog.ExactEntry(path, Source)).OfType<InstalledGame>().ToArray();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException) { return []; }
    }

    public static void Add(string executable)
    {
        var game = OtherGameCatalog.ExactEntry(executable, Source) ?? throw new ArgumentException("Select an existing game executable.");
        using var key = Registry.CurrentUser.CreateSubKey(Key);
        key.SetValue(game.Executable!, game.Name, RegistryValueKind.String);
        Interlocked.Increment(ref revision);
    }

    public static void Remove(string executable)
    {
        using var key = Registry.CurrentUser.OpenSubKey(Key, writable: true);
        key?.DeleteValue(executable, throwOnMissingValue: false);
        Interlocked.Increment(ref revision);
    }
}
