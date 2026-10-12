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
    private string layoutMode = PanelPreferences.Layout;
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

    public PanelWindow(string? previewPath, string? capturePath, int fpsPid = 0, bool benchmarkPolling = false, Size? previewSize = null, bool sensorPreview = false)
    {
        this.previewSize=previewSize; this.sensorPreview=sensorPreview;
        this.benchmarkPolling = benchmarkPolling;
        selectedFpsPid = fpsPid;
        this.previewPath = previewPath; this.capturePath = capturePath;
        fit = previewPath is null;
        Title = PanelBranding.Name;
        Icon = PanelBranding.WindowIcon;
        Width = 1024; Height = 576; MinWidth = 640; MinHeight = 360;
        WindowStyle = WindowStyle.None; ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.Manual;
        Background = Brushes.Black; Topmost = false; ShowActivated = false;
        viewport.Stretch = ScalingStretch(PanelPreferences.Scaling); viewport.Child = board; Content = viewport;
        BuildLayout();
        SizeChanged += (_, _) => UpdatePanelLayout();
        DpiChanged += (_, _) => { if (fit && display is not null) Dispatcher.BeginInvoke(() => Displays.Place(this, display, true)); };
        board.MouseLeftButtonDown += (_, e) => { if (e.ClickCount == 2) ToggleFit(); else if (!fit) DragMove(); };
        KeyDown += (_, e) => { if (e.Key == Key.F11) ToggleFit(); else if (e.Key == Key.Escape && fit) ToggleFit(); };
        ContextMenu = BuildMenu();
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
        var animate = new MenuItem { Header = "Animate AORUS artwork", IsCheckable = true, IsChecked = true };
        animate.Click += (_, _) => { if (artworkAnimation is not null) artworkAnimation.Enabled = animate.IsChecked; };
        menu.Items.Add(animate);
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
        foreach (string key in new[] { "cpuClock", "cpuUsage", "cpuPower", "voltage", "gpuPower", "gpuUsage", "gpuClock", "gpuMemory", "memory", "memoryClock", "memoryClockNotice", "fps", "upload", "download" }) text[key].Text = "—";
        text["voltageNotice"].Text = "Unavailable";
        text["fpsNotice"].Text = "No frame data";
        text["fpsApplication"].Text = "";
        text["cpuPowerMax"].Text = text["gpuPowerMax"].Text = "max: —";
        text["boardSensors"].Text = "BOARD   System —   PCH —   VRM MOS —   Pump —";
        cpuGauge.Update(null, null); gpuGauge.Update(null, null);
        cpuBar.Update(null); gpuBar.Update(null); memoryBar.Update(null);
        cpuGraph.Add(null); gpuGraph.Add(null);
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

    private static Stretch ScalingStretch(string mode) => mode switch { "Fill"=>Stretch.UniformToFill, "Stretch"=>Stretch.Fill, _=>Stretch.Uniform };
    private void UpdatePanelLayout()
    {
        if (ActualWidth>0 && ActualHeight>0) panelLayout?.Apply(ActualWidth/ActualHeight,layoutMode);
    }
    private void SaveDisplayState()
    {
        if (closing) return;
        var bounds=Displays.CurrentBounds(this);
        File.WriteAllText(Path.Combine(PanelPaths.Artifacts,"panel-display-state.json"),JsonSerializer.Serialize(new
        {
            Version=typeof(App).Assembly.GetName().Version?.ToString(), Preview=previewPath is not null,
            Display=display, Bounds=bounds, Fit=fit, FitsTarget=display is not null && bounds==Displays.Placement(display,true),
            Dpi=Displays.Dpi(this), Layout=panelLayout?.Mode, Scaling=viewport.Stretch.ToString(), Visible=IsVisible
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
        board.UpdateLayout();
        var bitmap = new RenderTargetBitmap((int)Math.Ceiling(board.Width), (int)Math.Ceiling(board.Height), 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(board);
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
            $"Rendered WPF canvas: {board.Width:0}x{board.Height:0}; layout {panelLayout?.Mode}. Saved {path}\nFonts:\n" +
            string.Join("\n", Fonts.GetFontFamilies(new Uri("pack://application:,,,/"), "./Aorus/Fonts/").Select(f => f.Source)));
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
        Add(image, x, y); return image;
    }
    private void Add(UIElement element, double x, double y)
    { Canvas.SetLeft(element, x); Canvas.SetTop(element, y); board.Children.Add(element); }
}
