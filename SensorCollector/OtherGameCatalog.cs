using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Xml;
using System.Xml.Linq;
using Microsoft.Win32;

namespace SensorCollector;

internal static class OtherGameCatalog
{
    internal static void Load(List<InstalledGame> games, List<string> notes)
    {
        Read("Epic", () => LoadEpic(games, Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "Epic", "EpicGamesLauncher", "Data", "Manifests")));
        Read("EA and GOG", () => LoadRegistryLibraries(games));
        Read("Xbox", () => LoadXbox(games));
        Read("Windows game registrations", () => LoadWindows(games));
        games.AddRange(ManualGameRegistration.List());

        void Read(string source, Action load)
        {
            try { load(); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or System.Security.SecurityException)
            { notes.Add($"{source} catalog partially unavailable."); }
        }
    }

    private static void LoadEpic(List<InstalledGame> games, string manifests)
    {
        if (!Directory.Exists(manifests)) return;
        foreach (var path in Directory.EnumerateFiles(manifests, "*.item"))
        {
            try
            {
                if (new FileInfo(path).Length > 1024 * 1024) continue;
                using var document = JsonDocument.Parse(File.ReadAllBytes(path));
                var game = ParseEpic(document.RootElement);
                if (game is not null) games.Add(game);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or ArgumentException) { }
        }
    }

    internal static InstalledGame? ParseEpic(JsonElement body)
    {
        if (body.ValueKind != JsonValueKind.Object ||
            body.TryGetProperty("bIsIncompleteInstall", out var incomplete) && incomplete.ValueKind != JsonValueKind.False ||
            !body.TryGetProperty("AppCategories", out var categories) || categories.ValueKind != JsonValueKind.Array ||
            !categories.EnumerateArray().Any(value => value.ValueKind == JsonValueKind.String && value.ValueEquals("games"))) return null;

        string? location = Text("InstallLocation"), launch = Text("LaunchExecutable"), title = Text("DisplayName");
        if (string.IsNullOrWhiteSpace(launch) || !LibraryEntry(title, location, "Epic Games", out var game)) return null;
        var executable = Path.GetFullPath(Path.Combine(game.Directory, launch));
        return GameDiscovery.Inside(executable, game.Directory) && File.Exists(executable) ? game : null;

        string? Text(string property) => body.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
    }

    private static void LoadRegistryLibraries(List<InstalledGame> games)
    {
        foreach (var view in new[] { RegistryView.Registry64, RegistryView.Registry32 })
        {
            using var machine = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, view);
            Load(@"SOFTWARE\EA Games", "EA", "DisplayName", "Install Dir");
            Load(@"SOFTWARE\GOG.com\Games", "GOG", "gameName", "path");

            void Load(string path, string source, string titleKey, string directoryKey)
            {
                using var root = machine.OpenSubKey(path);
                if (root is null) return;
                foreach (string id in root.GetSubKeyNames())
                {
                    using var game = root.OpenSubKey(id);
                    if (LibraryEntry(game?.GetValue(titleKey) as string, game?.GetValue(directoryKey) as string, source, out var entry) &&
                        !games.Any(other => other.Directory.Equals(entry.Directory, StringComparison.OrdinalIgnoreCase))) games.Add(entry);
                }
            }
        }
    }

    private static void LoadXbox(List<InstalledGame> games)
    {
        foreach (var drive in DriveInfo.GetDrives().Where(drive => drive.DriveType == DriveType.Fixed))
        {
            try
            {
                string driveRoot = drive.RootDirectory.FullName;
                string marker = Path.Combine(driveRoot, ".GamingRoot");
                if (!File.Exists(marker) || new FileInfo(marker).Length > 4096) continue;
                var bytes = File.ReadAllBytes(marker);
                if (bytes.Length < 12 || (bytes.Length & 1) != 0 || !bytes.AsSpan(0, 8).SequenceEqual(new byte[] { 82, 71, 66, 88, 1, 0, 0, 0 })) continue;
                var root = Path.GetFullPath(Path.Combine(driveRoot, Encoding.Unicode.GetString(bytes, 8, bytes.Length - 8).TrimEnd('\0')));
                if (!GameDiscovery.Inside(root, driveRoot) || !Directory.Exists(root)) continue;
                foreach (string folder in Directory.EnumerateDirectories(root))
                {
                    try
                    {
                        var content = Path.Combine(folder, "Content");
                        using var stream = File.OpenRead(Path.Combine(content, "MicrosoftGame.config"));
                        using var reader = XmlReader.Create(stream, new XmlReaderSettings { MaxCharactersInDocument = 1024 * 1024 });
                        var body = XDocument.Load(reader).Root;
                        if (body?.Name.LocalName != "Game" || body.Element("Identity")?.Attribute("Name") is null ||
                            body.Element("ExecutableList")?.Element("Executable")?.Attribute("Name") is null) continue;
                        games.Add(new(Path.GetFileName(folder), content, "Xbox game installation"));
                    }
                    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or XmlException) { }
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException) { }
        }
    }

    private static void LoadWindows(List<InstalledGame> games)
    {
        using var root = Registry.CurrentUser.OpenSubKey(@"System\GameConfigStore\Children");
        if (root is null) return;
        var existing = new GameDiscovery.Catalog(games);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (string id in root.GetSubKeyNames())
        {
            using var entry = root.OpenSubKey(id);
            if (entry?.GetValue("Type") is not 1 || !uint.TryParse(entry.GetValue("TitleId") as string, out uint titleId) || titleId == 0) continue;
            var game = ExactEntry(entry.GetValue("MatchedExeFullPath") as string, "Windows registered game");
            if (game is not null && seen.Add(game.Executable!) && existing.Match(game.Executable!) is null) games.Add(game);
        }
    }

    private static bool LibraryEntry(string? title, string? location, string source, out InstalledGame game)
    {
        game = null!;
        if (string.IsNullOrWhiteSpace(title) || location is null || !Path.IsPathFullyQualified(location)) return false;
        string directory = Path.TrimEndingDirectorySeparator(Path.GetFullPath(location));
        if (string.Equals(directory, Path.GetPathRoot(directory), StringComparison.OrdinalIgnoreCase) || !Directory.Exists(directory)) return false;
        game = new(title, directory, source);
        return true;
    }

    internal static InstalledGame? ExactEntry(string? path, string source)
    {
        if (path is null || !Path.IsPathFullyQualified(path) || !Path.GetExtension(path).Equals(".exe", StringComparison.OrdinalIgnoreCase) || !File.Exists(path)) return null;
        string executable = Path.GetFullPath(path);
        string name = Path.GetFileNameWithoutExtension(executable);
        try { name = FileVersionInfo.GetVersionInfo(executable).FileDescription is { Length: > 0 } description ? description : name; }
        catch (Exception ex) when (ex is IOException or System.ComponentModel.Win32Exception) { }
        return new(name, Path.GetDirectoryName(executable)!, source, executable);
    }
}
