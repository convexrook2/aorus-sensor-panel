using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace SensorPanel;

// The existing commands supply state and actions for the settings pages.
internal sealed class PanelSettings
{
    private const int AppTabIndex = 3;
    public Grid Overlay { get; } = new() { UseLayoutRounding=true };
    private readonly Window owner;
    private readonly ContextMenu commands;
    private readonly MenuItem shipCommands;
    private readonly Func<PanelTheme> currentTheme;
    private readonly Func<PanelTray?> updater;
    private readonly Border pane = new() { CornerRadius=new(10), BorderThickness=new(1),
        HorizontalAlignment=HorizontalAlignment.Right, VerticalAlignment=VerticalAlignment.Bottom,
        Margin=new(12,12,12,48), Visibility=Visibility.Collapsed };
    private readonly Button gear, close;
    private readonly TextBlock heading = new() { FontSize=17, FontWeight=FontWeights.SemiBold, VerticalAlignment=VerticalAlignment.Center };
    private readonly TabControl tabs = new();
    private readonly ContentControl body = new() { HorizontalContentAlignment=HorizontalAlignment.Stretch, VerticalContentAlignment=VerticalAlignment.Stretch };
    private readonly StackPanel sections = new();
    private readonly ScrollViewer scroll;
    private readonly Dictionary<string,ComboBox> selectors = new();
    private readonly Dictionary<string,Control> controls = new();
    private Button? updateCheck, updateDownload;
    private TextBlock? updateStatus;
    private MenuItem? shipPage;
    private bool rebuilding;
    private int revision;
    public bool IsOpen => pane.Visibility==Visibility.Visible;
    internal event Action? Closed;
    public bool IsPointerOver => pane.IsMouseOver || gear.IsMouseOver;
    internal Rect Bounds => new(pane.TranslatePoint(new Point(),Overlay),new Size(pane.ActualWidth,pane.ActualHeight));

