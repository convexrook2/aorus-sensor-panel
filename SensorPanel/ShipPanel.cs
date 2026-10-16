using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;

namespace SensorPanel;

// A separate presentation over the same published text/metrics. No second
// collector, fabricated telemetry or artwork animation timer.
internal sealed class ShipPanel : Canvas
{
    internal sealed record Plan(string Mode, double Width, double Height, Rect Sidebar, Rect Cpu, Rect Gpu, Rect Memory, Rect Network, Rect Board, Rect Navigation);
    private readonly Dictionary<string, TextBlock> source;
    private readonly List<(TextBlock Label, string Key, string Suffix)> bound = new();
    private readonly ConsoleDial cpuDial, gpuDial;
    private readonly HistoryGraph cpuGraph = new(), gpuGraph = new();
    private string configuration = "";
    private bool motion = PanelPreferences.HolographicMotion;
    private bool readoutPulse = PanelPreferences.ReadoutPulse;
    private bool gaugeTransitions = PanelPreferences.GaugeTransitions;
    private ShipPortrait? shipPortrait;
    private SolarSystemView? projection;
    private ComboBox? shipChoices;
    private string? selectedShip;
    public string? NavigationFocus => projection?.SelectedBodyId;
    public event Action<string>? ShipSelectionRequested;
    public event Action? NavigationChanged;
    private Holotable? holotable;
    private PanelTheme? activeTheme;
    public bool HolotableOpen => holotable is not null;
    public bool HolotableAvailable => Holotable.Supports(activeTheme?.ShipId);
    public string Mode { get; private set; } = "Landscape";

    public ShipPanel(Dictionary<string, TextBlock> source)
    {
        this.source = source; ClipToBounds = true;
        cpuDial = new(source["cpuTemperature"], source["cpuTemperatureMax"]);
        gpuDial = new(source["gpuTemperature"], source["gpuTemperatureMax"]);
    }

    internal static Plan Calculate(double aspect, string layout)
    {
        aspect = double.IsFinite(aspect) && aspect > 0 ? aspect : 16.0 / 9;
        string mode = layout is "Wide" or "Portrait" ? layout : layout == "Classic" ? "Landscape"
            : aspect >= 2.5 ? "Wide" : aspect < 0.9 ? "Portrait" : "Landscape";
        if (mode == "Portrait")
        {
            double height = Math.Max(1120, 512 / aspect), mapHeight=height-860;
            return new(mode, 512, height, new(8, 100, 104, height - 142), new(126, 100, 368, 210), new(126, 320, 368, 210),
                new(126, 552+mapHeight, 368, 94), new(126, 656+mapHeight, 368, 94), new(126, height-94, 368, 50), new(126,540,368,mapHeight));
        }
        if (mode == "Wide")
        {
            double height = Math.Max(540, 1920 / aspect), small = (height - 176) / 2;
            return new(mode, 1920, height, new(18, 96, 194, height - 160), new(228, 96, 548, 210),
                new(792, 96, 548, 210), new(1356, 96, 546, small), new(1356, 112 + small, 546, small), new(228, height - 52, 1674, 28), new(228,318,1112,height-382));
        }
        double h = layout == "Classic" ? 720 : Math.Max(720, 1280 / aspect);
        return new(mode, 1280, h, new(18, 110, 208, h - 154), new(244, 110, 498, 210), new(760, 110, 498, 210),
            new(244, h-160, 498, 94), new(760, h-160, 498, 94), new(244, h-54, 1014, 28), new(244,332,1014,h-504));
    }

