using System.IO;
using System.Text.Json;
using System.Windows.Media.Imaging;

namespace SensorPanel;

// Media is optional and local. The application never downloads, uploads, or
// packages a user's scene folder. Missing images keep the built-in space view.
internal sealed record ExpanseScene(string ShipId, string Character, string Actor, string Scene,
    string? Background = null, string? Portrait = null, string Credit = "", string Source = "", string Association = "");

internal static class ExpanseMedia
{
    public static string Folder => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "X890Sensor", "ExpanseMedia");
    public static IReadOnlyList<string> Ships { get; } = ShipProfile.All.Select(ship => ship.Id).ToArray();
    private static readonly Dictionary<string, ExpanseScene> defaults = new()
    {
        ["Rocinante"] = new("Rocinante", "James Holden", "Steven Strait", "REMEMBER THE CANT", Association: "CAPTAIN / ROCINANTE"),
        ["Donnager"] = new("Donnager", "Theresa Yao", "Jean Yoon", "MARS / JUPITER FLEET", Association: "CAPTAIN / DONNAGER"),
        ["Truman"] = new("Truman", "Michael Souther", "Martin Roach", "AGATHA KING / TRUMAN CLASS", Association: "ADMIRAL / AGATHA KING"),
        ["Tynan"] = new("Tynan", "Camina Drummer", "Cara Gee", "BELT / SALVAGE OPERATIONS", Association: "CAPTAIN / TYNAN"),
        ["Razorback"] = new("Razorback", "Julie Mao", "Florence Faivre", "A LITTLE REBELLION", Association: "ORIGINAL RACING PILOT"),
        ["Morrigan"] = new("Morrigan", "", "", "MORRIGAN CLASS / PATROL", Association: "SHIP CLASS / CREW UNIDENTIFIED"),
        ["Dropship"] = new("Dropship", "", "", "MCRN / LANDING OPERATIONS", Association: "TRANSPORT TYPE / CREW UNIDENTIFIED"),
        ["Epstein"] = new("Epstein", "Solomon Epstein", "Sam Huntington", "THE DRIVE THAT OPENED THE SYSTEM", Association: "ENGINEER / TEST PILOT")
    };
    private static Dictionary<string, ExpanseScene>? scenes;
    private static DateTime manifestStamp;
    public static string Status { get; private set; } = "Built-in space scene";

    public static ExpanseScene ForShip(string ship)
    {
        // Check only when a scene is requested, never on the sensor poll path.
        // Local corrections become visible at the next scene selection.
        DateTime stamp;
        try { stamp = File.GetLastWriteTimeUtc(Path.Combine(Folder, "scenes.json")); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { stamp = manifestStamp; }
        if (scenes is null || stamp != manifestStamp) { scenes = Load(); manifestStamp = stamp; }
        return scenes.GetValueOrDefault(ship) ?? defaults["Rocinante"];
    }
    public static void Reload() => scenes = null;
    private static Dictionary<string, ExpanseScene> Load()
    {
        var result = defaults.ToDictionary(pair => pair.Key, pair => pair.Value);
        string path = Path.Combine(Folder, "scenes.json");
        Status = "Built-in space scene";
        if (!File.Exists(path)) return result;
        try
        {
            if (new FileInfo(path).Length > 128 * 1024) { Status = "Media manifest is too large"; return result; }
            var items = JsonSerializer.Deserialize<ExpanseScene?[]>(File.ReadAllText(path), new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            foreach (var item in items ?? [])
            {
                if (item is null || string.IsNullOrWhiteSpace(item.ShipId)) continue;
                if (!defaults.TryGetValue(item.ShipId, out var fallback)) continue;
                result[item.ShipId] = fallback with
                {
                    Background = SafeFile(item.Background), Portrait = SafeFile(item.Portrait),
                    Scene = Short(item.Scene, fallback.Scene), Character = Short(item.Character, fallback.Character),
                    Actor = Short(item.Actor, fallback.Actor), Credit = Short(item.Credit, ""), Source = Short(item.Source, "", 1000),
                    Association = Short(item.Association, fallback.Association)
                };
            }
            Status = "Local media pack";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or ArgumentException)
        { Status = "Media manifest unavailable; built-in space scene"; }
        return result;
    }

    private static string Short(string? value, string fallback, int length = 160) =>
        string.IsNullOrWhiteSpace(value) ? fallback : value.Trim()[..Math.Min(value.Trim().Length, length)];

    internal static string? SafeFile(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || Path.IsPathRooted(value) || value.Contains(':')) return null;
        string root = Path.GetFullPath(Folder) + Path.DirectorySeparatorChar;
        string path = Path.GetFullPath(Path.Combine(root, value));
        return path.StartsWith(root, StringComparison.OrdinalIgnoreCase) && File.Exists(path)
            && new[] { ".png", ".jpg", ".jpeg" }.Contains(Path.GetExtension(path).ToLowerInvariant()) ? path : null;
    }

    public static BitmapSource? Image(string? path, int width)
    {
        if (path is null) return null;
        try
        {
            if (new FileInfo(path).Length > 24 * 1024 * 1024) return null;
            using var stream = File.OpenRead(path);
            var bitmap = new BitmapImage(); bitmap.BeginInit(); bitmap.StreamSource = stream;
            bitmap.DecodePixelWidth = width; bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.EndInit(); bitmap.Freeze(); return bitmap;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException or ArgumentException or FormatException)
        { return null; }
    }

    public static string Next(string? current, int direction = 1)
    {
        int index = Ships.ToList().IndexOf(current ?? "Rocinante");
        return Ships[(index + direction % Ships.Count + Ships.Count) % Ships.Count];
    }

    internal static void Check()
    {
        if (Ships.Count != defaults.Count || Ships.Any(ship => !defaults.ContainsKey(ship))
            || Next(Ships[^1]) != Ships[0] || Next(Ships[0], -1) != Ships[^1]
            || SafeFile("../settings.json") is not null || SafeFile("https://example.invalid/photo.jpg") is not null)
            throw new InvalidOperationException("Expanse scene order or local media bounds failed.");
    }
}
