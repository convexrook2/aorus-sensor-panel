using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;

namespace SensorPanel;

// Both the gear and right-click use the same command model as the original
// context menu. Nested choices stay inside this compact overlay.
internal sealed class PanelSettings
{
    public Grid Overlay { get; } = new();
    private readonly Window owner;
    private readonly ContextMenu commands;
    private readonly Func<PanelTheme> currentTheme;
    private readonly Border pane = new() { CornerRadius = new CornerRadius(10), BorderThickness = new Thickness(1),
        HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Bottom, Margin = new Thickness(12, 12, 12, 48), Visibility = Visibility.Collapsed };
    private readonly Button gear;
    private readonly StackPanel rows = new();
    private readonly TextBlock heading = new() { FontSize = 15, FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center };
    private readonly Button back, close;
    private readonly Stack<MenuItem> navigation = new();
    private readonly VisualBrush backdrop;
    private readonly Border tint = new();
    private Rect lastViewbox;
    public bool IsOpen => pane.Visibility == Visibility.Visible;
    public bool IsPointerOver => pane.IsMouseOver || gear.IsMouseOver;
    internal Rect Bounds => new(pane.TranslatePoint(new Point(), Overlay), new Size(pane.ActualWidth, pane.ActualHeight));
    internal void SelectVisibleOption(string name)
    {
        var button = rows.Children.OfType<Button>().Single(b => AutomationProperties.GetName(b) == name);
        button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    }
    internal void NavigateBack() => back.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