    public void Configure(PanelTheme theme, double aspect, string layout)
    {
        var plan = Calculate(aspect, layout);
        string key = $"{theme.Id}:{plan.Width}:{plan.Height}:{plan.Mode}";
        if (configuration == key) return;
        configuration = key; Mode = plan.Mode;
        CloseHolotable(); activeTheme = theme;
        foreach (var element in new FrameworkElement[] { cpuDial, gpuDial, cpuGraph, gpuGraph })
        {
            if (element.Parent is Panel parent) parent.Children.Remove(element);
            else if (element.Parent is Decorator decorator) decorator.Child = null;
        }
        shipPortrait?.SetMotion(false);
        shipPortrait=null;
        Children.Clear(); bound.Clear(); Width = plan.Width; Height = plan.Height; Background = theme.Backdrop;
        bool portrait = Mode == "Portrait";
        double bodyX=plan.Cpu.X;
        bool reuseProjection=projection is not null;
        if (projection is null)
        {
            projection=new SolarSystemView();
            projection.BodySelected+=_=>NavigationChanged?.Invoke();
        }
        Put(this, projection, plan.Navigation);
        selectedShip=theme.ShipId;
        var navigation=ExpanseNavigation.ForShip(selectedShip);
        projection.FocusBody(navigation.Body,reuseProjection);
        projection.ToolTip=navigation.Relation+" · "+navigation.Place+"\n"+navigation.Source;
        var vessel = theme.Vessel ?? throw new InvalidOperationException("A ship console needs its vessel profile.");
        string title = vessel.Name;
        var heading = Label(title, portrait ? 26 : 38, theme.Header, true);
        Put(this, heading, new(bodyX, 16, plan.Width-bodyX-(portrait ? 18 : 260), 46));
        Put(this, Bind("date", 15, theme.Muted), new(portrait ? bodyX : plan.Width - 245, portrait ? 56 : 25, 224, 24));
        if (!portrait && Mode != "Wide")
            Put(this, Label(vessel.Role + "  /  SENSOR PROJECTION", 12, theme.Muted), new(bodyX+3, 68, plan.Width-bodyX-24, 20));
        Put(this, Bind("boardName", portrait ? 11 : 13, theme.Muted),
            new(bodyX, portrait ? 79 : Mode == "Wide" ? 69 : 88, plan.Width-bodyX-18, 18));
        Put(this, new Border { Background=theme.Chrome, Opacity=.32, CornerRadius=new CornerRadius(10) }, plan.Sidebar);
        if (ShipPortrait.Supports(theme.ShipId))
        {
            shipPortrait=new ShipPortrait(theme.ShipId!);
            Put(this, shipPortrait, new(plan.Sidebar.X+4, plan.Sidebar.Y+36, plan.Sidebar.Width-8, plan.Sidebar.Height-76));
        }
        else Put(this, new ShipArtwork(theme, true) { Opacity=.7 }, new(plan.Sidebar.X+4, plan.Sidebar.Y+36, plan.Sidebar.Width-8, plan.Sidebar.Height-76));
        Put(this, Label(vessel.Faction, portrait ? 13 : 19, theme.Accent, true), new(plan.Sidebar.X+10, plan.Sidebar.Y+12, plan.Sidebar.Width-20, 24));
        Put(this, Label(ShipPortrait.Supports(theme.ShipId) ? "ORIGINAL COLOURS" : "SHIP BLUEPRINT", portrait ? 7 : 9, theme.Muted),
            new(plan.Sidebar.X+8, plan.Sidebar.Bottom-28, plan.Sidebar.Width-16, 18));
        shipChoices=new ComboBox {
            FontSize=portrait ? 8 : 10, Foreground=theme.Foreground, Background=theme.Surface, BorderBrush=theme.Divider,
            ToolTip="Select a ship to focus its associated planet or station", MaxDropDownHeight=240 };
        var itemStyle=new Style(typeof(ComboBoxItem));
        itemStyle.Setters.Add(new Setter(Control.BackgroundProperty,theme.Surface));
        itemStyle.Setters.Add(new Setter(Control.ForegroundProperty,theme.Foreground));
        shipChoices.ItemContainerStyle=itemStyle;
        NavigationControls.Apply(shipChoices);
        foreach (var ship in ShipProfile.All)
            shipChoices.Items.Add(new ComboBoxItem { Content=ship.Name, Tag=ship });
        shipChoices.SelectedIndex=ShipProfile.All.ToList().FindIndex(ship=>ship.Id==selectedShip);
        System.Windows.Automation.AutomationProperties.SetName(shipChoices,"Ship navigation selection");
        shipChoices.SelectionChanged += (_,_) =>
        {
            if (shipChoices.SelectedItem is not ComboBoxItem { Tag: ShipProfile choice } || choice.Id==selectedShip) return;
            ShipSelectionRequested?.Invoke(choice.Id);
        };
        Put(this,shipChoices,new(plan.Sidebar.X+5,plan.Sidebar.Y-34,plan.Sidebar.Width-10,26));
        var contextLabel=Label(navigation.Relation+" · "+navigation.Place,portrait ? 8 : 10,theme.Muted);
        Put(this,contextLabel,new(plan.Navigation.X+8,plan.Navigation.Y-12,plan.Navigation.Width-16,14));
        Hardware(plan.Cpu, theme, false); Hardware(plan.Gpu, theme.ForGpu(), true);
        SmallCard(plan.Memory, theme, false); SmallCard(plan.Network, theme.ForGpu(), true);
        var board = MakeCard(plan.Board, theme);
        var boardText = Bind("boardSensors", portrait ? 11 : 13, theme.Muted);
        boardText.TextWrapping = TextWrapping.Wrap;
        Put(board, boardText, new(10, 5, plan.Board.Width - 20, plan.Board.Height - 6));
        Put(this, Bind("status", portrait ? 9 : 11, theme.Muted), new(20, plan.Height - 22, plan.Width - 160, 18));
        Put(this, Label("FAN MADE", 10, theme.Muted), new(plan.Width - 116, plan.Height - 23, 92, 18));
        if (!portrait)
            Put(this, Label((ShipPortrait.Supports(theme.ShipId) ? "MODEL  /  " + Holotable.ModelCredit(theme.ShipId!) : "ARTWORK  /  " + vessel.Artist), 9, theme.Muted), new(plan.Width - 390, plan.Height - 23, 260, 18));
        cpuDial.ApplyTheme(theme); gpuDial.ApplyTheme(theme.ForGpu());
        cpuGraph.ApplyTheme(theme); gpuGraph.ApplyTheme(theme.ForGpu());
        SetReadoutEffects(readoutPulse, gaugeTransitions); SetMotion(motion);
    }