    public PanelSettings(Window owner, ContextMenu commands, MenuItem shipCommands, Func<PanelTheme> currentTheme, Func<PanelTray?> updater)
    {
        this.owner=owner; this.commands=commands; this.shipCommands=shipCommands; this.currentTheme=currentTheme; this.updater=updater;
        Overlay.Resources.MergedDictionaries.Add(new ResourceDictionary { Source=new Uri("pack://application:,,,/SettingsStyles.xaml") });
        gear=Button("\uE713",Toggle,"Settings");
        gear.FontFamily=new FontFamily("Segoe MDL2 Assets"); gear.FontSize=21;
        gear.Width=gear.Height=32; gear.Padding=new(0); gear.Margin=new(12);
        gear.HorizontalAlignment=HorizontalAlignment.Right; gear.VerticalAlignment=VerticalAlignment.Bottom;
        Overlay.Children.Add(gear);
        KeyboardNavigation.SetTabNavigation(pane,KeyboardNavigationMode.Cycle);
        var content=new DockPanel { Margin=new(12) };
        var title=new DockPanel { Margin=new(2,0,0,8) };
        AutomationProperties.SetHeadingLevel(heading, AutomationHeadingLevel.Level1);
        close=Button("×",Close,"Close settings"); close.FontSize=20; close.Width=30; close.Padding=new(0);
        DockPanel.SetDock(close,Dock.Right); title.Children.Add(close); title.Children.Add(heading);
        DockPanel.SetDock(title,Dock.Top); content.Children.Add(title);
        scroll=new ScrollViewer { Content=sections, VerticalScrollBarVisibility=ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled, Focusable=false };
        tabs.Template=(ControlTemplate)Overlay.Resources["SettingsTabsTemplate"];
        tabs.ItemContainerStyle=(Style)Overlay.Resources["SettingsTabStyle"];
        foreach(string page in new[]{"Appearance","Display","Sensors","App"}) tabs.Items.Add(new TabItem { Header=page });
        tabs.SelectedIndex=0;
        tabs.SelectionChanged+=(_,e)=>
        {
            if(e.Source!=tabs || !IsOpen || shipPage is not null)return;
            AttachScroll();
            Rebuild();
            scroll.ScrollToTop();
        };
        content.Children.Add(body); pane.Child=content; Overlay.Children.Add(pane);
        Overlay.SizeChanged+=(_,_)=>Resize();
        ApplyTheme();
    }
    public void Open(MenuItem? page=null)
    {
        shipPage=page;
        if(!IsOpen || page is not null)tabs.SelectedIndex=0;
        pane.Visibility=Visibility.Visible;
        AttachScroll();
        Rebuild(); scroll.ScrollToTop();
        if(body.Content==tabs && tabs.SelectedItem is TabItem tab)tab.Focus();
        else close.Focus();
    }
    internal void OpenUpdates()
    {
        if(!IsOpen || shipPage is not null)Open();
        tabs.SelectedIndex=AppTabIndex;
        ((TabItem)tabs.SelectedItem).Focus();
    }
    private void AttachScroll()
    {
        if(ReferenceEquals(body.Content,scroll))body.Content=null;
        var selected=shipPage is null ? (TabItem)tabs.SelectedItem : null;
        foreach(var tab in tabs.Items.OfType<TabItem>())
            if(tab!=selected)tab.Content=null;
        if(selected is null)body.Content=scroll;
        else
        {
            selected.Content=scroll;
            body.Content=tabs;
        }
    }
    private void Toggle() { if(IsOpen)Close();else Open(); }
    public void Close()
    {
        bool wasOpen=IsOpen;
        pane.Visibility=Visibility.Collapsed;gear.Focus();
        ClearControls();shipPage=null;
        if(wasOpen)Closed?.Invoke();
    }
    public void ApplyTheme()
    {
        var theme=currentTheme();
        Overlay.Resources["SettingsHoverSurface"]=theme.Surface;
        Overlay.Resources["SettingsScrollThumb"]=theme.Divider;
        Overlay.Resources["SettingsFocus"]=theme.Accent;
        Overlay.Resources["SettingsOptionBackground"]=theme.Background;
        Overlay.Resources["SettingsOptionForeground"]=theme.Foreground;
        Overlay.Resources["SettingsMuted"]=theme.Muted;
        Overlay.Resources["SettingsForeground"]=theme.Foreground;
        Overlay.Resources["SettingsDivider"]=theme.Divider;
        pane.Background=theme.Background; pane.BorderBrush=theme.Divider;
        heading.Foreground=gear.Foreground=close.Foreground=theme.Foreground;
        gear.Background=theme.Surface;
        if(IsOpen)Rebuild();
    }
    private void Resize()
    {
        pane.Width=Math.Min(440,Math.Max(0,Overlay.ActualWidth-24));
        pane.Height=Math.Max(0,Math.Min(448,Overlay.ActualHeight-60));
    }
    private static IEnumerable<MenuItem> Items(ItemCollection items)
    {
        foreach(var item in items.OfType<MenuItem>())
        {
            yield return item;
            foreach(var child in Items(item.Items))yield return child;
        }
    }
    private MenuItem Find(string name) => Items(commands.Items).Concat(Items(shipCommands.Items))
        .First(item=>item.Header?.ToString()==name || name=="Sensor polling" && item.Header?.ToString()?.StartsWith("Sensor polling",StringComparison.Ordinal)==true);
    private static void Refresh(ItemCollection items)
    {
        foreach(var item in items.OfType<MenuItem>())
        {
            if(item.HasItems)item.RaiseEvent(new RoutedEventArgs(MenuItem.SubmenuOpenedEvent));
            Refresh(item.Items);
        }
    }
    private void Rebuild()
    {
        if(rebuilding)return;
        string? focus=Keyboard.FocusedElement is Control element && controls.ContainsValue(element)
            ? AutomationProperties.GetName(element) : null;
        rebuilding=true;
        try
        {
            revision++;
            commands.RaiseEvent(new RoutedEventArgs(ContextMenu.OpenedEvent));
            Refresh(commands.Items);
            Refresh(shipCommands.Items);
            ClearControls();
            heading.Text=shipPage is null ? "Settings" : "Ship options";
            if(shipPage is null)BuildSettings();else BuildShipOptions();
            Resize();
        }
        finally { rebuilding=false; }
        if(focus is not null && controls.TryGetValue(focus,out var replacement) && replacement.IsEnabled)replacement.Focus();
    }
    private void ClearControls()
    {
        sections.Children.Clear(); selectors.Clear(); controls.Clear();
        updateCheck=updateDownload=null;updateStatus=null;
    }
    private void BuildSettings()
    {
        switch(tabs.SelectedIndex)
        {
            case 0: BuildAppearance(); break;
            case 1: BuildDisplay(); break;
            case 2: BuildSensors(); break;
            case AppTabIndex: BuildApp(); break;
        }
    }
    private void BuildAppearance()
    {
        var appearance=Section("Experience and theme");
        Choice(appearance,"Experience",Find("Experience"));
        if(currentTheme().ShipConsole)BuildGraphicsChoice(appearance);
        Choice(appearance,"Theme",Find("Theme"));
        if(currentTheme().Showcase)BuildCycleControls(appearance);

        var effects=Section("Visual effects");
        if(currentTheme().ShipConsole)
        {
            Toggle(effects,"Pulse changed numbers",Find("Pulse changed numbers"));
            Toggle(effects,"Smooth temperature rings",Find("Smooth temperature rings"));
        }
        else Toggle(effects,"Animate AORUS artwork",Find("Animate AORUS artwork"));
    }
    private void BuildDisplay()
    {
        var display=Section("Placement");
        Choice(display,"Screen",Find("Move to display"));
        Choice(display,"Layout",Find("Layout"));
        Row(display,"Window mode",ActionButtons(("Fullscreen",Find("Fit to display")),("Windowed",Find("Restore window size"))));
        Toggle(display,"Always on top",Find("Always on top"));

        var scaling=Section("Scaling");
        Segments(scaling,"Artwork size",Find("Scale artwork"));
    }
    private void BuildSensors()
    {
        var sensors=Section("Monitoring");
        Choice(sensors,"Read sensors every",Find("Sensor polling"));
        Choice(sensors,"FPS source",Find("FPS source"));

        var registrations=Find("FPS source").Items.OfType<MenuItem>().Where(item=>!item.IsCheckable).ToList();
        if(registrations.Count==0)return;
        var games=Section("Game detection");
        foreach(var item in registrations)
            if(item.HasItems)Choice(games,"Remove registered game",item);
            else Actions(games,("Add game executable…",item));
    }
    private void BuildApp()
    {
        var updates=Section("Updates");
        updateCheck=Button("Check for updates",async ()=>
        {
            if(updater() is { } tray)await tray.CheckForUpdatesAsync();
        },"Check for updates");
        Row(updates,$"Version {PanelUpdateService.Label(PanelUpdateService.CurrentVersion)}",updateCheck);
        updateStatus=Label("");
        updateStatus.Foreground=currentTheme().Muted;
        AutomationProperties.SetLiveSetting(updateStatus,AutomationLiveSetting.Polite);
        updates.Children.Add(updateStatus);
        updateDownload=Button("Open release page",()=>updater()?.OpenUpdate(),"Open release page");
        updateDownload.HorizontalAlignment=HorizontalAlignment.Right;
        updates.Children.Add(updateDownload);
        RefreshUpdater();

        var app=Section("Application");
        Toggle(app,"Start with Windows",Find("Start with Windows"));
        Actions(app,("Save panel image",Find("Save panel image")),("Help",Find("Open README")));
        Actions(app,("Hide to tray",Find("Hide to tray")),("Exit app",Find("Close panel (Alt+F4)")));
    }
    internal void RefreshUpdater()
    {
        if(updateCheck is null || updateStatus is null || updateDownload is null)return;
        var tray=updater();
        updateCheck.IsEnabled=tray?.IsCheckingForUpdates==false;
        var status=tray?.UpdateStatus ?? "Update checks are disabled in preview.";
        bool changed=updateStatus.Text!=status;
        updateStatus.Text=status;
        updateStatus.Visibility=status.Length==0 ? Visibility.Collapsed : Visibility.Visible;
        updateDownload.Visibility=tray?.AvailableVersion is null ? Visibility.Collapsed : Visibility.Visible;
        if(changed)UIElementAutomationPeer.FromElement(updateStatus)?.RaiseAutomationEvent(AutomationEvents.LiveRegionChanged);
    }
    private void BuildShipOptions()
    {
        var motion=Section("Scene");
        BuildGraphicsChoice(motion);
        Toggle(motion,"Animate map and background",Find("Map and background motion"));
        var showcase=Find("Expanse showcase");
        var cycle=Section("Ship cycling"); cycle.IsEnabled=showcase.IsEnabled;
        if(!showcase.IsEnabled)cycle.Children.Add(Label("Choose The Expanse theme to cycle ships.",12));
        BuildCycleControls(cycle);
        Actions(cycle,("Previous ship",Find("Previous ship · pause cycle")),("Next ship",Find("Next ship · pause cycle")));
        var images=Section("Scene images"); images.IsEnabled=showcase.IsEnabled;
        Actions(images,("Open folder",Find("Open local scene-media folder")),("Reload images",Find("Reload local scene media")));
    }
    private void BuildCycleControls(StackPanel parent)
    {
        Toggle(parent,"Cycle ship themes",Find("Auto-cycle ships and personnel"));
        Choice(parent,"Change ship every",Find("Scene duration"));
    }
    private void BuildGraphicsChoice(StackPanel parent)
    {
        Choice(parent,"Rendering",Find("Rendering mode"));
        var summary=Label("Efficient reduces motion updates while keeping full ship detail.",11);
        summary.Foreground=currentTheme().Muted;
        parent.Children.Add(summary);
    }
    private StackPanel Section(string title)
    {
        var group=new StackPanel { Margin=new(2,0,8,0) };
        if(sections.Children.Count>0)
            sections.Children.Add(new Border { Height=1,Background=currentTheme().Divider,Margin=new(2,6,8,10) });
        var caption=Label(title,13); caption.FontWeight=FontWeights.SemiBold; caption.Margin=new(0,0,0,6);
        AutomationProperties.SetHeadingLevel(caption,AutomationHeadingLevel.Level2);
        group.Children.Add(caption);
        sections.Children.Add(group);
        return group;
    }
    private TextBlock Label(string text,double size=12) => new() { Text=text,FontSize=size,Foreground=currentTheme().Foreground,
        TextWrapping=TextWrapping.Wrap,Margin=new(0,0,0,4) };
    private static string Caption(MenuItem item,string field)
    {
        string value=item.Header?.ToString() ?? "";
        if(field=="Read sensors every")return value.StartsWith("1000 ms",StringComparison.Ordinal) ? "1 second (default)" : value switch {
            "500 ms"=>"0.5 seconds", "2000 ms"=>"2 seconds", "5000 ms"=>"5 seconds", _=>value };
        if(field=="Screen")return value.Replace(@"\\.\DISPLAY","Display ",StringComparison.Ordinal);
        if(field=="Layout")return value.Split('·')[0].Trim();
        return value;
    }
    private void Choice(StackPanel parent,string name,MenuItem group)
    {
        var theme=currentTheme();
        var combo=new ComboBox { FontSize=13,MinHeight=30,MaxDropDownHeight=240,
            Background=theme.Background,Foreground=theme.Foreground,BorderBrush=theme.Divider,IsEnabled=group.IsEnabled,ToolTip=name };
        NavigationControls.Apply(combo);
        combo.ItemContainerStyle=(Style)Overlay.Resources["SettingsOptionStyle"];
        AutomationProperties.SetName(combo,name); selectors[name]=combo;
        foreach(var item in group.Items.OfType<MenuItem>().Where(item=>item.Visibility==Visibility.Visible && (name!="FPS source" || item.IsCheckable)))
        {
            var option=new ComboBoxItem { Content=Caption(item,name),Tag=item,IsEnabled=item.IsEnabled };
            combo.Items.Add(option); if(item.IsChecked)combo.SelectedItem=option;
        }
        if(combo.SelectedItem is null)
        {
            string value=name=="Read sensors every" ? group.Header!.ToString()!.Replace("Sensor polling · ","",StringComparison.Ordinal)+" (custom)"
                : name=="FPS source" ? "Pinned game is not running" : "Select…";
            var current=new ComboBoxItem { Content=value,IsEnabled=false };
            combo.Items.Insert(0,current); combo.SelectedItem=current;
        }
        combo.SelectionChanged+=(_,_)=>
        {
            if(!rebuilding && combo.SelectedItem is ComboBoxItem { Tag:MenuItem item })Execute(item,true);
        };
        controls[name]=combo;
        Row(parent,name,combo);
    }
    private void Row(StackPanel parent,string name,FrameworkElement control)
    {
        var row=new Grid { Margin=new(0,0,0,6) };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width=new GridLength(2,GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width=new GridLength(3,GridUnitType.Star) });
        var label=Label(name); label.VerticalAlignment=VerticalAlignment.Center;
        label.Margin=new(0,0,8,0); row.Children.Add(label);
        AutomationProperties.SetLabeledBy(control,label);
        Grid.SetColumn(control,1); row.Children.Add(control); parent.Children.Add(row);
    }
    private void Toggle(StackPanel parent,string label,MenuItem item)
    {
        var text=Label(label); text.Margin=new(0);
        var checkbox=new CheckBox { Content=text,IsChecked=item.IsChecked,IsEnabled=item.IsEnabled,Foreground=currentTheme().Foreground,
            FontSize=12,MinHeight=30,VerticalContentAlignment=VerticalAlignment.Center,Margin=new(0,0,0,2) };
        AutomationProperties.SetName(checkbox,label);
        controls[label]=checkbox;
        checkbox.Click+=(_,_)=>Execute(item,checkbox.IsChecked==true);
        parent.Children.Add(checkbox);
    }
    private void Segments(StackPanel parent,string label,MenuItem group)
    {
        Row(parent,label,ActionButtons(group.Items.OfType<MenuItem>().Select(item=>(item.Header!.ToString()!.Split('·')[0].Trim(),item)).ToArray()));
    }
    private void Actions(Panel parent,params (string Text,MenuItem Command)[] actions)
        => parent.Children.Add(ActionButtons(actions));
    private Grid ActionButtons(params (string Text,MenuItem Command)[] actions)
    {
        var row=new Grid();
        for(int index=0;index<actions.Length;index++)
        {
            var action=actions[index]; row.ColumnDefinitions.Add(new ColumnDefinition());
            var button=Button(action.Text,()=>Execute(action.Command,action.Command.IsCheckable ? true : null),action.Text);
            button.IsEnabled=action.Command.IsEnabled; button.MinHeight=30; button.FontSize=12;
            button.Margin=new(index==0 ? 0 : 3,0,index==actions.Length-1 ? 0 : 3,0);
            if(action.Command.IsChecked) { button.Background=currentTheme().Accent; button.Foreground=currentTheme().Background; }
            Grid.SetColumn(button,index); row.Children.Add(button);
        }
        row.Margin=new(0,0,0,4);
        return row;
    }
    private void Execute(MenuItem item,bool? value=null)
    {
        if(!item.IsEnabled)return;
        int before=revision;
        if(item.IsCheckable)item.IsChecked=value ?? !item.IsChecked;
        item.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
        if(!owner.IsVisible) { Close();return; }
        if(IsOpen && before==revision)Rebuild();
    }
    internal void FocusChoice(string control)
    {
        if(selectors.TryGetValue(control,out var selector)) { selector.BringIntoView(); selector.Focus(); }
    }
    internal void SelectChoice(string control,string option)
    {
        // Native reviews can lose activation while WebView shutdown completes.
        // Reopen the same user-facing settings flow before selecting a control.
        if(!IsOpen)Open();
        var selector=selectors[control];
        selector.SelectedItem=selector.Items.OfType<ComboBoxItem>().Single(item=>item.Tag is MenuItem command && command.Header?.ToString()==option);
    }
    internal async Task CheckPopupThemeAsync()
    {
        var selector=selectors["Theme"];
        selector.IsDropDownOpen=true;
        try
        {
            await Task.Delay(250);
            var theme=currentTheme();
            foreach(var item in selector.Items.OfType<ComboBoxItem>())
                if(item.Background is not SolidColorBrush background||background.Color!=theme.Background.Color||
                    item.Foreground is not SolidColorBrush foreground||foreground.Color!=theme.Foreground.Color)
                    throw new InvalidOperationException("A settings popup lost its theme colours.");
        }
        finally{selector.IsDropDownOpen=false;}
    }
    internal void CheckExperienceControls(bool expanse)
    {
        if(!IsOpen||!selectors.ContainsKey("Experience")||selectors.ContainsKey("Rendering")!=expanse)
            throw new InvalidOperationException("Experience/rendering controls differ from the current experience.");
        foreach(string name in expanse?new[]{"Experience","Rendering"}:new[]{"Experience"})
        {
            var control=selectors[name];var rectangle=control.TransformToAncestor(Overlay).TransformBounds(new Rect(0,0,control.ActualWidth,control.ActualHeight));
            var centre=new Point(rectangle.X+rectangle.Width/2,rectangle.Y+rectangle.Height/2);
            var hit=Overlay.InputHitTest(centre) as DependencyObject;
            if(!control.IsVisible||rectangle.Height<25||!Bounds.Contains(rectangle)||hit is null||(hit!=control&&!control.IsAncestorOf(hit)))
                throw new InvalidOperationException("Experience control is clipped or covered: "+name);
        }
    }
    private Button Button(object content,Action action,string name)
    {
        var theme=currentTheme();
        var button=new Button { Content=content,Background=Brushes.Transparent,Foreground=theme.Foreground,BorderThickness=new(1),
            BorderBrush=theme.Divider,Padding=new(6,3,6,3),MinHeight=30,HorizontalContentAlignment=HorizontalAlignment.Center,
            Cursor=Cursors.Hand };
        AutomationProperties.SetName(button,name);
        button.Template=(ControlTemplate)Overlay.Resources["SettingsButtonTemplate"];
        controls[name]=button;
        button.Click+=(_,_)=>action();return button;
    }
}