    public PanelSettings(Window owner, FrameworkElement panel, ContextMenu commands, Func<PanelTheme> currentTheme)
    {
        this.owner = owner; this.commands = commands; this.currentTheme = currentTheme;
        Overlay.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("pack://application:,,,/SettingsStyles.xaml") });
        // An empty Grid background lets clicks outside the controls reach the panel.
        gear = MakeButton("\uE713", Toggle);
        gear.FontFamily = new FontFamily("Segoe MDL2 Assets"); gear.FontSize = 21;
        gear.Width = gear.Height = 32; gear.Padding = new Thickness(0);
        gear.HorizontalContentAlignment = HorizontalAlignment.Center;
        gear.HorizontalAlignment = HorizontalAlignment.Right; gear.VerticalAlignment = VerticalAlignment.Bottom;
        gear.Margin = new Thickness(12); gear.ToolTip = "Settings";
        AutomationProperties.SetName(gear, "Settings");
        Overlay.Children.Add(gear);

        var surface = new Grid { ClipToBounds = true };
        surface.SizeChanged += (_, _) => surface.Clip = new RectangleGeometry(new Rect(surface.RenderSize), 9, 9);
        backdrop = new VisualBrush(panel) { ViewboxUnits = BrushMappingMode.Absolute, Stretch = Stretch.Fill };
        surface.Children.Add(new Rectangle { Fill = backdrop, Effect = new BlurEffect { Radius = 14, RenderingBias = RenderingBias.Performance } });
        surface.Children.Add(tint);
        var content = new DockPanel { Margin = new Thickness(10) };
        var title = new Grid { Margin = new Thickness(0, 0, 0, 7) };
        title.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(26) });
        title.ColumnDefinitions.Add(new ColumnDefinition());
        title.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(26) });
        back = MakeButton("‹", GoBack); back.FontSize = 21; back.Padding = new Thickness(0);
        close = MakeButton("×", Close); close.FontSize = 20; close.Padding = new Thickness(0); close.ToolTip = "Close settings";
        back.HorizontalContentAlignment = close.HorizontalContentAlignment = HorizontalAlignment.Center;
        AutomationProperties.SetName(back, "Back"); AutomationProperties.SetName(close, "Close settings");
        Grid.SetColumn(heading, 1); Grid.SetColumn(close, 2);
        title.Children.Add(back); title.Children.Add(heading); title.Children.Add(close);
        DockPanel.SetDock(title, Dock.Top); content.Children.Add(title);
        var scroll = new ScrollViewer { Content = rows, VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
        content.Children.Add(scroll);
        surface.Children.Add(content); pane.Child = surface; Overlay.Children.Add(pane);
        Overlay.SizeChanged += (_, _) => Resize();
        pane.LayoutUpdated += (_, _) => UpdateBackdrop();
        ApplyTheme();
    }

    public void Open()
    {
        commands.RaiseEvent(new RoutedEventArgs(ContextMenu.OpenedEvent));
        navigation.Clear(); pane.Visibility = Visibility.Visible;
        Resize(); Rebuild();
    }
    private void Toggle() { if (IsOpen) Close(); else Open(); }
    public void Close() { pane.Visibility = Visibility.Collapsed; gear.Focus(); }
    private void GoBack() { if (navigation.Count > 0) navigation.Pop(); Rebuild(); }

    private void Resize()
    {
        pane.Width = Math.Min(320, Math.Max(180, Overlay.ActualWidth - 24));
        pane.MaxHeight = Math.Max(80, Math.Min(500, Overlay.ActualHeight - 72));
    }
    private void UpdateBackdrop()
    {
        if (!IsOpen || pane.ActualWidth <= 0 || pane.ActualHeight <= 0) return;
        var origin = pane.TranslatePoint(new Point(), Overlay);
        var rectangle = new Rect(origin, new Size(pane.ActualWidth, pane.ActualHeight));
        if (rectangle == lastViewbox) return;
        lastViewbox = rectangle; backdrop.Viewbox = rectangle;
    }
    public void ApplyTheme()
    {
        var theme = currentTheme();
        Overlay.Resources["SettingsHoverSurface"] = theme.Surface;
        Overlay.Resources["SettingsScrollThumb"] = theme.Divider;
        pane.BorderBrush = theme.Divider;
        var color = theme.Background.Color; color.A = 220;
        tint.Background = new SolidColorBrush(color);
        heading.Foreground = gear.Foreground = back.Foreground = close.Foreground = theme.Foreground;
        gear.Background = theme.Surface;
        if (IsOpen) Rebuild();
    }
    private void Rebuild()
    {
        var theme = currentTheme();
        rows.Children.Clear();
        var page = navigation.Count > 0 ? navigation.Peek() : null;
        page?.RaiseEvent(new RoutedEventArgs(MenuItem.SubmenuOpenedEvent));
        heading.Text = page?.Header?.ToString() ?? "Settings";
        back.Visibility = page is null ? Visibility.Hidden : Visibility.Visible;
        var items = page?.Items ?? commands.Items;
        foreach (var entry in items)
        {
            if (entry is Separator)
            {
                rows.Children.Add(new Border { Height = 1, Background = theme.Divider, Margin = new Thickness(5, 5, 5, 5) });
                continue;
            }
            if (entry is not MenuItem item) continue;
            if (item.HasItems) item.RaiseEvent(new RoutedEventArgs(MenuItem.SubmenuOpenedEvent));
            var content = new Grid();
            content.ColumnDefinitions.Add(new ColumnDefinition());
            content.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(20) });
            var label = new TextBlock { Text = item.Header?.ToString(), TextTrimming = TextTrimming.CharacterEllipsis,
                VerticalAlignment = VerticalAlignment.Center };
            var previewTheme = page?.Header?.ToString() == "Theme"
                ? PanelTheme.All.FirstOrDefault(t => t.Name == item.Header?.ToString()) : null;
            if (previewTheme is not null)
            {
                var chips = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(0, 0, 9, 0) };
                foreach (var brush in new[] { previewTheme.Background, previewTheme.Accent, previewTheme.SecondaryAccent ?? previewTheme.Foreground })
                    chips.Children.Add(new Border { Background = brush, Width = 9, Height = 16, BorderBrush = previewTheme.Divider,
                        BorderThickness = new Thickness(0.5), CornerRadius = new CornerRadius(2), Margin = new Thickness(0, 0, 2, 0) });
                var preview = new DockPanel(); DockPanel.SetDock(chips, Dock.Left);
                preview.Children.Add(chips); preview.Children.Add(label); content.Children.Add(preview);
            }
            else content.Children.Add(label);
            var marker = new TextBlock { Text = item.HasItems ? "›" : item.IsChecked ? "✓" : "",
                Foreground = theme.Accent, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(marker, 1); content.Children.Add(marker);
            var button = MakeButton(content, () => Invoke(item));
            button.Foreground = theme.Foreground; button.MinHeight = 29; button.FontSize = 13;
            button.ToolTip = item.Header?.ToString(); button.IsEnabled = item.IsEnabled;
            AutomationProperties.SetName(button, item.Header?.ToString() ?? "Setting");
            if (item.IsChecked) button.Background = theme.Surface;
            rows.Children.Add(button);
            if (previewTheme is not null && item.IsChecked)
                button.Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Loaded, new Action(() => button.BringIntoView()));
        }
    }
    private void Invoke(MenuItem item)
    {
        if (item.HasItems)
        {
            item.RaiseEvent(new RoutedEventArgs(MenuItem.SubmenuOpenedEvent));
            navigation.Push(item); Rebuild(); return;
        }
        if (item.IsCheckable) item.IsChecked = !item.IsChecked;
        item.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
        if (!owner.IsVisible) { Close(); return; }
        Rebuild();
    }
    private Button MakeButton(object content, Action action)
    {
        var theme = currentTheme();
        var button = new Button { Content = content, Background = Brushes.Transparent, Foreground = theme.Foreground,
            BorderThickness = new Thickness(0), Padding = new Thickness(9, 5, 9, 5), HorizontalContentAlignment = HorizontalAlignment.Stretch,
            Cursor = System.Windows.Input.Cursors.Hand };
        var border = new FrameworkElementFactory(typeof(Border));
        border.SetValue(Border.CornerRadiusProperty, new CornerRadius(5));
        border.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Control.BackgroundProperty));
        border.SetValue(Border.PaddingProperty, new TemplateBindingExtension(Control.PaddingProperty));
        var presenter = new FrameworkElementFactory(typeof(ContentPresenter));
        presenter.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
        presenter.SetValue(ContentPresenter.HorizontalAlignmentProperty, new TemplateBindingExtension(Control.HorizontalContentAlignmentProperty));
        border.AppendChild(presenter);
        var template = new ControlTemplate(typeof(Button)) { VisualTree = border };
        var hover = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true };
        hover.Setters.Add(new Setter(Control.BackgroundProperty, new DynamicResourceExtension("SettingsHoverSurface"))); template.Triggers.Add(hover);
        button.Template = template; button.Click += (_, _) => action(); return button;
    }
}