    private void Hardware(Rect bounds, PanelTheme theme, bool gpu)
    {
        var card = MakeCard(bounds, theme);
        bool compact = bounds.Height < 260;
        Put(card, Label(gpu ? "02  /  GRAPHICS ARRAY" : "01  /  PROCESSOR CORE", 11, theme.Accent, true), new(17, 12, bounds.Width - 34, 18));
        Put(card, Bind(gpu ? "gpuName" : "cpuName", compact ? 15 : 19, theme.Foreground), new(17, 35, bounds.Width - 34, 28));
        double dialSize = compact ? 118 : 154;
        var host = new Viewbox { Child = gpu ? gpuDial : cpuDial, Stretch = Stretch.Uniform };
        Put(card, host, new(12, compact ? 64 : 82, dialSize, dialSize));
        double x = dialSize + 31, y = compact ? 67 : 85, width = (bounds.Width - x - 14) / 2;
        double row = compact ? 44 : 62;
        var fields = gpu
            ? new[] { ("LOAD", "gpuUsage", ""), ("POWER", "gpuPower", ""), ("CLOCK", "gpuClock", ""), ("DISPLAY FPS", "fps", ""), ("VRAM CLOCK", "gpuMemory", " MHz"), ("POWER PEAK", "gpuPowerMax", "") }
            : new[] { ("LOAD", "cpuUsage", ""), ("POWER", "cpuPower", ""), ("CLOCK", "cpuClock", " MHz"), ("VCORE", "voltage", ""), ("POWER PEAK", "cpuPowerMax", ""), ("VOLTAGE SOURCE", "voltageNotice", "") };
        for (int i = 0; i < fields.Length; i++)
            Metric(card, fields[i].Item1, fields[i].Item2, fields[i].Item3, new(x + (i % 2) * width, y + (i / 2) * row, width - 5, row - 4), theme, compact ? 19 : 25);
        if (!compact)
        {
            var graph = gpu ? gpuGraph : cpuGraph;
            Put(card, graph, new(18, bounds.Height - 40, bounds.Width - 36, 26));
        }
    }

    private void SmallCard(Rect bounds, PanelTheme theme, bool network)
    {
        var card = MakeCard(bounds, theme);
        bool compact = bounds.Height < 120;
        Put(card, Label(network ? "04  /  NETWORK LINK" : "03  /  MEMORY & SESSION", 11, theme.Accent, true), new(17, 11, bounds.Width - 34, 20));
        var fields = network ? new[] { ("UPLOAD", "upload"), ("DOWNLOAD", "download"), ("FPS APPLICATION", "fpsApplication") }
            : new[] { ("MEMORY LOAD", "memory"), ("RAM CLOCK", "memoryClock"), ("UPTIME", "uptime") };
        double width = (bounds.Width - 34) / 3;
        for (int i = 0; i < fields.Length; i++)
            Metric(card, fields[i].Item1, fields[i].Item2, "", new(17 + i * width, compact ? 39 : 55, width - 8, compact ? 43 : 65), theme, compact ? 19 : 27);
        if (!compact)
            Put(card, Bind(network ? "fpsNotice" : "memoryClockNotice", 11, theme.Muted), new(17, bounds.Height - 26, bounds.Width - 34, 19));
    }

