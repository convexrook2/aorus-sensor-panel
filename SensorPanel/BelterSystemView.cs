using System.ComponentModel;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;

namespace SensorPanel;

/// <summary>One local Belter Charts scene, over the existing sensor presentation.</summary>
internal sealed class SolarSystemView : Grid, IDisposable
{
    private const string Origin = "https://x890-navigation.example/";
    private readonly TextBlock loading = new() { Text = "LOADING LOCAL BELTER CHARTS…", Foreground = Brushes.LightBlue,
        FontSize = 11, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
    private WebView2CompositionControl? browser;
    private LegacySolarSystemView? fallback;
    private Task? initialization;
    private readonly TaskCompletionSource ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private bool motion = true, disposed, failed, loaded;
    private string selected = "Sun";
    private Window? owner;
    public event Action<string>? BodySelected;
    public string SelectedBodyId => selected;
    public bool MotionActive => !disposed && !failed && ready.Task.IsCompletedSuccessfully && motion && loaded && IsVisible && IsEnabled && SystemParameters.ClientAreaAnimation;
    internal bool BrowserReady => ready.Task.IsCompletedSuccessfully && !failed && !disposed;

    public SolarSystemView()
    {
        ClipToBounds = true;
        Background = new SolidColorBrush(Color.FromRgb(3, 11, 20));
        Children.Add(loading);
        Loaded += OnLoaded;
        Unloaded += (_, _) => { loaded = false; PublishActivity(); };
        IsVisibleChanged += (_, _) => PublishActivity();
        IsEnabledChanged += (_, _) => PublishActivity();
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        loaded = true;
        if (owner is null)
        {
            owner = Window.GetWindow(this);
            if (owner is not null) owner.Closed += OwnerClosed;
            SystemParameters.StaticPropertyChanged += AnimationPreferenceChanged;
        }
        initialization ??= InitializeAsync();
        PublishActivity();
    }
    private void OwnerClosed(object? sender, EventArgs e) => Dispose();
    private void AnimationPreferenceChanged(object? sender, PropertyChangedEventArgs e)
    { if (e.PropertyName == nameof(SystemParameters.ClientAreaAnimation)) PublishActivity(); }

    private async Task InitializeAsync()
    {
        try
        {
            string assets = Path.Combine(AppContext.BaseDirectory, "SolarNavigation");
            if (!File.Exists(Path.Combine(assets, "index.html"))) throw new FileNotFoundException("The local Belter Charts asset bundle is missing.");
            string profile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "X890Sensor", "NavigationWebView2");
            var environment = await CoreWebView2Environment.CreateAsync(userDataFolder: profile);
            if (disposed) return;
            browser = new WebView2CompositionControl { DefaultBackgroundColor = System.Drawing.Color.FromArgb(255, 3, 11, 20) };
            Children.Insert(0, browser);
            await browser.EnsureCoreWebView2Async(environment);
            if (disposed) return;
            var core = browser.CoreWebView2;
            core.SetVirtualHostNameToFolderMapping("x890-navigation.example", assets, CoreWebView2HostResourceAccessKind.Deny);
            core.Settings.AreDefaultContextMenusEnabled = false;
            core.Settings.IsStatusBarEnabled = false;
            core.Settings.AreBrowserAcceleratorKeysEnabled = false;
            core.Settings.IsZoomControlEnabled = false;
            core.IsMuted = true;
            core.NavigationStarting += (_, e) =>
            {
                if (!Uri.TryCreate(e.Uri, UriKind.Absolute, out var uri) || uri.Scheme != "https" || uri.Host != "x890-navigation.example") e.Cancel = true;
            };
            core.NewWindowRequested += (_, e) => e.Handled = true;
            core.PermissionRequested += (_, e) => e.State = CoreWebView2PermissionState.Deny;
            core.DownloadStarting += (_, e) => e.Cancel = true;
            core.WebMessageReceived += MessageReceived;
            core.ProcessFailed += (_, e) => Fail(new InvalidOperationException("The local 3D renderer stopped: " + e.ProcessFailedKind));
            core.NavigationCompleted += (_, e) => { if (!e.IsSuccess) Fail(new InvalidOperationException("The local chart failed to load: " + e.WebErrorStatus)); };
            core.Navigate(Origin + "index.html?focus=" + Uri.EscapeDataString(selected));
        }
        catch (Exception ex) { Fail(ex); }
    }

