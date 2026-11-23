using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace SensorPanel;

internal static class PanelBranding
{
    public const string Name = "AORUS Sensor Panel — FAN MADE";
    private static readonly Uri IconUri = new("pack://application:,,,/Branding/AorusFanMade.ico");
    public static ImageSource WindowIcon => BitmapFrame.Create(IconUri);
    public static System.Drawing.Icon TrayIcon()
    {
        using var stream = Application.GetResourceStream(IconUri)?.Stream ?? throw new FileNotFoundException("Missing FAN MADE icon.");
        using var icon = new System.Drawing.Icon(stream, 32, 32);
        return (System.Drawing.Icon)icon.Clone();
    }
    public static void ShowReadme()
    {
        string file = Path.Combine(AppContext.BaseDirectory, "README.txt");
        var window = new Window
        {
            Title = Name + " — README", Icon = WindowIcon, Width = 820, Height = 680,
            WindowStartupLocation = WindowStartupLocation.CenterScreen,
            Content = new TextBox { Text = File.Exists(file) ? File.ReadAllText(file) : "The installed README.txt is missing.",
                IsReadOnly = true, TextWrapping = TextWrapping.Wrap, VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                FontFamily = new FontFamily("Segoe UI"), FontSize = 15, Padding = new Thickness(24), BorderThickness = new Thickness(0) }
        };
        window.Show();
    }
}

internal sealed class PanelTray : IDisposable
{
    private readonly System.Windows.Forms.NotifyIcon icon;
    private readonly System.Drawing.Icon artwork;
    private readonly System.Windows.Forms.ContextMenuStrip menu = new();
    private readonly System.Windows.Forms.ToolStripMenuItem checkForUpdates;
    private readonly System.Windows.Forms.ToolStripMenuItem downloadUpdate;
    private readonly DispatcherTimer updateTimer;
    private readonly CancellationTokenSource updateLifetime = new();
    private PanelRelease? availableUpdate;
    internal bool Visible => icon.Visible;
    public PanelTray(PanelWindow window)
    {
        artwork = PanelBranding.TrayIcon();
        void Restore() => window.ShowPanel();
        menu.Items.Add("Show panel", null, (_, _) => window.Dispatcher.Invoke(Restore));
        menu.Items.Add("Open settings", null, (_, _) => window.Dispatcher.Invoke(window.ShowSettings));
        menu.Items.Add("Fit to display", null, (_, _) => window.Dispatcher.Invoke(window.FitToDisplay));
        menu.Items.Add("Hide to tray", null, (_, _) => window.Dispatcher.Invoke(window.HidePanel));
        menu.Items.Add("Open README", null, (_, _) => window.Dispatcher.Invoke(PanelBranding.ShowReadme));
        checkForUpdates = new("Check for updates", null, async (_, _) => await CheckForUpdatesAsync(true));
        downloadUpdate = new("Download update", null, (_, _) => OpenUpdate()) { Visible = false };
        menu.Items.Add(checkForUpdates);
        menu.Items.Add(downloadUpdate);
        menu.Items.Add(new System.Windows.Forms.ToolStripSeparator());
        menu.Items.Add("Exit", null, (_, _) => window.Dispatcher.Invoke(window.Close));
        icon = new System.Windows.Forms.NotifyIcon { Text = PanelBranding.Name, Icon = artwork, ContextMenuStrip = menu, Visible = true };
        icon.DoubleClick += (_, _) => window.Dispatcher.Invoke(Restore);
        icon.BalloonTipClicked += (_, _) => OpenUpdate();
        updateTimer = new DispatcherTimer(TimeSpan.FromHours(6), DispatcherPriority.Background,
            async (_, _) => await CheckForUpdatesAsync(false), window.Dispatcher);
        updateTimer.Start();
        _ = CheckForUpdatesAsync(false);
    }

    private async Task CheckForUpdatesAsync(bool manual)
    {
        if (!checkForUpdates.Enabled) return;
        checkForUpdates.Enabled = false;
        var cancellation = updateLifetime.Token;
        var latest = await PanelUpdateService.CheckAsync(cancellation);
        if (cancellation.IsCancellationRequested) return;
        checkForUpdates.Enabled = true;
        if (latest is null)
        {
            if (manual) Notify("Update check unavailable", "Sensor Panel couldn't check GitHub Releases. Try again later.");
            return;
        }

        if (latest.Version > PanelUpdateService.CurrentVersion)
        {
            bool newAlert = latest.Version != availableUpdate?.Version;
            availableUpdate = latest;
            downloadUpdate.Text = $"Download v{PanelUpdateService.Label(latest.Version)}";
            downloadUpdate.Visible = true;
            if (newAlert || manual) Notify("Sensor Panel update available", $"Version {PanelUpdateService.Label(latest.Version)} is ready. Click to open the release page.");
        }
        else
        {
            availableUpdate = null;
            downloadUpdate.Visible = false;
            if (manual) Notify("Sensor Panel is up to date", $"You're running v{PanelUpdateService.Label(PanelUpdateService.CurrentVersion)}.");
        }
    }

    private void Notify(string title, string message) =>
        icon.ShowBalloonTip(8000, title, message, System.Windows.Forms.ToolTipIcon.Info);

    private void OpenUpdate()
    {
        if (availableUpdate is not { } release) return;
        try { using var browser = Process.Start(new ProcessStartInfo(release.Page.AbsoluteUri) { UseShellExecute = true }); }
        catch (System.ComponentModel.Win32Exception)
        {
            Notify("Couldn't open the release page", "Open the Sensor Panel project on GitHub to download the update.");
        }
    }

    public void Dispose()
    {
        updateTimer.Stop();
        updateLifetime.Cancel();
        updateLifetime.Dispose();
        icon.Visible = false;
        icon.Dispose();
        menu.Dispose();
        artwork.Dispose();
    }
}