    private Canvas MakeCard(Rect bounds, PanelTheme theme)
    {
        var card = new Canvas { ClipToBounds = true };
        Put(this, card, bounds);
        // An open projection plane rather than an opaque dashboard tile.
        var plane=new StreamGeometry();
        using (var path=plane.Open()) { path.BeginFigure(new Point(8,0),true,true); path.PolyLineTo(new Point[] {new(bounds.Width-12,0),new(bounds.Width,bounds.Height-8),new(0,bounds.Height)},true,false); }
        plane.Freeze();
        var wash=new SolidColorBrush(Color.FromArgb(42,theme.Surface.Color.R,theme.Surface.Color.G,theme.Surface.Color.B)); wash.Freeze();
        card.Children.Add(new System.Windows.Shapes.Path { Data=plane, Fill=wash, Stroke=theme.Divider, StrokeThickness=.6, Opacity=.72, IsHitTestVisible=false });
        Put(card, new Border { Background=theme.Accent, Opacity=.8 }, new(8,0,Math.Min(56,bounds.Width/4),1));
        return card;
    }
    private void Metric(Canvas card, string caption, string key, string suffix, Rect bounds, PanelTheme theme, double size)
    {
        Put(card, Label(caption, 9, theme.Muted), new(bounds.X, bounds.Y, bounds.Width, 15));
        Brush ink = key.Contains("Power", StringComparison.OrdinalIgnoreCase) || key == "voltage" ? Brushes.LightSalmon
            : key == "fps" || key == "download" ? Brushes.Aquamarine
            : key is "voltageNotice" or "fpsApplication" or "uptime" ? theme.Foreground : theme.Accent;
        var value = Bind(key, size, ink, suffix);
        value.FontFamily = theme.ConsoleStyle is "Mars" or "Belt" or "Patrol" or "Landing" ? new FontFamily("Consolas") : new FontFamily("Segoe UI");
        var host = new Viewbox { Child = value, Stretch = Stretch.Uniform, StretchDirection = StretchDirection.DownOnly,
            HorizontalAlignment = HorizontalAlignment.Left };
        Put(card, host, new(bounds.X, bounds.Y + 15, bounds.Width, bounds.Height - 15));
    }
    private static TextBlock Label(string text, double size, Brush foreground, bool heading = false)
        => new() { Text = text, FontFamily = new FontFamily(heading ? "Bahnschrift" : "Segoe UI"), FontSize = size,
            FontWeight = heading ? FontWeights.SemiBold : FontWeights.Normal, Foreground = foreground, TextTrimming = TextTrimming.CharacterEllipsis };
    private TextBlock Bind(string key, double size, Brush foreground, string suffix = "")
    {
        TextBlock label = size >= 19 ? new HoloReadout(foreground, readoutPulse) { FontSize = size, FontFamily = new FontFamily("Segoe UI") }
            : Label("", size, foreground);
        label.SetBinding(TextBlock.TextProperty, new Binding(nameof(TextBlock.Text)) { Source = source[key], StringFormat = "{0}" + suffix, NotifyOnTargetUpdated = true });
        label.SetBinding(ToolTipProperty, new Binding(nameof(TextBlock.ToolTip)) { Source = source[key] });
        bound.Add((label, key, suffix)); return label;
    }
    private static void Put(Canvas parent, FrameworkElement element, Rect bounds)
    { element.Width = bounds.Width; element.Height = bounds.Height; SetLeft(element, bounds.X); SetTop(element, bounds.Y); parent.Children.Add(element); }
    public void Feed(double? cpuTemperature, double? gpuTemperature, double? cpuClock, double? gpuClock)
    { cpuDial.Update(cpuTemperature); gpuDial.Update(gpuTemperature); cpuGraph.Add(cpuClock); gpuGraph.Add(gpuClock); }
    public void SetMotion(bool enabled)
    {
        motion = enabled;
        shipPortrait?.SetMotion(enabled); projection?.SetMotion(enabled); holotable?.SetMotion(enabled);
    }
    public void SetReadoutEffects(bool pulse, bool smoothGauges)
    {
        readoutPulse=pulse; gaugeTransitions=smoothGauges;
        foreach (var entry in bound) if (entry.Label is HoloReadout reading) reading.SetPulse(pulse);
        cpuDial.SetPulse(pulse); gpuDial.SetPulse(pulse);
        cpuDial.SetMotion(smoothGauges); gpuDial.SetMotion(smoothGauges); holotable?.SetReadoutPulse(pulse);
    }
    internal void OpenHolotable()
    {
        if (holotable is not null || activeTheme is null || !HolotableAvailable) return;
        holotable = new Holotable(activeTheme, source, motion, readoutPulse, CloseHolotable);
        Put(this, holotable, new(8, 8, Width - 16, Height - 36));
        SetZIndex(holotable, 10); holotable.Focus();
    }
    internal void CloseHolotable()
    {
        if (holotable is null) return;
        holotable.SetMotion(false); Children.Remove(holotable); holotable = null;
    }
    internal void CheckHolotable()
    {
        OpenHolotable();
        holotable!.CheckInteraction();
        holotable.SetMotion(motion);
        CheckBindings();
    }
    internal void CheckBindings()
    {
        if (!HolotableAvailable && (holotable is not null || Children.OfType<Button>().Any(b => b.Content?.ToString()?.Contains("HOLOTABLE", StringComparison.Ordinal)==true)))
            throw new InvalidOperationException("A flat blueprint theme exposed a holotable.");
        if (bound.Any(entry => entry.Label.Text != source[entry.Key].Text + entry.Suffix))
            throw new InvalidOperationException("The ship console diverged from the sensor presentation.");
        cpuDial.CheckBindings(); gpuDial.CheckBindings();
        if (!readoutPulse && bound.Any(entry => entry.Label is HoloReadout reading && (reading.Opacity!=1 || reading.HasAnimatedProperties)))
            throw new InvalidOperationException("Disabled readout pulse changed number opacity.");
        holotable?.CheckBindings();
    }
    internal void CheckSeparateEffects()
    {
        bool savedMotion=motion, savedPulse=readoutPulse, savedGauges=gaugeTransitions;
        SetReadoutEffects(false,true); SetMotion(false);
        if (shipPortrait?.MotionActive==true || projection?.MotionActive==true)
            throw new InvalidOperationException("Holographic display motion did not stop independently.");
        CheckBindings();
        SetReadoutEffects(true,false);
        if (motion) throw new InvalidOperationException("Readout effects enabled holographic motion.");
        SetReadoutEffects(false,savedGauges); SetMotion(savedMotion); CheckBindings();
        SetReadoutEffects(savedPulse,savedGauges);
        shipPortrait?.Check();
        projection?.Check();
    }
    internal void SelectShip(string id)
    {
        if (shipChoices is null) throw new InvalidOperationException("Ship selector is unavailable.");
        shipChoices.SelectedItem=shipChoices.Items.OfType<ComboBoxItem>().Single(item=>item.Tag is ShipProfile ship && ship.Id==id);
    }
    internal static void Check()
    {
        foreach (var size in new[] { (1920, 1080), (1024, 600), (800, 800), (1920, 480), (1920, 462), (480, 800), (480, 1920), (3840, 2160) })
        foreach (var mode in new[] { "Auto", "Classic", "Wide", "Portrait" })
        {
            var plan = Calculate(size.Item1 / (double)size.Item2, mode);
            var sections = new[] { plan.Sidebar, plan.Cpu, plan.Gpu, plan.Memory, plan.Network, plan.Board, plan.Navigation };
            var viewport = new Rect(0, 0, plan.Width, plan.Height);
            if (sections.Any(r => !viewport.Contains(r) || r.Width <= 0 || r.Height <= 0)) throw new InvalidOperationException("A ship-console section left the canvas.");
            for (int i = 0; i < sections.Length; i++) for (int j = i + 1; j < sections.Length; j++)
                if (Rect.Intersect(sections[i], sections[j]) is { IsEmpty: false, Width: > 0, Height: > 0 }) throw new InvalidOperationException("Ship-console sections overlap.");
        }
    }
}

