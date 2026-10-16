using System.Globalization;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using System.Windows.Interop;
using SensorCollector;

namespace SensorPanel;

internal sealed class PanelWindow : Window
{
    private readonly Canvas board = new() { Width = 1024, Height = 576, Background = new SolidColorBrush(Color.FromRgb(228, 228, 228)) };
    private readonly Viewbox viewport = new();
    private PanelLayout? panelLayout;
    private ShipPanel? shipPanel;
    private bool animateArtwork = true;
    private bool holographicMotion = PanelPreferences.HolographicMotion;
    private bool readoutPulse = PanelPreferences.ReadoutPulse;
    private bool gaugeTransitions = PanelPreferences.GaugeTransitions;
    private MenuItem? animationCommand;
    private PanelSettings? settings;
    private string layoutMode = PanelPreferences.Layout;
    private PanelTheme theme = PanelTheme.Resolve(PanelPreferences.Theme);
    private readonly Dictionary<TextBlock, bool> mutedLabels = new();
    private readonly List<Border> dividers = new();
    private readonly List<(Image Image, string Path, ImageSource Original)> themeImages = new();
    private readonly Dictionary<string, TextBlock> text = new();
    private readonly Dictionary<string, double> maxima = new();
    private readonly TemperatureGauge cpuGauge = new(), gpuGauge = new();
    private readonly HistoryGraph cpuGraph = new() { Width = 183, Height = 55 }, gpuGraph = new() { Width = 118, Height = 35 };
    private readonly UsageBar cpuBar = new(), gpuBar = new(), memoryBar = new();
    private readonly CancellationTokenSource cancellation = new();
    private readonly AutoResetEvent pollingChanged = new(false);
    private int pollingIntervalMs = PanelPreferences.PollingInterval;
    private int applyPending;
    private long collectionCycles, collectionTicks;
    private readonly bool benchmarkPolling;
    private PollingBenchmark? benchmark;
    private PendingUpdate? pendingUpdate;
    private sealed record PendingUpdate(Snapshot Snapshot, RuntimeStats Statistics);
    private readonly DispatcherTimer clock;
    private readonly DispatcherTimer displayRefresh;
    private DisplayRecovery? displayRecovery;
    private ArtworkAnimation? artworkAnimation;
    private PanelTray? tray;
    private readonly string? previewPath, capturePath;
    private readonly Size? previewSize;
    private readonly bool sensorPreview;
    private Thread? worker;
    private Snapshot? latest;
    private DateTimeOffset lastReceived;
    private DisplayInfo? display;
    private bool fit, closing, captured, stale;
    private bool hideInitially;
    private int samples;
    private int selectedFpsPid;
    private readonly GameDiscovery fpsGames = new();
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = true };

    public PanelWindow(string? previewPath, string? capturePath, int fpsPid = 0, bool benchmarkPolling = false, Size? previewSize = null, bool sensorPreview = false, string? previewTheme = null, bool checkThemes = false, bool checkShips = false)
    {
        this.previewSize=previewSize; this.sensorPreview=sensorPreview;
        this.benchmarkPolling = benchmarkPolling;
        selectedFpsPid = fpsPid;
        this.previewPath = previewPath; this.capturePath = capturePath;
        if (previewTheme is not null) theme = PanelTheme.Resolve(previewTheme);
        fit = previewPath is null;
        Title = PanelBranding.Name;
        Icon = PanelBranding.WindowIcon;
        Width = 1024; Height = 576; MinWidth = 640; MinHeight = 360;
        WindowStyle = WindowStyle.None; ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.Manual;
        Background = Brushes.Black; Topmost = false; ShowActivated = false; ShowInTaskbar = false;
        viewport.Stretch = ScalingStretch(PanelPreferences.Scaling); viewport.Child = board;
        var surface = new Grid(); surface.Children.Add(viewport); Content = surface;
        BuildLayout();
        foreach (string key in new[] { "cpuTemperature", "cpuTemperatureMax", "gpuTemperature", "gpuTemperatureMax" }) text[key] = new TextBlock { Text = "—" };
        shipPanel = new(text);
        shipPanel.ShipSelectionRequested += ship =>
        {
            theme=PanelTheme.All.Single(choice=>choice.ShipId==ship);
            if (previewPath is null) PanelPreferences.SaveTheme(theme.Id);
            ApplyTheme(); SaveDisplayState();
        };
        shipPanel.NavigationChanged += () => { if (IsLoaded) SaveDisplayState(); };
        ApplyTheme();
        SizeChanged += (_, _) => UpdatePanelLayout();
        DpiChanged += (_, _) => { if (fit && display is not null) Dispatcher.BeginInvoke(() => Displays.Place(this, display, true)); };
        viewport.MouseLeftButtonDown += (_, e) => { if (e.ClickCount == 2) ToggleFit(); else if (!fit) DragMove(); };
        KeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape && shipPanel?.HolotableOpen == true) { shipPanel.CloseHolotable(); e.Handled = true; }
            else if (e.Key == Key.Escape && settings?.IsOpen == true) { settings.Close(); e.Handled = true; }
            else if (e.Key == Key.F11) ToggleFit(); else if (e.Key == Key.Escape && fit) ToggleFit();
        };
        ContextMenu = BuildMenu();
        settings = new PanelSettings(this, viewport, ContextMenu, () => theme);
        surface.Children.Add(settings.Overlay);
        ContextMenuOpening += (_, e) => { e.Handled = true; settings.Open(); };
        PreviewMouseDown += (_, e) =>
        {
            if (settings.IsOpen && !settings.IsPointerOver) { settings.Close(); e.Handled = true; }
        };
        Deactivated += (_, _) => { if (settings.IsOpen) settings.Close(); };
        clock = new DispatcherTimer(TimeSpan.FromSeconds(1), DispatcherPriority.Background, (_, _) => Tick(), Dispatcher);
        displayRefresh = new DispatcherTimer(DispatcherPriority.ContextIdle) { Interval = TimeSpan.FromMilliseconds(300) };
        displayRefresh.Tick += (_, _) => { displayRefresh.Stop(); RefreshConnectedDisplay(); };
        SourceInitialized += (_, _) =>
        {
            PlaceInitialDisplay();
            HwndSource.FromHwnd(new WindowInteropHelper(this).Handle)?.AddHook(DisplayMessage);
        };
        Loaded += (_, _) =>
        {
            // WPF can apply its initial size after SourceInitialized placement.
            // Reapply native monitor bounds after that initial layout completes.
            if (display is not null) Displays.Place(this, display, fit,previewSize);
            UpdatePanelLayout();
            tray = new PanelTray(this); Start();
            Dispatcher.BeginInvoke(DispatcherPriority.ContextIdle,new Action(SaveDisplayState));
        };
        if (checkThemes || checkShips) ContentRendered += async (_, _) => await CheckThemeNavigation(checkShips);
        Closed += (_, _) =>
        {
            closing = true; benchmark?.Dispose(); tray?.Dispose(); clock.Stop(); displayRefresh.Stop(); artworkAnimation?.Dispose(); cancellation.Cancel();
            worker?.Join(TimeSpan.FromSeconds(3));
            if (latest is not null) SaveSnapshot(latest);
            cancellation.Dispose(); pollingChanged.Dispose();
        };
    }

    private void BuildLayout()
    {
        Picture("Images/Bars/TopBar.png", 178, 0, 846, 18);
        Picture("Images/Bars/BottomBar.png", 115, 558, 909, 18);
        foreach (var position in new[] { (212, 108), (636, 108), (212, 458), (636, 458) })
        {
            Picture("Images/Title Lines/0.png", position.Item1, position.Item2, 380, 2);
            Picture("Images/Title Lines/1.png", position.Item1, position.Item2, 118, 5);
        }
        Picture("Images/Corner Lines/CLine1.png", 212, 108, 101, 277);
        Picture("Images/Corner Lines/CLine1.png", 636, 108, 101, 277);
        Picture("Images/Corner Lines/CLine2.png", 212, 454, 101, 98);
        Picture("Images/Corner Lines/CLine2.png", 636, 458, 101, 94);
        foreach (var separator in new[] { (333, 288), (454, 288), (756, 288), (888, 288), (333, 472), (490, 472) })
            Add(new Border { Width = 1, Height = separator.Item2 == 472 ? 76 : 88, Background = new SolidColorBrush(Color.FromRgb(177, 177, 177)) }, separator.Item1, separator.Item2);
        Add(new Border { Width = 1, Height = 105, Background = new SolidColorBrush(Color.FromRgb(177, 177, 177)) }, 736, 121);
        var leftArt = Picture("Animations/L/Comp 0.png", 0, 0, 200, 576);
        var rightArt = Picture("Animations/R/Comp 0.png", 1004, 0, 20, 576);
        var brandArt = Picture("Animations/X/X0.png", 724, 17, 35, 27);
        artworkAnimation = new(leftArt, rightArt, brandArt);
        Label("fanMade", "FAN MADE", 26, 551, 15, 115, color: Brushes.White);

        Label("boardName", "MOTHERBOARD", 220, 19, 25, 498, title: true, color: new SolidColorBrush(Color.FromRgb(73, 73, 73)));
        Label("date", "", 790, 21, 19, 220, color: Artwork.Gray);
        Label("cpuName", "CPU", 220, 78, 21, 375, title: true);
        Label("gpuName", "GPU", 644, 78, 21, 355, title: true);

        Label("cpuClockLabel", "CPU Clock", 220, 119, 18, 200, color: Artwork.Gray);
        Add(cpuGraph, 220, 152);
        Label("cpuClock", "—", 220, 198, 58, 140);
        Label("cpuClockUnit", "MHz", 361, 229, 23, 65);
        Add(cpuGauge, 440, 121);
        cpuGauge.ToolTip = "Ryzen CPU package temperature: Tctl/Tdie. Maximum since panel launch.";
        Label("cpuUsageLabel", "CPU Usage", 220, 284, 14, 115, color: Artwork.Gray);
        Label("cpuUsage", "—", 220, 309, 30, 111);
        Add(cpuBar, 220, 352);
        Label("voltageLabel", "Vcore", 341, 284, 14, 110, color: Artwork.Gray);
        Label("voltage", "—", 341, 309, 30, 110);
        Label("voltageNotice", "Unavailable", 341, 348, 13, 109, color: Artwork.Gray);
        text["voltage"].ToolTip = "CPU VDDCR_VDD measured rail voltage from SVI3 firmware telemetry.";
        Label("cpuPowerLabel", "CPU Power", 462, 284, 14, 115, color: Artwork.Gray);
        Label("cpuPower", "—", 462, 309, 30, 129);
        Label("cpuPowerMax", "max: —", 462, 348, 16, 129, color: Artwork.Gray);

        Label("fpsLabel", "Display FPS", 644, 119, 16, 89, color: Artwork.Gray);
        Label("fps", "—", 644, 160, 40, 89);
        Label("fpsNotice", "Unavailable", 644, 209, 13, 89, color: Artwork.Gray);
        Label("fpsApplication", "", 644, 231, 11, 89, color: Artwork.Gray);
        text["fps"].ToolTip = "Windows displayed-frame FPS. Automatically detects running games and retains the selected game when you switch to desktop apps.";
        Label("gpuPowerLabel", "GPU Power", 744, 119, 18, 115, color: Artwork.Gray);
        Label("gpuPower", "—", 744, 160, 40, 115);
        Label("gpuPowerMax", "max: —", 744, 209, 16, 115, color: Artwork.Gray);
        Add(gpuGauge, 864, 121);
        gpuGauge.ToolTip = "NVIDIA GPU temperature. Maximum since panel launch.";
        Label("gpuUsageLabel", "GPU Usage", 644, 284, 14, 109, color: Artwork.Gray);
        Label("gpuUsage", "—", 644, 309, 30, 109);
        Add(gpuBar, 644, 352);
        Label("gpuClockLabel", "GPU Clock", 764, 284, 14, 118, color: Artwork.Gray);
        Add(gpuGraph, 764, 309);
        Label("gpuClock", "—", 764, 343, 25, 123);
        Label("gpuMemoryLabel", "GPU Mem. Clock", 896, 284, 14, 125, color: Artwork.Gray);
        Label("gpuMemory", "—", 896, 309, 28, 109);
        Label("gpuMemoryUnit", "MHz", 896, 343, 18, 109, color: Artwork.Gray);
        Label("boardSensors", "BOARD   System —   PCH —   VRM MOS —   Pump —", 220, 401, 15, 780, color: Artwork.Gray);
        text["boardSensors"].ToolTip = "Motherboard sensor names follow the upstream X870 mapping. Fan-header placement has not been independently traced.";

        Label("systemTitle", "SYSTEM INFO.", 220, 426, 22, 200, title: true);
        Picture("Images/Icons/info.png", 418, 427, 22, 22);
        Label("memoryLabel", "Mem. Usage", 220, 471, 14, 109, color: Artwork.Gray);
        Label("memory", "—", 220, 496, 30, 109);
        Add(memoryBar, 220, 536);
        Label("memoryClockLabel", "Memory Clock", 341, 471, 14, 145, color: Artwork.Gray);
        Label("memoryClock", "—", 341, 496, 30, 145);
        Label("memoryClockNotice", "Not collected", 341, 538, 13, 145, color: Artwork.Gray);
        Label("uptimeLabel", "System Uptime", 500, 471, 14, 125, color: Artwork.Gray);
        Label("uptime", "—", 500, 496, 35, 125);
        Label("networkTitle", "NETWORK", 644, 426, 22, 151, title: true);
        Picture("Images/Icons/net.png", 801, 427, 22, 22);
        Picture("Images/Icons/ULrate.png", 644, 483, 14, 10);
        Picture("Images/Icons/DLrate.png", 644, 525, 14, 10);
        Label("upload", "—", 674, 470, 28, 327);
        Label("download", "—", 674, 512, 28, 327);
        Label("status", "Starting sensors…", 220, 556, 10, 470, color: Artwork.Gray);
        Label("controls", "Right-click for display controls", 745, 556, 10, 184, color: Artwork.Gray);
        text["controls"].Text = "Settings ⚙ · right-click";
        panelLayout = new(board,text,leftArt,rightArt,brandArt);
    }

    private ContextMenu BuildMenu()
    {
        var menu = new ContextMenu();
        var displays = new MenuItem { Header = "Move to display" };
        // WPF cannot open an empty submenu. Populate before it is displayed,
        // rather than waiting for SubmenuOpened on an item without children.
        menu.Opened += (_, _) =>
        {
            displays.Items.Clear();
            foreach (var target in Displays.List())
            {
                var item = new MenuItem { Header = $"{target.Name} · {target.Width}×{target.Height}" };
                item.Click += (_, _) =>
                {
                    display = target; displayRecovery?.Select(target);
                    PanelPreferences.Save(target,fit); ShowPanel();
                };
                displays.Items.Add(item);
            }
        };
        menu.Items.Add(displays);
        var fpsApplications = new MenuItem { Header = "FPS game" };
        menu.Opened += (_, _) =>
        {
            fpsApplications.Items.Clear();
            var automatic = new MenuItem { Header = "Automatic · running games", IsCheckable = true, IsChecked = selectedFpsPid == 0 };
            automatic.Click += (_, _) => Volatile.Write(ref selectedFpsPid, 0);
            fpsApplications.Items.Add(automatic);
            foreach (var game in fpsGames.Running())
            {
                int pid = game.ProcessId;
                var item = new MenuItem { Header = game.Name, IsCheckable = true, IsChecked = selectedFpsPid == pid };
                item.Click += (_, _) => Volatile.Write(ref selectedFpsPid, pid);
                fpsApplications.Items.Add(item);
            }
        };
        menu.Items.Add(fpsApplications);
        var full = new MenuItem { Header = "Fit to display" }; full.Click += (_, _) => FitToDisplay(); menu.Items.Add(full);
        var restore = new MenuItem { Header = "Restore window size" }; restore.Click += (_, _) => SetFit(false); menu.Items.Add(restore);
        var layout = new MenuItem { Header = "Layout" };
        foreach (var choice in new[] { ("Auto","Automatic · screen aspect ratio"),("Classic","Classic · 16:9"),("Wide","Wide sensor strip"),("Portrait","Portrait · stacked sensors") })
        {
            var item = new MenuItem { Header=choice.Item2, IsCheckable=true };
            layout.SubmenuOpened += (_,_) => item.IsChecked=layoutMode==choice.Item1;
            item.Click += (_,_) => { layoutMode=choice.Item1; PanelPreferences.SaveLayout(layoutMode); UpdatePanelLayout(); };
            layout.Items.Add(item);
        }
        menu.Items.Add(layout);
        var themes = new MenuItem { Header = "Theme" };
        foreach (var choice in PanelTheme.All)
        {
            var item = new MenuItem { Header = choice.Name, IsCheckable = true };
            themes.SubmenuOpened += (_, _) => item.IsChecked = theme.Id == choice.Id;
            item.Click += (_, _) =>
            {
                theme = choice;
                if (previewPath is null) PanelPreferences.SaveTheme(theme.Id);
                ApplyTheme(); SaveDisplayState();
            };
            themes.Items.Add(item);
        }
        menu.Items.Add(themes);
        var scaling = new MenuItem { Header = "Scale artwork" };
        foreach (var choice in new[] { ("Fit","Fit · preserve proportions"),("Fill","Fill · crop outer edges"),("Stretch","Stretch · no borders") })
        {
            var item = new MenuItem { Header=choice.Item2, IsCheckable=true };
            scaling.SubmenuOpened += (_,_) => item.IsChecked=viewport.Stretch==ScalingStretch(choice.Item1);
            item.Click += (_,_) => { viewport.Stretch=ScalingStretch(choice.Item1); PanelPreferences.SaveScaling(choice.Item1); };
            scaling.Items.Add(item);
        }
        menu.Items.Add(scaling);
        var top = new MenuItem { Header = "Always on top", IsCheckable = true, IsChecked = Topmost };
        top.Click += (_, _) => Topmost = top.IsChecked; menu.Items.Add(top);
        var animate = animationCommand = new MenuItem { Header = "Animate AORUS artwork", IsCheckable = true, IsChecked = true };
        animate.Click += (_, _) => { animateArtwork = animate.IsChecked; if (artworkAnimation is not null) artworkAnimation.Enabled = animateArtwork && !theme.ShipConsole; };
        menu.Opened += (_, _) => animate.IsEnabled = !theme.ShipConsole;
        menu.Items.Add(animate);
        var readoutEffects = new MenuItem { Header = "Sensor readouts" };
        var pulse = new MenuItem { Header = "Pulse changed numbers", IsCheckable = true, IsChecked = readoutPulse };
        pulse.Click += (_, _) =>
        {
            readoutPulse=pulse.IsChecked; shipPanel?.SetReadoutEffects(readoutPulse,gaugeTransitions);
            if (previewPath is null) PanelPreferences.SaveReadoutPulse(readoutPulse);
            SaveDisplayState();
        };
        var smooth = new MenuItem { Header = "Smooth temperature rings", IsCheckable = true, IsChecked = gaugeTransitions };
        smooth.Click += (_, _) =>
        {
            gaugeTransitions=smooth.IsChecked; shipPanel?.SetReadoutEffects(readoutPulse,gaugeTransitions);
            if (previewPath is null) PanelPreferences.SaveGaugeTransitions(gaugeTransitions);
            SaveDisplayState();
        };
        readoutEffects.Items.Add(pulse); readoutEffects.Items.Add(smooth); menu.Items.Add(readoutEffects);
        var holographic = new MenuItem { Header = "Holographic display" };
        var holoMotion = new MenuItem { Header = "Projection motion and ship rotation", IsCheckable = true, IsChecked = holographicMotion };
        holoMotion.Click += (_, _) =>
        {
            holographicMotion=holoMotion.IsChecked; shipPanel?.SetMotion(holographicMotion);
            if (previewPath is null) PanelPreferences.SaveHolographicMotion(holographicMotion);
            SaveDisplayState();
        };
        menu.Opened += (_, _) => readoutEffects.IsEnabled=holographic.IsEnabled=theme.ShipConsole;
        holographic.Items.Add(holoMotion); menu.Items.Add(holographic);
        var polling = new MenuItem { Header = "Sensor polling interval" };
        menu.Opened += (_, _) => polling.Header = $"Sensor polling · {Volatile.Read(ref pollingIntervalMs)} ms";
        foreach (int interval in new[] { 500, 1000, 2000, 5000 })
        {
            var item = new MenuItem { Header = interval == 1000 ? "1000 ms · recommended default" : $"{interval} ms", IsCheckable = true };
            polling.SubmenuOpened += (_, _) => item.IsChecked = Volatile.Read(ref pollingIntervalMs) == interval;
            item.Click += (_, _) => SetPollingInterval(interval); polling.Items.Add(item);
        }
        polling.Items.Add(new Separator());
        var custom = new MenuItem { Header = "Custom interval…" };
        custom.Click += (_, _) => new PollingDialog(this, Volatile.Read(ref pollingIntervalMs), value => SetPollingInterval(value)).ShowDialog();
        polling.Items.Add(custom); menu.Items.Add(polling);
        var save = new MenuItem { Header = "Save panel image" };
        save.Click += (_, _) => Capture(Path.Combine(PanelPaths.Artifacts, "panel-ui.png")); menu.Items.Add(save);
        var readme = new MenuItem { Header = "Open README" };
        readme.Click += (_, _) => PanelBranding.ShowReadme(); menu.Items.Add(readme);
        var hide = new MenuItem { Header = "Hide to tray" };
        hide.Click += (_, _) => HidePanel(); menu.Items.Add(hide);
        var startup = new MenuItem { Header = "Start with Windows", IsCheckable = true };
        menu.Opened += (_, _) => startup.IsChecked = ScheduledStartup.IsEnabled();
        startup.Click += (_, _) =>
        {
            try { ScheduledStartup.SetEnabled(startup.IsChecked); }
            catch (Exception ex) { startup.IsChecked = ScheduledStartup.IsEnabled(); MessageBox.Show(ex.Message, PanelBranding.Name); }
        };
        menu.Items.Add(startup);
        menu.Items.Add(new Separator());
        var exit = new MenuItem { Header = "Close panel (Alt+F4)" }; exit.Click += (_, _) => Close(); menu.Items.Add(exit);
        return menu;
    }

    private void PlaceInitialDisplay()
    {
        var displays = Displays.List();
        display = previewPath is not null && !sensorPreview ? displays.FirstOrDefault(d => d.Primary) : PanelPreferences.Preferred(displays);
        if (sensorPreview && display is not { Primary:false }) throw new InvalidOperationException("A secondary screen is required for sensor-display preview; the primary workspace will not be used.");
        // Automatic fullscreen is limited to the actual attached sensor display.
        // A missing/primary match must never cover the owner's working screen.
        if (previewPath is null)
            fit = PanelPreferences.AutomaticFit(display);
        hideInitially = previewPath is null && !PanelPreferences.ShowInitially(display);
        if (display is not null) displayRecovery = new(display);
        if (display is not null) Displays.Place(this, display, fit,previewSize);
    }

    private void Start()
    {
        if (hideInitially) Hide();
        Tick();
        _ = LoadArtwork();
        if (previewPath is not null)
        {
            var snapshot = JsonSerializer.Deserialize<Snapshot>(File.ReadAllText(previewPath), JsonOptions)
                ?? throw new InvalidOperationException("The preview snapshot is empty.");
            Apply(snapshot, new(null, null, null, "Saved snapshot"));
            return;
        }
        worker = new Thread(Poll) { IsBackground = true, Name = "Sensor collector" };
        worker.Start();
        if (benchmarkPolling) benchmark = new(this, value => SetPollingInterval(value, false), () => (Interlocked.Read(ref collectionCycles),Interlocked.Read(ref collectionTicks)));
    }

    private void Poll()
    {
        try
        {
            using var collector = new Collector(useLibre: true, fpsProcessId: Volatile.Read(ref selectedFpsPid), usePresentMon: true);
            var runtime = new RuntimeReader();
            cancellation.Token.WaitHandle.WaitOne(250);
            DateTimeOffset nextSnapshot = DateTimeOffset.MinValue;
            WaitHandle[] waits = [cancellation.Token.WaitHandle, pollingChanged];
            while (!cancellation.IsCancellationRequested)
            {
                long started = Stopwatch.GetTimestamp();
                collector.FpsProcessId = Volatile.Read(ref selectedFpsPid);
                var snapshot = collector.Read();
                var statistics = runtime.Read();
                Interlocked.Increment(ref collectionCycles);
                Interlocked.Add(ref collectionTicks, Stopwatch.GetTimestamp()-started);
                if (DateTimeOffset.UtcNow >= nextSnapshot) { SaveSnapshot(snapshot); nextSnapshot = DateTimeOffset.UtcNow.AddSeconds(30); }
                Volatile.Write(ref pendingUpdate, new(snapshot, statistics));
                if (Interlocked.CompareExchange(ref applyPending, 1, 0) == 0)
                    Dispatcher.BeginInvoke(() =>
                    {
                        var update = Volatile.Read(ref pendingUpdate);
                        Interlocked.Exchange(ref applyPending, 0);
                        if (!closing && update is not null) Apply(update.Snapshot, update.Statistics);
                    });
                int wait = PollingWaitMilliseconds(Volatile.Read(ref pollingIntervalMs), Stopwatch.GetElapsedTime(started).TotalMilliseconds);
                WaitHandle.WaitAny(waits, wait);
            }
        }
        catch (Exception ex)
        {
            File.WriteAllText(Path.Combine(PanelPaths.Artifacts, "panel-error.txt"), ex.ToString());
            if (!closing) Dispatcher.BeginInvoke(() => Clear("Sensor collection stopped; see panel-error.txt"));
        }
    }

    internal static int PollingWaitMilliseconds(int interval, double elapsedMs) => Math.Max(0, interval - (int)Math.Ceiling(elapsedMs));
    private void SetPollingInterval(int value, bool persist = true)
    {
        if (value is < 500 or > 5000) throw new ArgumentOutOfRangeException(nameof(value));
        if (persist) PanelPreferences.SavePollingInterval(value);
        Volatile.Write(ref pollingIntervalMs, value); pollingChanged.Set();
    }

    private void Apply(Snapshot snapshot, RuntimeStats statistics)
    {
        latest = snapshot; lastReceived = DateTimeOffset.UtcNow; stale = false;
        var metrics = new PanelMetrics(snapshot, lastReceived, previewPath is not null, Volatile.Read(ref pollingIntervalMs));
        text["boardName"].Text = snapshot.Motherboard ?? "Motherboard unavailable";
        text["boardName"].ToolTip = snapshot.Motherboard;
        text["cpuName"].Text = (snapshot.Cpu ?? "CPU unavailable").Replace(" 16-Core Processor", "", StringComparison.Ordinal);
        text["cpuName"].ToolTip = snapshot.Cpu;
        text["gpuName"].Text = metrics.GpuName;
        text["cpuClock"].Text = Number(metrics.CpuClock);
        text["cpuUsage"].Text = Number(metrics.CpuUsage, "%", "0.0");
        text["cpuPower"].Text = Number(metrics.CpuPower, "W");
        text["cpuPowerMax"].Text = "max: " + Number(Peak("cpuPower", metrics.CpuPower), "W");
        text["voltage"].Text = Number(metrics.CpuVoltage, "V", "0.000");
        text["voltageNotice"].Text = metrics.CpuVoltage.HasValue ? "SVI3 TFN" : "Unavailable";
        text["voltage"].ToolTip = snapshot.Readings.FirstOrDefault(r => r.Id == "cpu/core-voltage")?.Detail;
        cpuGauge.Update(metrics.CpuTemperature, Peak("cpuTemperature", metrics.CpuTemperature));
        double? gpuTemperature = metrics.Gpu("temperature"), gpuPower = metrics.Gpu("power"), gpuClock = metrics.Gpu("graphics-clock");
        gpuGauge.Update(gpuTemperature, Peak("gpuTemperature", gpuTemperature));
        text["cpuTemperature"].Text = Number(metrics.CpuTemperature, "°C");
        text["cpuTemperatureMax"].Text = Number(metrics.CpuTemperature.HasValue ? maxima.GetValueOrDefault("cpuTemperature") : null, "°C");
        text["gpuTemperature"].Text = Number(gpuTemperature, "°C");
        text["gpuTemperatureMax"].Text = Number(gpuTemperature.HasValue ? maxima.GetValueOrDefault("gpuTemperature") : null, "°C");
        shipPanel?.Feed(metrics.CpuTemperature, gpuTemperature, metrics.CpuClock, gpuClock);
        text["gpuPower"].Text = Number(gpuPower, "W");
        text["gpuPowerMax"].Text = "max: " + Number(Peak("gpuPower", gpuPower), "W");
        text["gpuUsage"].Text = Number(metrics.Gpu("utilization"), "%", "0.0");
        text["gpuClock"].Text = Number(gpuClock, "MHz");
        text["gpuMemory"].Text = Number(metrics.Gpu("memory-clock"));
        text["fps"].Text = Number(metrics.Fps);
        text["fpsNotice"].Text = metrics.Fps.HasValue ? "max: " + Number(Peak(metrics.FpsId ?? "fps", metrics.Fps))
            : snapshot.Sources.FirstOrDefault(s => s.Source == PresentMonSource.SourceName)?.Status == "no-game" ? "No game"
            : snapshot.Sources.FirstOrDefault(s => s.Source == PresentMonSource.SourceName)?.Status == "access-denied" ? "Trace denied"
            : snapshot.Sources.FirstOrDefault(s => s.Source == FrameViewSource.SourceName)?.Status == "service-stopped" ? "Service off" : "No frame data";
        text["fps"].ToolTip = metrics.FpsDetail;
        text["fpsApplication"].Text = metrics.FpsApplication ?? "";
        text["fpsApplication"].ToolTip = metrics.FpsDetail;
        text["memoryClock"].Text = Number(metrics.MemoryClock, "MHz");
        text["memoryClock"].FontSize = 27;
        text["memoryClockNotice"].Text = Number(metrics.MemoryDataRate, "MT/s");
        text["memoryClock"].ToolTip = snapshot.Readings.FirstOrDefault(r => r.Id == "ram/mclk")?.Detail;
        cpuBar.Update(metrics.CpuUsage); gpuBar.Update(metrics.Gpu("utilization"));
        cpuGraph.Add(metrics.CpuClock); gpuGraph.Add(gpuClock);
        text["boardSensors"].Text = $"BOARD   System {Number(metrics.BoardSystem, "°C")}    PCH {Number(metrics.BoardPch, "°C")}    VRM MOS {Number(metrics.BoardVrm, "°C")}    Pump {Number(metrics.BoardPump, "RPM")}";
        text["memory"].Text = Number(statistics.MemoryUsage, "%", "0.0"); memoryBar.Update(statistics.MemoryUsage);
        text["upload"].Text = Rate(statistics.UploadBytes); text["download"].Text = Rate(statistics.DownloadBytes);
        text["upload"].ToolTip = text["download"].ToolTip = $"Network adapter: {statistics.NetworkName}";
        bool complete = metrics.CpuTemperature.HasValue && gpuTemperature.HasValue;
        text["status"].Text = previewPath is not null ? $"Saved snapshot · {snapshot.CapturedAt.ToLocalTime():HH:mm:ss}"
            : complete ? $"Live · sensor polling {Volatile.Read(ref pollingIntervalMs)} ms" : !snapshot.Elevated ? "Run as administrator for CPU and motherboard sensors" : "Some sensors are unavailable";
        text["status"].ToolTip = string.Join("\n", snapshot.Sources.Select(s => $"{s.Source}: {s.Status}\n{s.Detail}"));
        if (capturePath is not null && !captured && (previewPath is not null || ++samples >= 3))
        {
            captured = true;
            Dispatcher.BeginInvoke(DispatcherPriority.ContextIdle, new Action(() => Capture(capturePath)));
        }
    }

    private void Tick()
    {
        // Some drivers announce display change before enumeration is ready.
        // Retry only while the selected monitor is missing.
        if (displayRecovery?.Waiting == true) RefreshConnectedDisplay();
        text["date"].Text = DateTime.Now.ToString("HH:mm  MM/dd/yyyy", CultureInfo.InvariantCulture);
        var uptime = TimeSpan.FromMilliseconds(Environment.TickCount64);
        text["uptime"].Text = $"{(int)uptime.TotalHours:00}:{uptime.Minutes:00}";
        if (previewPath is null && latest is not null && DateTimeOffset.UtcNow - lastReceived > TimeSpan.FromMilliseconds(Math.Max(5000, Volatile.Read(ref pollingIntervalMs)*2)) && !stale)
            Clear("Readings expired; waiting for sensors");
    }

    private void Clear(string message)
    {
        stale = true;
        foreach (string key in new[] { "cpuClock", "cpuUsage", "cpuPower", "voltage", "gpuPower", "gpuUsage", "gpuClock", "gpuMemory", "memory", "memoryClock", "memoryClockNotice", "fps", "upload", "download", "cpuTemperature", "cpuTemperatureMax", "gpuTemperature", "gpuTemperatureMax" }) text[key].Text = "—";
        text["voltageNotice"].Text = "Unavailable";
        text["fpsNotice"].Text = "No frame data";
        text["fpsApplication"].Text = "";
        text["cpuPowerMax"].Text = text["gpuPowerMax"].Text = "max: —";
        text["boardSensors"].Text = "BOARD   System —   PCH —   VRM MOS —   Pump —";
        cpuGauge.Update(null, null); gpuGauge.Update(null, null);
        cpuBar.Update(null); gpuBar.Update(null); memoryBar.Update(null);
        cpuGraph.Add(null); gpuGraph.Add(null);
        shipPanel?.Feed(null, null, null, null);
        text["status"].Text = message;
    }

    private double? Peak(string name, double? value)
    {
        if (!value.HasValue) return null;
        maxima[name] = Math.Max(maxima.GetValueOrDefault(name, value.Value), value.Value);
        return maxima[name];
    }

    private void ToggleFit()
    {
        SetFit(!fit);
    }

    private void ResolveDisplay()
    {
        var displays = Displays.List();
        display = displayRecovery is null ? PanelPreferences.Preferred(displays) : displayRecovery.Connected(displays);
    }

    private void SetFit(bool value)
    {
        ResolveDisplay();
        if (display is null) { text["status"].Text = "No connected display found"; return; }
        fit = value;
        displayRecovery?.Select(display);
        PanelPreferences.Save(display,fit);
        WindowState = WindowState.Normal;
        Displays.Place(this, display, fit);
        Dispatcher.BeginInvoke(DispatcherPriority.ContextIdle,new Action(SaveDisplayState));
    }

    internal void FitToDisplay()
    {
        SetFit(true);
        ShowPanel();
    }

    internal void ShowPanel()
    {
        ResolveDisplay();
        if (display is null) { text["status"].Text = "Selected display is disconnected"; return; }
        displayRecovery?.Select(display);
        WindowState = WindowState.Normal;
        if (display is not null) Displays.Place(this, display, fit);
        Show();
        if (display is not null) Displays.Place(this, display, fit);
        Activate(); // Only explicit Show/Fit requests activate the panel.
        Dispatcher.BeginInvoke(DispatcherPriority.ContextIdle,new Action(SaveDisplayState));
    }

    internal void HidePanel()
    {
        displayRecovery?.HideByUser();
        Hide();
        SaveDisplayState();
    }
    internal void ShowSettings()
    {
        ShowPanel();
        if (IsVisible) settings?.Open();
    }

    private static Stretch ScalingStretch(string mode) => mode switch { "Fill"=>Stretch.UniformToFill, "Stretch"=>Stretch.Fill, _=>Stretch.Uniform };
    private void UpdatePanelLayout()
    {
        if (ActualWidth>0 && ActualHeight>0) panelLayout?.Apply(ActualWidth/ActualHeight,layoutMode);
        if (theme.ShipConsole) shipPanel?.Configure(theme, ActualWidth > 0 && ActualHeight > 0 ? ActualWidth / ActualHeight : 16.0 / 9, layoutMode);
        ApplyFanMadeColor();
    }
    private void ApplyFanMadeColor()
    {
        if (text.TryGetValue("fanMade", out var label)) label.Foreground = panelLayout?.Mode == "Portrait" ? theme.Foreground : Brushes.White;
    }
    private void ApplyTheme()
    {
        if (shipPanel is not null)
        {
            if (theme.ShipConsole) shipPanel.Configure(theme, ActualWidth > 0 && ActualHeight > 0 ? ActualWidth / ActualHeight : 16.0 / 9, layoutMode);
            viewport.Child = theme.ShipConsole ? shipPanel : board;
            shipPanel.SetMotion(holographicMotion && theme.ShipConsole);
            shipPanel.SetReadoutEffects(readoutPulse,gaugeTransitions);
        }
        if (artworkAnimation is not null) artworkAnimation.Enabled = animateArtwork && !theme.ShipConsole;
        if (animationCommand is not null) animationCommand.IsEnabled = !theme.ShipConsole;
        board.Background = theme.Backdrop;
        foreach (var pair in mutedLabels) pair.Key.Foreground = pair.Value ? theme.Muted : theme.Foreground;
        text["boardName"].Foreground = theme.Header;
        ApplyFanMadeColor();
        foreach (var divider in dividers) divider.Background = theme.Divider;
        foreach (var entry in themeImages)
        {
            if (theme.Classic) { entry.Image.Source = entry.Original; continue; }
            var brush = entry.Path.Contains("ULrate") || entry.Path.Contains("DLrate") ? theme.Accent
                : entry.Path.Contains("Icons/") ? theme.Muted
                : entry.Path.Contains("TopBar") || entry.Path.Contains("BottomBar") ? theme.Chrome : theme.Divider;
            var drawing = new DrawingGroup { OpacityMask = new ImageBrush(entry.Original) { Stretch = Stretch.Fill } };
            drawing.Children.Add(new GeometryDrawing(brush, null, new RectangleGeometry(new Rect(0, 0, 1, 1))));
            drawing.Freeze(); entry.Image.Source = new DrawingImage(drawing);
        }
        var gpuTheme = theme.ForGpu();
        cpuGauge.ApplyTheme(theme); gpuGauge.ApplyTheme(gpuTheme);
        cpuGraph.ApplyTheme(theme); gpuGraph.ApplyTheme(gpuTheme);
        cpuBar.ApplyTheme(theme); gpuBar.ApplyTheme(gpuTheme); memoryBar.ApplyTheme(theme);
        settings?.ApplyTheme();
    }
    private void SaveDisplayState()
    {
        if (closing) return;
        var bounds=Displays.CurrentBounds(this);
        File.WriteAllText(Path.Combine(PanelPaths.Artifacts,"panel-display-state.json"),JsonSerializer.Serialize(new
        {
            Version=typeof(App).Assembly.GetName().Version?.ToString(), Preview=previewPath is not null,
            Display=display, Bounds=bounds, Fit=fit, FitsTarget=display is not null && bounds==Displays.Placement(display,true),
            Dpi=Displays.Dpi(this), Layout=theme.ShipConsole ? shipPanel?.Mode : panelLayout?.Mode, Renderer=theme.ShipConsole ? "ShipConsole" : "Aorus", Scaling=viewport.Stretch.ToString(), Visible=IsVisible,
            ShowInTaskbar, TrayIconVisible=tray?.Visible ?? false, Theme=theme.Id, SettingsOpen=settings?.IsOpen ?? false,
            HolographicMotion=holographicMotion, ReadoutPulse=readoutPulse, GaugeTransitions=gaugeTransitions,
            RotatingShipInLeftPanel=theme.ShipConsole && ShipPortrait.Supports(theme.ShipId),
            NavigationFocus=theme.ShipConsole ? shipPanel?.NavigationFocus : null,
            HolotableOpen=shipPanel?.HolotableOpen ?? false
        },JsonOptions));
    }
    private nint DisplayMessage(nint hwnd,int message,nint wParam,nint lParam,ref bool handled)
    {
        if (message==0x007E) { displayRefresh.Stop(); displayRefresh.Start(); }
        return 0;
    }

    private void RefreshConnectedDisplay()
    {
        if (closing || displayRecovery is null) return;
        var result = displayRecovery.Refresh(Displays.List(), IsVisible);
        if (result.Display is null)
        {
            Hide(); // Preserve selected display and fit, without saving a fallback.
            SaveDisplayState();
            return;
        }
        display = result.Display;
        Displays.Place(this,display,fit,previewSize);
        if (result.Show)
        {
            Show(); // Automatic reconnection does not activate the window.
            Displays.Place(this,display,fit,previewSize);
        }
        Dispatcher.BeginInvoke(DispatcherPriority.ContextIdle,new Action(SaveDisplayState));
    }

    private void Capture(string path)
    {
        FrameworkElement target = theme.ShipConsole ? shipPanel! : board;
        target.UpdateLayout();
        var bitmap = new RenderTargetBitmap((int)Math.Ceiling(target.Width), (int)Math.Ceiling(target.Height), 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(target);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using var stream = File.Create(path); encoder.Save(stream);
        File.WriteAllText(Path.Combine(PanelPaths.Artifacts, "panel-animation-info.json"), JsonSerializer.Serialize(new
        {
            artworkAnimation?.Enabled, artworkAnimation?.TickCount, artworkAnimation?.FrameIndex,
            artworkAnimation?.UniqueFrameCount, LeftFrameCount = 175, RightFrameCount = 175, BrandFrameCount = 4,
            SocialIcons = false
        }, JsonOptions));
        File.WriteAllText(Path.Combine(PanelPaths.Artifacts, "panel-render-info.txt"),
            $"Rendered WPF canvas: {target.Width:0}x{target.Height:0}; layout {(theme.ShipConsole ? shipPanel?.Mode : panelLayout?.Mode)}. Saved {path}\nFonts:\n" +
            string.Join("\n", Fonts.GetFontFamilies(new Uri("pack://application:,,,/"), "./Aorus/Fonts/").Select(f => f.Source)));
    }

    private async Task CheckThemeNavigation(bool shipOnly)
    {
        try
        {
            var baseline = new[] { "cpuClock", "cpuUsage", "cpuPower", "voltage", "gpuPower", "gpuClock", "gpuMemory", "memoryClock", "fps" }
                .ToDictionary(key => key, key => text[key].Text);
            var baselineMaxima = maxima.ToDictionary(pair => pair.Key, pair => pair.Value);
            var choices = shipOnly ? PanelTheme.All.Where(t => t.ShipConsole).Append(PanelTheme.Resolve("Dark")).ToArray() : PanelTheme.All.ToArray();
            string output = Path.Combine(PanelPaths.Artifacts, "theme-previews", previewSize is { } size ? $"{size.Width:0}x{size.Height:0}" : "default");
            Directory.CreateDirectory(output);
            async Task SavePreview(string name)
            {
                await Task.Delay(900); UpdateLayout();
                if (theme.ShipConsole) shipPanel!.CheckBindings();
                if (settings!.IsOpen && !new Rect(0, 0, ActualWidth, ActualHeight).Contains(settings.Bounds))
                    throw new InvalidOperationException("Settings overlay left the visible window.");
                var bitmap = new RenderTargetBitmap((int)Math.Ceiling(ActualWidth), (int)Math.Ceiling(ActualHeight), 96, 96, PixelFormats.Pbgra32);
                bitmap.Render((Visual)Content);
                var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
                using var stream = File.Create(Path.Combine(output, name + ".png")); encoder.Save(stream);
            }
            settings!.Open(); settings.SelectVisibleOption("Theme");
            foreach (var choice in choices)
            {
                settings.SelectVisibleOption(choice.Name);
                await SavePreview(choice.Id.ToLowerInvariant() + "-settings");
                if (theme != choice || baseline.Any(pair => text[pair.Key].Text != pair.Value)
                    || maxima.Count != baselineMaxima.Count || baselineMaxima.Any(pair => maxima[pair.Key] != pair.Value))
                    throw new InvalidOperationException("Theme selection changed a sensor value or failed to apply.");
                settings.NavigateBack();
                if (animationCommand!.IsEnabled != !choice.ShipConsole)
                    throw new InvalidOperationException("Artwork animation availability did not follow the selected renderer.");
                await SavePreview(choice.Id.ToLowerInvariant() + "-settings-root");
                settings.Close(); await SavePreview(choice.Id.ToLowerInvariant());
                if (choice.ShipConsole)
                {
                    string expected=choice.ShipId switch { "Rocinante"=>"Tycho", "Donnager"=>"Callisto", "Truman"=>"Earth", "Tynan"=>"Medina", "Razorback"=>"Earth", "Morrigan" or "Dropship" or "Epstein"=>"Mars", _=>throw new InvalidOperationException("Missing ship navigation check.") };
                    if (shipPanel!.NavigationFocus!=expected) throw new InvalidOperationException("Ship selection focused the wrong planet or station.");
                    shipPanel!.CheckSeparateEffects(); await SavePreview(choice.Id.ToLowerInvariant()+"-projection");
                }
                settings.Open();
                settings.SelectVisibleOption("Theme");
            }
            settings.SelectVisibleOption(PanelTheme.Resolve("Rocinante").Name);
            settings.Close();
            foreach (var route in new[] { ("Donnager","Callisto"), ("Tynan","Medina"), ("Epstein","Mars"), ("Razorback","Earth"), ("Rocinante","Tycho") })
            {
                shipPanel!.SelectShip(route.Item1);
                await Task.Delay(100);
                if (theme.ShipId!=route.Item1 || shipPanel.NavigationFocus!=route.Item2 || baseline.Any(pair=>text[pair.Key].Text!=pair.Value)
                    || maxima.Count!=baselineMaxima.Count || baselineMaxima.Any(pair=>maxima[pair.Key]!=pair.Value))
                    throw new InvalidOperationException("The ship selector did not update navigation independently of sensor values.");
                shipPanel.CheckBindings();
            }
            var savedSnapshot = latest!;
            Clear("Preview: unavailable sensor check");
            await Task.Delay(100);
            shipPanel!.CheckBindings();
            if (text["cpuTemperature"].Text != "—" || text["gpuTemperature"].Text != "—")
                throw new InvalidOperationException("Unavailable temperatures remained visible on the ship console.");
            Apply(savedSnapshot, new(null, null, null, "Saved snapshot"));
            await Task.Delay(100);
            shipPanel.CheckBindings();
            if (baseline.Any(pair => text[pair.Key].Text != pair.Value))
                throw new InvalidOperationException("Restoring readings after an unavailable sample failed.");
            settings.Open(); settings.SelectVisibleOption("Sensor readouts");
            await SavePreview("sensor-readout-controls"); settings.NavigateBack();
            settings.SelectVisibleOption("Holographic display");
            await SavePreview("holographic-controls"); settings.NavigateBack();
            settings.SelectVisibleOption("Theme");
            settings.SelectVisibleOption(choices[^1].Name);
            settings.NavigateBack();
            foreach (string group in new[] { "Move to display", "Layout", "Scale artwork", $"Sensor polling · {pollingIntervalMs} ms" })
            {
                settings.SelectVisibleOption(group); await Task.Delay(100); settings.NavigateBack();
            }
            settings.Close();
            File.WriteAllText(Path.Combine(output, "navigation-check.json"), JsonSerializer.Serialize(new
            {
                Version = typeof(App).Assembly.GetName().Version?.ToString(),
                Passed = true, Themes = choices.Select(t => t.Id), SensorValuesPreserved = true, MaximaPreserved = true, ShipBindingsChecked = true, UnavailableBindingsChecked = true, AnimationStateChecked = true,
                SettingsWithinWindow = true, Display = display, Preview = true, ShipArtworkMode = "RotatingLeftStrip",
                ReadoutEffectsIndependent = true, ShipRotationBoundsChecked = true, OriginalShipMaterialsChecked = true,
                HolographicNumbersOnDashboard = true, ShipInLeftPanel = true,
                SolarSystemChecked = true, ShipNavigationFocusChecked = true, ShipSelectorChecked = true
            }, JsonOptions));
            Close();
        }
        catch (Exception ex)
        {
            File.WriteAllText(Path.Combine(PanelPaths.Artifacts, "theme-check-error.txt"), ex.ToString());
            Close(); Application.Current.Shutdown(1);
        }
    }

    private static void SaveSnapshot(Snapshot snapshot)
    {
        string path = Path.Combine(PanelPaths.Artifacts, "panel-live.json");
        File.WriteAllText(path + ".tmp", JsonSerializer.Serialize(snapshot, JsonOptions));
        File.Move(path + ".tmp", path, true);
    }

    private static string Number(double? value, string unit = "", string format = "0")
        => value.HasValue ? value.Value.ToString(format, CultureInfo.InvariantCulture) + (unit.Length > 0 ? " " + unit : "") : "—";
    private static string Rate(double? bytes)
        => !bytes.HasValue ? "—" : bytes.Value >= 1_000_000 ? $"{bytes / 1_000_000:0.0} MB/s" : $"{bytes / 1000:0.0} kB/s";

    private TextBlock Label(string key, string value, double x, double y, double size, double width, bool title = false, Brush? color = null)
    {
        var label = new TextBlock { Text = value, FontFamily = title ? Artwork.Title : Artwork.Body,
            FontWeight = title ? FontWeights.Normal : FontWeights.SemiBold, FontSize = size,
            Width = width, Foreground = color ?? Brushes.Black, TextTrimming = TextTrimming.CharacterEllipsis };
        mutedLabels[label] = ReferenceEquals(color, Artwork.Gray);
        text[key] = label; Add(label, x, y); return label;
    }
    private async Task LoadArtwork()
    {
        try { if (artworkAnimation is not null) await artworkAnimation.LoadAsync(); }
        catch (Exception ex) { File.WriteAllText(Path.Combine(PanelPaths.Artifacts, "panel-animation-error.txt"), ex.ToString()); }
    }
    private Image Picture(string path, double x, double y, double width, double height)
    {
        var image = new Image { Source = Artwork.Load(path), Width = width, Height = height, Stretch = Stretch.Fill };
        if (path.StartsWith("Images/", StringComparison.Ordinal)) themeImages.Add((image, path, image.Source));
        Add(image, x, y); return image;
    }
    private void Add(UIElement element, double x, double y)
    {
        if (element is Border divider) dividers.Add(divider);
        Canvas.SetLeft(element, x); Canvas.SetTop(element, y); board.Children.Add(element);
    }
}
