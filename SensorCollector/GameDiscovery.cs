using Microsoft.Win32;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace SensorCollector;

public sealed record InstalledGame(string Name, string Directory, string Source);
public sealed record RunningGame(int ProcessId, string Name, string Executable, DateTime StartedAt, string Source);

public sealed class GameDiscovery
{
    private DateTimeOffset nextRefresh;
    private IReadOnlyList<InstalledGame> games = [];
    public string Diagnostic { get; private set; } = "Game catalog not loaded.";
    public IReadOnlyList<InstalledGame> Installed { get { Refresh(); return games; } }

    public IReadOnlyList<RunningGame> Running()
    {
        Refresh(); var running = new List<RunningGame>();
        foreach (var process in Process.GetProcesses())
        {
            using (process)
            {
                try
                {
                    if (process.Id == Environment.ProcessId || process.MainWindowHandle == 0) continue;
                    string? path = ImagePath(process.Id);
                    if (path is null) continue;
                    var game = Match(path, games);
                    if (game is not null) running.Add(new(process.Id, game.Name, path, process.StartTime.ToUniversalTime(), game.Source));
                }
                catch (System.ComponentModel.Win32Exception) { }
                catch (InvalidOperationException) { }
            }
        }
        return running;
    }

    public static RunningGame? Select(IReadOnlyList<RunningGame> running, int foregroundPid, int previousPid)
        => Select(running, foregroundPid, previousPid, 0);

    public static RunningGame? Select(IReadOnlyList<RunningGame> running, int foregroundPid, int previousPid, int requestedPid)
        => requestedPid > 0 ? running.FirstOrDefault(g => g.ProcessId == requestedPid)
        : running.FirstOrDefault(g => g.ProcessId == foregroundPid)
        ?? running.FirstOrDefault(g => g.ProcessId == previousPid)
        ?? running.OrderByDescending(g => g.StartedAt).ThenBy(g => g.ProcessId).FirstOrDefault();

    internal static bool IsGameType(string type) => type.Equals("game", StringComparison.OrdinalIgnoreCase) || type.Equals("demo", StringComparison.OrdinalIgnoreCase);
    internal static InstalledGame? Match(string executable, IReadOnlyList<InstalledGame> installed)
    {
        string name = Path.GetFileNameWithoutExtension(executable);
        if (new[] { "launcher", "crash", "helper", "setup", "updater", "server", "editor", "config", "webview", "cef" }
            .Any(marker => name.Contains(marker, StringComparison.OrdinalIgnoreCase))) return null;
        return installed.FirstOrDefault(g => Inside(executable, g.Directory));
    }
    internal static bool Inside(string file, string directory)
        => Path.GetFullPath(file).StartsWith(Path.GetFullPath(directory).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);

    private void Refresh()
    {
        if (DateTimeOffset.UtcNow < nextRefresh) return;
        nextRefresh = DateTimeOffset.UtcNow.AddSeconds(30);
        var result = new List<InstalledGame>(); var notes = new List<string>();
        try { LoadSteam(result, notes); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException or ArgumentException) { notes.Add("Steam catalog unavailable: " + ex.Message); }
        // Unknown catalogs do not turn arbitrary foreground processes into games.
        games = result;
        Diagnostic = $"Recognized {games.Count} installed games. " + string.Join(" ", notes);
    }

    private static void LoadSteam(List<InstalledGame> result, List<string> notes)
    {
        using var steamKey = Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam");
        string? steamRoot = Convert.ToString(steamKey?.GetValue("SteamPath"));
        if (string.IsNullOrEmpty(steamRoot)) { notes.Add("Steam install path not found."); return; }
        var libraries = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { Path.GetFullPath(steamRoot) };
        string libraryFile = Path.Combine(steamRoot, "steamapps", "libraryfolders.vdf");
        if (File.Exists(libraryFile)) foreach (Match match in Regex.Matches(File.ReadAllText(libraryFile), "\"path\"\\s+\"([^\"]+)\"")) libraries.Add(Path.GetFullPath(match.Groups[1].Value.Replace("\\\\", "\\")));
        var installed = new List<(uint AppId, string Name, string Directory)>();
        foreach (string library in libraries)
        {
            string steamApps = Path.Combine(library, "steamapps"); if (!System.IO.Directory.Exists(steamApps)) continue;
            foreach (string manifest in System.IO.Directory.EnumerateFiles(steamApps, "appmanifest_*.acf"))
            {
                string text = File.ReadAllText(manifest);
                string Field(string name) => Regex.Match(text, "\"" + name + "\"\\s+\"([^\"]+)\"").Groups[1].Value;
                if (!uint.TryParse(Field("appid"), out uint appId) || string.IsNullOrEmpty(Field("installdir"))) continue;
                string common = Path.Combine(steamApps, "common"), root = Path.GetFullPath(Path.Combine(common, Field("installdir")));
                if (Inside(root, common) && System.IO.Directory.Exists(root)) installed.Add((appId, Field("name"), root));
            }
        }
        var metadata = SteamAppCache.Read(Path.Combine(steamRoot, "appcache", "appinfo.vdf"), installed.Select(g => g.AppId).ToHashSet());
        foreach (var entry in installed)
            if (metadata.TryGetValue(entry.AppId, out var app) && IsGameType(app.Type))
                result.Add(new(app.Name, entry.Directory, $"Steam app {entry.AppId}; common/type={app.Type}"));
        notes.Add("Steam tools/applications excluded using local app type; no store network request.");
    }

    internal static string? ImagePath(int pid)
    {
        nint handle = OpenProcess(0x1000, false, (uint)pid); if (handle == 0) return null;
        try { var name = new StringBuilder(32768); int length = name.Capacity; return QueryFullProcessImageName(handle, 0, name, ref length) ? name.ToString() : null; }
        finally { CloseHandle(handle); }
    }
    [DllImport("kernel32.dll", SetLastError = true)] private static extern nint OpenProcess(uint access, [MarshalAs(UnmanagedType.Bool)] bool inherit, uint processId);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool QueryFullProcessImageName(nint process, uint flags, StringBuilder name, ref int length);
    [DllImport("kernel32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool CloseHandle(nint handle);
}