internal sealed class ConsoleDial : Canvas
{
    private PanelTheme theme = PanelTheme.Resolve("Rocinante");
    private double? temperature;
    private bool motion = true;
    private static readonly DependencyProperty GaugeValueProperty = DependencyProperty.Register("GaugeValue", typeof(double), typeof(ConsoleDial),
        new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsRender));
    private readonly TextBlock readingSource, maximumSource;
    public ConsoleDial(TextBlock reading, TextBlock maximum)
    {
        readingSource = reading; maximumSource = maximum;
        Width = Height = 160;
        var value = new HoloReadout(theme.Accent, PanelPreferences.ReadoutPulse) { FontFamily = new FontFamily("Segoe UI"), FontSize = 34, FontWeight = FontWeights.SemiBold, TextAlignment = TextAlignment.Center };
        value.SetBinding(TextBlock.TextProperty, new Binding(nameof(TextBlock.Text)) { Source = reading, NotifyOnTargetUpdated = true });
        var max = new TextBlock { FontFamily = new FontFamily("Consolas"), FontSize = 11, TextAlignment = TextAlignment.Center };
        max.SetBinding(TextBlock.TextProperty, new Binding(nameof(TextBlock.Text)) { Source = maximum, StringFormat = "PEAK {0}" });
        SetLeft(value, 10); SetTop(value, 52); value.Width = 140; Children.Add(value);
        SetLeft(max, 8); SetTop(max, 100); max.Width = 144; Children.Add(max);
        IsVisibleChanged += (_, _) => { if (!IsVisible) Settle(); };
        Unloaded += (_, _) => Settle();
    }
    public void ApplyTheme(PanelTheme value)
    { theme = value; ((HoloReadout)Children[0]).SetInk(theme.Accent); ((TextBlock)Children[1]).Foreground = theme.Muted; InvalidateVisual(); }
    public void SetMotion(bool enabled) { motion=enabled; if (!enabled) Settle(); }
    public void SetPulse(bool enabled) => ((HoloReadout)Children[0]).SetPulse(enabled);
    private void Settle() { BeginAnimation(GaugeValueProperty, null); SetValue(GaugeValueProperty, temperature ?? 0); }
    public void Update(double? value)
    {
        double before=(double)GetValue(GaugeValueProperty); temperature=value;
        BeginAnimation(GaugeValueProperty, null); SetValue(GaugeValueProperty, value ?? 0);
        if (!value.HasValue || !motion || !IsVisible || !SystemParameters.ClientAreaAnimation) { InvalidateVisual(); return; }
        var animation=new DoubleAnimation(before, value.Value, TimeSpan.FromMilliseconds(260)) { FillBehavior=FillBehavior.Stop };
        Timeline.SetDesiredFrameRate(animation, 20); BeginAnimation(GaugeValueProperty, animation);
    }
    internal void CheckBindings()
    {
        if (((TextBlock)Children[0]).Text != readingSource.Text || ((TextBlock)Children[1]).Text != "PEAK " + maximumSource.Text)
            throw new InvalidOperationException("A ship-console temperature dial diverged from its source.");
    }
    protected override void OnRender(DrawingContext context)
    {
        base.OnRender(context);
        double fraction = Math.Clamp((double)GetValue(GaugeValueProperty) / 100, 0, 1);
        if (theme.ConsoleStyle is "Mars" or "Landing")
        {
            var octagon = new StreamGeometry();
            using (var path = octagon.Open()) { path.BeginFigure(new Point(38, 15), false, true); path.PolyLineTo(new Point[] { new(122, 15), new(146, 39), new(146, 121), new(122, 145), new(38, 145), new(14, 121), new(14, 39) }, true, false); }
            context.DrawGeometry(null, new Pen(theme.Divider, 3), octagon);
            context.DrawRectangle(theme.Accent, null, new Rect(38, 15, 84 * fraction, 3));
            if (theme.ConsoleStyle == "Landing")
            {
                context.DrawLine(new Pen(theme.Accent, 2), new Point(14, 116), new Point(14, 135));
                context.DrawLine(new Pen(theme.Accent, 2), new Point(146, 116), new Point(146, 135));
            }
        }
        else
        {
            for (int step = 0; step < 40; step++)
            {
                double angle = (135 + step * 270.0 / 39) * Math.PI / 180;
                var outer = new Point(80 + 65 * Math.Cos(angle), 80 + 65 * Math.Sin(angle));
                var inner = new Point(80 + (theme.ConsoleStyle == "Belt" ? 54 : 61) * Math.Cos(angle), 80 + (theme.ConsoleStyle == "Belt" ? 54 : 61) * Math.Sin(angle));
                context.DrawLine(new Pen(temperature.HasValue && step < fraction * 40 ? theme.Accent : theme.Divider, theme.ConsoleStyle == "Belt" ? 3 : 2), inner, outer);
            }
            if (theme.ConsoleStyle is "Earth" or "Race") context.DrawEllipse(null, new Pen(theme.Divider, 1), new Point(80, 80), 73, 73);
            if (theme.ConsoleStyle == "Patrol")
            {
                context.DrawLine(new Pen(theme.Accent, 2), new Point(8, 80), new Point(16, 80));
                context.DrawLine(new Pen(theme.Accent, 2), new Point(144, 80), new Point(152, 80));
            }
        }
    }
}
