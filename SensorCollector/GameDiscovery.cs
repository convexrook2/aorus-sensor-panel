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
    private Catalog catalog = new([]);
    public string Diagnostic { get; private set; } = "Game catalog not loaded.";
    public IReadOnlyList<InstalledGame> Installed { get { Refresh(); return games; } }

    public IReadOnlyList<RunningGame> Running()
    {
        Refresh(); var running = new List<RunningGame>();
        // Enumerating Process.GetProcesses builds a system-wide process/thread
        // snapshot every poll. Only visible, ownerless windows can qualify as
        // a MainWindowHandle, so enumerate those HWNDs and inspect their PIDs.
        var candidates = new HashSet<int>();
        EnumWindows((window, _) =>
        {
            if (GetWindow(window, 4) == 0 && IsWindowVisible(window))
            {
                GetWindowThreadProcessId(window, out uint pid);
                if (pid > 0 && pid != Environment.ProcessId) candidates.Add((int)pid);
            }
            return true;
        }, 0);
        foreach (int pid in candidates)
        {
            try
            {
                string? path = ImagePath(pid);
                if (path is null) continue;
                var game = catalog.Match(path);
                if (game is null) continue;
                using var process = Process.GetProcessById(pid);
                running.Add(new(pid, game.Name, path, process.StartTime.ToUniversalTime(), game.Source));
            }
            catch (System.ComponentModel.Win32Exception) { }
            catch (InvalidOperationException) { }
            catch (ArgumentException) { }
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
        =>new Catalog(installed).Match(executable);
    internal sealed class Catalog(IReadOnlyList<InstalledGame> installed)
    {
        private static readonly string[] excluded=["launcher","crash","helper","setup","updater","server","editor","config","webview","cef"];
        private readonly (InstalledGame Game,string Prefix)[] paths=installed.Select(g=>(g,Path.GetFullPath(g.Directory).TrimEnd(Path.DirectorySeparatorChar,Path.AltDirectorySeparatorChar)+Path.DirectorySeparatorChar)).ToArray();
        internal InstalledGame? Match(string executable)
        {
            string name=Path.GetFileNameWithoutExtension(executable);
            foreach(string marker in excluded)if(name.Contains(marker,StringComparison.OrdinalIgnoreCase))return null;
            string full=Path.GetFullPath(executable);
            foreach(var entry in paths)if(full.StartsWith(entry.Prefix,StringComparison.OrdinalIgnoreCase))return entry.Game;
            return null;
        }
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
        catalog = new(result);
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
        try
        {
            var name = new StringBuilder(512); int length = name.Capacity;
            if (QueryFullProcessImageName(handle, 0, name, ref length)) return name.ToString();
            if (Marshal.GetLastWin32Error() != 122) return null;
            name.EnsureCapacity(32768); length = name.Capacity;
            return QueryFullProcessImageName(handle, 0, name, ref length) ? name.ToString() : null;
        }
        finally { CloseHandle(handle); }
    }
    [DllImport("kernel32.dll", SetLastError = true)] private static extern nint OpenProcess(uint access, [MarshalAs(UnmanagedType.Bool)] bool inherit, uint processId);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool QueryFullProcessImageName(nint process, uint flags, StringBuilder name, ref int length);
    [DllImport("kernel32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool CloseHandle(nint handle);
    private delegate bool EnumWindow(nint window, nint parameter);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool EnumWindows(EnumWindow callback, nint parameter);
    [DllImport("user32.dll")] private static extern nint GetWindow(nint window, uint command);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool IsWindowVisible(nint window);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(nint window, out uint pid);
}