    private void MessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        if (disposed || !e.Source.StartsWith(Origin, StringComparison.Ordinal)) return;
        try
        {
            using var message = JsonDocument.Parse(e.WebMessageAsJson);
            var root = message.RootElement;
            string? type = root.GetProperty("type").GetString();
            if (type == "ready")
            {
                ready.TrySetResult(); loading.Visibility = Visibility.Collapsed;
                Send(new { type = "focus", id = selected }); PublishActivity();
            }
            else if (type == "selected" && root.TryGetProperty("id", out var id) && id.GetString() is string value && value.Length <= 32)
            { selected = value; BodySelected?.Invoke(selected); }
            else if (type == "error") Fail(new InvalidOperationException(root.GetProperty("message").GetString()));
        }
        catch (JsonException ex) { Fail(ex); }
    }

    private void Fail(Exception error)
    {
        if (disposed || failed) return;
        failed = true;
        ready.TrySetException(error);
        try { File.WriteAllText(Path.Combine(PanelPaths.Artifacts, "navigation-web-error.txt"), error.ToString()); } catch (IOException) { }
        browser?.Dispose(); browser = null;
        Children.Clear();
        // Preserve the pre-existing native navigation if a runtime is absent;
        // the visible status makes clear that the requested web scene failed.
        fallback = new LegacySolarSystemView(); fallback.FocusBody(selected, false); fallback.SetMotion(false);
        Children.Add(fallback);
        loading.Text = "BELTER CHARTS UNAVAILABLE · " + error.Message;
        loading.FontSize = 9; loading.VerticalAlignment = VerticalAlignment.Bottom;
        loading.Background = new SolidColorBrush(Color.FromRgb(30, 14, 10));
        loading.TextWrapping = TextWrapping.Wrap; loading.Visibility = Visibility.Visible; Children.Add(loading);
    }
    private void Send(object message)
    {
        if (!BrowserReady || browser?.CoreWebView2 is not { } core) return;
        try { core.PostWebMessageAsJson(JsonSerializer.Serialize(message)); }
        catch (InvalidOperationException ex) { Fail(ex); }
    }
    private void PublishActivity()
    {
        if (disposed) return;
        Send(new { type = "motion", enabled = motion && SystemParameters.ClientAreaAnimation });
        Send(new { type = "active", enabled = loaded && IsVisible && IsEnabled });
    }
    public void FocusBody(string id, bool animate = true)
    {
        selected = id;
        fallback?.FocusBody(id, false);
        Send(new { type = "focus", id });
    }
    public void SetMotion(bool enabled) { motion = enabled; PublishActivity(); }
    internal void Check()
    {
        if (failed) throw new InvalidOperationException("The Belter Charts renderer is unavailable; inspect navigation-web-error.txt.");
        if (!File.Exists(Path.Combine(AppContext.BaseDirectory, "SolarNavigation", "index.html"))) throw new InvalidOperationException("Local navigation assets are missing.");
    }
    internal async Task WaitForReadyAsync() => await ready.Task.WaitAsync(TimeSpan.FromSeconds(30));
    internal async Task<JsonElement> DiagnoseAsync()
    {
        await WaitForReadyAsync();
        string json = await browser!.CoreWebView2.ExecuteScriptAsync("window.x890Navigation.diagnose()");
        return JsonDocument.Parse(json).RootElement.Clone();
    }
    internal async Task<JsonElement> CheckBrowserAsync()
    {
        await WaitForReadyAsync();
        string saved = selected;
        string json = await browser!.CoreWebView2.ExecuteScriptAsync("window.x890Navigation.check()");
        Send(new { type = "focus", id = saved });
        if (json == "null") throw new InvalidOperationException("The web navigation self-check did not return a result.");
        return JsonDocument.Parse(json).RootElement.Clone();
    }
    internal async Task CaptureBrowserAsync(string path)
    {
        await WaitForReadyAsync();
        using var stream = File.Create(path);
        await browser!.CoreWebView2.CapturePreviewAsync(CoreWebView2CapturePreviewImageFormat.Png, stream);
    }
    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        if (owner is not null) owner.Closed -= OwnerClosed;
        SystemParameters.StaticPropertyChanged -= AnimationPreferenceChanged;
        browser?.Dispose(); browser = null; fallback?.SetMotion(false);
    }
}
