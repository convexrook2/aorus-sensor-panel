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
    private readonly DispatcherTimer updateTimer;
    private readonly CancellationTokenSource updateLifetime = new();
    private PanelRelease? availableUpdate;
    internal bool Visible => icon.Visible;
    internal bool IsCheckingForUpdates { get; private set; }
    internal Version? AvailableVersion => availableUpdate?.Version;
    internal string UpdateStatus { get; private set; } = "";
    internal event Action? UpdateStatusChanged;
    public PanelTray(PanelWindow window)
    {
        artwork = PanelBranding.TrayIcon();
        void Restore() => window.ShowPanel();
        menu.Items.Add("Show panel", null, (_, _) => window.Dispatcher.Invoke(Restore));
        menu.Items.Add("Open settings", null, (_, _) => window.Dispatcher.Invoke(window.ShowSettings));
        menu.Items.Add("Fit to display", null, (_, _) => window.Dispatcher.Invoke(window.FitToDisplay));
        menu.Items.Add("Hide to tray", null, (_, _) => window.Dispatcher.Invoke(window.HidePanel));
        menu.Items.Add("Open README", null, (_, _) => window.Dispatcher.Invoke(PanelBranding.ShowReadme));
        menu.Items.Add(new System.Windows.Forms.ToolStripSeparator());
        menu.Items.Add("Exit", null, (_, _) => window.Dispatcher.Invoke(window.Close));
        icon = new System.Windows.Forms.NotifyIcon { Text = PanelBranding.Name, Icon = artwork, ContextMenuStrip = menu, Visible = true };
        icon.DoubleClick += (_, _) => window.Dispatcher.Invoke(Restore);
        icon.BalloonTipClicked += (_, _) => window.Dispatcher.Invoke(window.ShowUpdateSettings);
        updateTimer = new DispatcherTimer(TimeSpan.FromHours(6), DispatcherPriority.Background,
            async (_, _) => await CheckForUpdatesAsync(notifyAvailable: true), window.Dispatcher);
        updateTimer.Start();
        _ = CheckForUpdatesAsync(notifyAvailable: true);
    }

    internal async Task CheckForUpdatesAsync(bool notifyAvailable = false)
    {
        if (IsCheckingForUpdates) return;
        IsCheckingForUpdates = true;
        SetUpdateStatus("Checking for updates…");
        var cancellation = updateLifetime.Token;
        var latest = await PanelUpdateService.CheckAsync(cancellation);
        if (cancellation.IsCancellationRequested) return;
        IsCheckingForUpdates = false;
        if (latest is null)
        {
            SetUpdateStatus("Couldn’t check GitHub Releases. Try again later.");
            return;
        }

        if (latest.Version > PanelUpdateService.CurrentVersion)
        {
            bool newAlert = latest.Version != availableUpdate?.Version;
            availableUpdate = latest;
            SetUpdateStatus($"Version {PanelUpdateService.Label(latest.Version)} is available.");
            if (notifyAvailable && newAlert)
                icon.ShowBalloonTip(8000, "Sensor Panel update available",
                    "Open Settings → App to review the update.", System.Windows.Forms.ToolTipIcon.Info);
        }
        else
        {
            availableUpdate = null;
            SetUpdateStatus("You’re up to date.");
        }
    }

    private void SetUpdateStatus(string status)
    {
        UpdateStatus = status;
        UpdateStatusChanged?.Invoke();
    }

    internal void OpenUpdate()
    {
        if (availableUpdate is not { } release) return;
        try { using var browser = Process.Start(new ProcessStartInfo(release.Page.AbsoluteUri) { UseShellExecute = true }); }
        catch (System.ComponentModel.Win32Exception)
        {
            SetUpdateStatus("Couldn’t open the release page.");
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
