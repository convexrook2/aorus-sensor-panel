using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;

namespace FleetStudio.Desktop;

internal static class App
{
    [STAThread]
    private static int Main(string[] args)
    {
        try
        {
            string? reviewRoot = null;
            int reviewIndex = Array.IndexOf(args, "--review-root");
            bool protectedCheck = args.Contains("--check-protected-assets");
            if (reviewIndex >= 0)
            {
                if (protectedCheck || reviewIndex + 1 >= args.Length ||
                    args.Count(a => a == "--review-root") != 1 ||
                    !Path.IsPathFullyQualified(args[reviewIndex + 1]))
                    throw new ArgumentException("Use --review-root with one absolute viewer folder, separately from protected-asset checks.");
                reviewRoot = Path.GetFullPath(args[reviewIndex + 1]);
                foreach (string file in new[] { "index.html", "catalog.json", "engines.json" })
                    if (!File.Exists(Path.Combine(reviewRoot, file)))
                        throw new FileNotFoundException("Local review file is missing: " + file);
            }
            return new Application { ShutdownMode = ShutdownMode.OnMainWindowClose }
                .Run(new StudioWindow(protectedCheck, reviewRoot));
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Fleet Studio could not start", MessageBoxButton.OK, MessageBoxImage.Error);
            return 2;
        }
    }
}

internal sealed partial class StudioWindow : Window
{
    private const string Host = "fleet-studio.example";
    private readonly Grid surface = new();
    private readonly TextBlock status = new()
    {
        Text = "Opening local ship viewer…", TextWrapping = TextWrapping.Wrap,
        HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center,
        Foreground = System.Windows.Media.Brushes.White, Margin = new Thickness(24)
    };
    private WebView2? browser;
    private string address = "https://" + Host + "/index.html";
    private int generation;
    private bool closed;
    private readonly bool protectedCheck;
    private readonly string? reviewRoot;
    private bool checkStarted;

    internal StudioWindow(bool protectedCheck=false, string? reviewRoot=null)
    {
        this.protectedCheck=protectedCheck;
        this.reviewRoot=reviewRoot;
        Title = reviewRoot is null ? "X890 Fleet Studio · on demand" : "X890 Fleet Studio · local model review";
        Width = 1200; Height = 800; MinWidth = 800; MinHeight = 600;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Background = System.Windows.Media.Brushes.Black;
        surface.Children.Add(status); Content = surface;
        Loaded += async (_, _) => await OpenViewer();
        StateChanged += async (_, _) =>
        {
            if (WindowState == WindowState.Minimized) ReleaseViewer();
            else if (browser is null) await OpenViewer();
        };
        Closed += (_, _) => { closed = true; ReleaseViewer(); };
    }

    private async Task OpenViewer()
    {
        if (closed || browser is not null || WindowState == WindowState.Minimized) return;
        int version = ++generation;
        status.Text = "Opening local ship viewer…";
        try
        {
            if (reviewRoot is null && !Rookline.AssetProtection.AppAssets.Exists("FleetStudio/index.html")) throw new FileNotFoundException("Fleet Studio files are missing. Rebuild the desktop project.");
            string profile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "X890Sensor", "FleetStudioWebView2");
            var environment = await CoreWebView2Environment.CreateAsync(userDataFolder: profile);
            if (closed || version != generation) return;
            var view = new WebView2(); browser = view;
            surface.Children.Insert(0, view);
            await view.EnsureCoreWebView2Async(environment);
            if (closed || version != generation) return;
            var core = view.CoreWebView2;
            if (reviewRoot is null) Rookline.AssetProtection.AppAssets.Host(core,Host,"FleetStudio");
            else core.SetVirtualHostNameToFolderMapping(Host, reviewRoot, CoreWebView2HostResourceAccessKind.Deny);
            core.Settings.AreDefaultContextMenusEnabled = false;
            core.Settings.IsStatusBarEnabled = false;
            core.Settings.IsSwipeNavigationEnabled = false;
            core.IsMuted = true;
            core.NavigationStarting += (_, e) =>
            {
                if (!Uri.TryCreate(e.Uri, UriKind.Absolute, out var uri) || uri.Scheme != "https" || uri.Host != Host) e.Cancel = true;
            };
            core.NewWindowRequested += (_, e) =>
            {
                e.Handled = true;
                if (e.IsUserInitiated && Uri.TryCreate(e.Uri, UriKind.Absolute, out var uri) && uri.Scheme is "https" or "http")
                    Process.Start(new ProcessStartInfo(uri.AbsoluteUri) { UseShellExecute = true });
            };
            core.PermissionRequested += (_, e) => e.State = CoreWebView2PermissionState.Deny;
            core.DownloadStarting += (_, e) => e.Cancel = true;
            core.NavigationCompleted += (_, e) =>
            {
                status.Visibility = e.IsSuccess ? Visibility.Collapsed : Visibility.Visible;
                if (!e.IsSuccess) status.Text = "Viewer could not open: " + e.WebErrorStatus;
                if(protectedCheck&&!checkStarted){checkStarted=true;_=CheckProtectedAsync();}
            };
            core.ProcessFailed += (_, e) =>
            {
                status.Text = "Viewer stopped: " + e.ProcessFailedKind + ". Close and reopen Fleet Studio.";
                status.Visibility = Visibility.Visible;
            };
            core.Navigate(address);
        }
        catch (Exception ex)
        {
            if (version != generation || closed) return;
            ReleaseViewer(); status.Text = "Fleet Studio could not start: " + ex.Message;
        }
    }

    private void ReleaseViewer()
    {
        ++generation;
        if (browser is { } view)
        {
            if (view.CoreWebView2 is { } core) address = core.Source;
            surface.Children.Remove(view); view.Dispose(); browser = null;
        }
        status.Text = "Viewer paused while minimized. Restore to reopen the selected ship.";
        status.Visibility = Visibility.Visible;
    }
}
