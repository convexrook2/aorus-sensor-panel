using System.IO;
using System.Diagnostics;
using System.Security.Principal;
using System.Windows;

namespace SensorPanel;

internal static class App
{
    [STAThread]
    private static int Main(string[] args)
    {
        try
        {
            // WPF hosts the window; WinForms supplies only the tray. Configure
            // process DPI before either framework creates an HWND.
            System.Windows.Forms.Application.SetHighDpiMode(System.Windows.Forms.HighDpiMode.PerMonitorV2);
            if (args.Contains("--check-mapping")) { CheckArtworkPackage(); return PanelMetrics.Check(); }
            if (args.Contains("--check-displays"))
            {
                File.WriteAllText(Path.Combine(PanelPaths.Artifacts,"display-check.json"),System.Text.Json.JsonSerializer.Serialize(Displays.List()));
                return 0;
            }
            string? preview = Argument(args, "--preview");
            string? capture = Argument(args, "--capture");
            int themeIndex = Array.IndexOf(args, "--preview-theme");
            string? previewTheme = null;
            if (themeIndex >= 0)
            {
                if (preview is null || themeIndex + 1 >= args.Length || !PanelTheme.All.Any(t => t.Id == args[themeIndex + 1]))
                    throw new ArgumentException("--preview-theme requires --preview and a supported theme ID: " + string.Join(", ", PanelTheme.All.Select(t => t.Id)));
                previewTheme = args[themeIndex + 1];
            }
            bool checkThemes = args.Contains("--preview-check-themes");
            bool checkShips = args.Contains("--preview-check-ships");
            if ((checkThemes || checkShips) && (preview is null || !args.Contains("--sensor-preview")))
                throw new ArgumentException("--preview-check-themes requires a saved preview and the secondary sensor display.");
            if (preview is null && !args.Contains("--readme") && !args.Contains("--remove-install-startup") && !IsElevated())
            {
                var launch = new ProcessStartInfo(Environment.ProcessPath ?? throw new InvalidOperationException("Cannot resolve the app executable.")) { UseShellExecute = true, Verb = "runas" };
                foreach (string argument in args) launch.ArgumentList.Add(argument);
                using var elevated = Process.Start(launch);
                return 0;
            }
            if (args.Contains("--enable-startup")) { ScheduledStartup.SetEnabled(true); return 0; }
            if (args.Contains("--disable-startup")) { ScheduledStartup.SetEnabled(false); return 0; }
            if (args.Contains("--remove-install-startup")) { ScheduledStartup.RemoveForInstallation(); return 0; }
            using var instance = new Mutex(false, "Local\\AORUS-FAN-MADE-SensorPanel-1B7C308F");
            bool ownsInstance = preview is not null || args.Contains("--readme");
            if (!ownsInstance)
            {
                try { ownsInstance = instance.WaitOne(0); }
                catch (AbandonedMutexException) { ownsInstance = true; }
                if (!ownsInstance) { MessageBox.Show("The panel is already running. Double-click its FAN MADE tray icon to show it.", PanelBranding.Name); return 0; }
            }
            var application = new Application { ShutdownMode = ShutdownMode.OnMainWindowClose };
            if (args.Contains("--readme")) { PanelBranding.ShowReadme(); return application.Run(); }
            application.DispatcherUnhandledException += (_, e) =>
            {
                File.WriteAllText(Path.Combine(PanelPaths.Artifacts, "panel-error.txt"), e.Exception.ToString());
                MessageBox.Show(e.Exception.Message, PanelBranding.Name);
                e.Handled = true;
                application.Shutdown(1);
            };
            int fpsPidIndex = Array.IndexOf(args, "--fps-pid");
            int fpsPid = fpsPidIndex >= 0 ? int.Parse(args[fpsPidIndex + 1]) : 0;
            if (fpsPid < 0) throw new ArgumentException("FPS PID must be nonnegative.");
            Size? previewSize=null;
            int sizeIndex=Array.IndexOf(args,"--preview-size");
            if (sizeIndex>=0)
            {
                if (preview is null || sizeIndex+1>=args.Length) throw new ArgumentException("--preview-size requires --preview and WIDTHxHEIGHT.");
                var dimensions=args[sizeIndex+1].Split('x');
                if (dimensions.Length!=2 || !int.TryParse(dimensions[0],out int width) || !int.TryParse(dimensions[1],out int height) || width<1 || height<1 || width>8192 || height>8192)
                    throw new ArgumentException("Preview dimensions must be 1–8192 pixels.");
                previewSize=new Size(width,height);
            }
            return application.Run(new PanelWindow(preview, capture, fpsPid, args.Contains("--benchmark-polling"),previewSize,args.Contains("--sensor-preview"),previewTheme,checkThemes,checkShips));
        }
        catch (Exception ex)
        {
            File.WriteAllText(Path.Combine(PanelPaths.Artifacts, "panel-error.txt"), ex.ToString());
            MessageBox.Show(ex.Message, PanelBranding.Name);
            return 1;
        }
    }

    private static bool IsElevated()
    {
        using var identity = WindowsIdentity.GetCurrent();
        return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
    }

    private static string? Argument(string[] args, string key)
    {
        int index = Array.IndexOf(args, key);
        if (index < 0) return null;
        if (index + 1 >= args.Length) throw new ArgumentException($"{key} requires a path.");
        return Path.GetFullPath(args[index + 1]);
    }

    private static void CheckArtworkPackage()
    {
        using var stream = typeof(App).Assembly.GetManifestResourceStream("SensorPanel.g.resources")
            ?? throw new InvalidOperationException("The panel artwork resource package is missing.");
        using var reader = new System.Resources.ResourceReader(stream);
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var entries = reader.GetEnumerator();
        while (entries.MoveNext()) names.Add(Uri.UnescapeDataString((string)entries.Key));
        if (!names.Contains("settingsstyles.baml"))
            throw new InvalidOperationException("The themed settings styles are missing from the panel resource package.");
        foreach (string folder in new[] { "l", "r" })
            for (int frame = 0; frame < 175; frame++)
                if (!names.Contains($"aorus/animations/{folder}/comp {frame}.png"))
                    throw new InvalidOperationException($"The built panel is missing {folder} animation frame {frame}; rebuild the resource package.");
        for (int frame = 0; frame < 4; frame++)
            if (!names.Contains($"aorus/animations/x/x{frame}.png"))
                throw new InvalidOperationException($"The built panel is missing brand frame {frame}.");
    }
}

internal static class PanelPaths
{
    public static string Artifacts { get; } = FindArtifacts();
    private static string FindArtifacts()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (!Directory.Exists(Path.Combine(directory.FullName, "Assets", "AORUS"))) continue;
            string result = Path.Combine(directory.FullName, "artifacts");
            Directory.CreateDirectory(result);
            return result;
        }
        string fallback = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "X890Sensor", "artifacts");
        Directory.CreateDirectory(fallback);
        return fallback;
    }
}
