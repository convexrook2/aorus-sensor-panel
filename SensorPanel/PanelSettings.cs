using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;

namespace SensorPanel;

// The existing commands supply state and actions; the UI is one direct form.
internal sealed class PanelSettings
{
    public Grid Overlay { get; } = new();
    private readonly Window owner;
    private readonly ContextMenu commands;
    private readonly MenuItem shipCommands;
    private readonly Func<PanelTheme> currentTheme;
    private readonly Border pane = new() { CornerRadius=new(10), BorderThickness=new(1),
        HorizontalAlignment=HorizontalAlignment.Right, VerticalAlignment=VerticalAlignment.Bottom,
        Margin=new(12,12,12,48), Visibility=Visibility.Collapsed };
    private readonly Button gear, close;
    private readonly TextBlock heading = new() { FontSize=17, FontWeight=FontWeights.SemiBold, VerticalAlignment=VerticalAlignment.Center };
    private readonly WrapPanel sections = new();
    private readonly StackPanel footer = new();
    private readonly ScrollViewer scroll;
    private readonly Dictionary<string,ComboBox> selectors = new();
    private MenuItem? shipPage;
    private bool rebuilding;
    private int revision;
    public bool IsOpen => pane.Visibility==Visibility.Visible;
    internal event Action? Closed;
    public bool IsPointerOver => pane.IsMouseOver || gear.IsMouseOver;
    internal Rect Bounds => new(pane.TranslatePoint(new Point(),Overlay),new Size(pane.ActualWidth,pane.ActualHeight));

    public PanelSettings(Window owner, ContextMenu commands, MenuItem shipCommands, Func<PanelTheme> currentTheme)
    {
        this.owner=owner; this.commands=commands; this.shipCommands=shipCommands; this.currentTheme=currentTheme;
        Overlay.Resources.MergedDictionaries.Add(new ResourceDictionary { Source=new Uri("pack://application:,,,/SettingsStyles.xaml") });
        gear=Button("\uE713",Toggle,"Settings");
        gear.FontFamily=new FontFamily("Segoe MDL2 Assets"); gear.FontSize=21;
        gear.Width=gear.Height=32; gear.Padding=new(0); gear.Margin=new(12);
        gear.HorizontalAlignment=HorizontalAlignment.Right; gear.VerticalAlignment=VerticalAlignment.Bottom;
        Overlay.Children.Add(gear);
        var content=new DockPanel { Margin=new(12) };
        var title=new DockPanel { Margin=new(4,0,0,10) };
        close=Button("×",Close,"Close settings"); close.FontSize=20; close.Width=30; close.Padding=new(0);
        DockPanel.SetDock(close,Dock.Right); title.Children.Add(close); title.Children.Add(heading);
        DockPanel.SetDock(title,Dock.Top); content.Children.Add(title);
        DockPanel.SetDock(footer,Dock.Bottom); content.Children.Add(footer);
        scroll=new ScrollViewer { Content=sections, VerticalScrollBarVisibility=ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled };
        content.Children.Add(scroll); pane.Child=content; Overlay.Children.Add(pane);
        Overlay.SizeChanged+=(_,_)=>Resize();
        ApplyTheme();
    }
    public void Open(MenuItem? page=null)
    {
        shipPage=page; pane.Visibility=Visibility.Visible;
        Rebuild(); scroll.ScrollToTop();
    }
    private void Toggle() { if(IsOpen)Close();else Open(); }
    public void Close()
    {
        bool wasOpen=IsOpen;
        pane.Visibility=Visibility.Collapsed;gear.Focus();
        sections.Children.Clear();footer.Children.Clear();selectors.Clear();shipPage=null;
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
        pane.Background=theme.Background; pane.BorderBrush=theme.Divider;
        heading.Foreground=gear.Foreground=close.Foreground=theme.Foreground;
        gear.Background=theme.Surface;
        if(IsOpen)Rebuild();
    }
    private void Resize()
    {
        pane.Width=Math.Min(580,Math.Max(180,Overlay.ActualWidth-24));
        pane.MaxHeight=Math.Max(80,Math.Min(520,Overlay.ActualHeight-72));
        double width=Math.Max(140,pane.Width-40); sections.Width=width;
        foreach(var card in sections.Children.OfType<Border>()) card.Width=card.Tag is true||width<480?width-8:(width-16)/2;
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
        rebuilding=true;
        try
        {
            revision++;
            commands.RaiseEvent(new RoutedEventArgs(ContextMenu.OpenedEvent));
            Refresh(commands.Items);
            Refresh(shipCommands.Items);
            sections.Children.Clear(); footer.Children.Clear(); selectors.Clear();
            heading.Text=shipPage is null ? "Settings" : "Ship options";
            if(shipPage is null)BuildSettings();else BuildShipOptions();
            Resize();
        }
        finally { rebuilding=false; }
    }
    private void BuildSettings()
    {
        var experience=Section("Experience",true);
        Choice(experience,"Experience",Find("Experience"));
        if(currentTheme().ShipConsole)BuildGraphicsChoice(experience);
        else experience.Children.Add(Label("Original AORUS monitoring and colour themes. Ship and solar graphics are inactive.",11));
        var display=Section("Display");
        Choice(display,"Screen",Find("Move to display"));
        Actions(display,("Fullscreen",Find("Fit to display")),("Windowed",Find("Restore window size")));
        Choice(display,"Layout",Find("Layout"));
        Toggle(display,"Always on top",Find("Always on top"));

        var appearance=Section("Appearance");
        Choice(appearance,"Theme",Find("Theme"));
        if(currentTheme().Showcase)BuildCycleControls(appearance);
        Segments(appearance,"Artwork size",Find("Scale artwork"));
        if(currentTheme().ShipConsole)
        {
            Toggle(appearance,"Pulse changed numbers",Find("Pulse changed numbers"));
            Toggle(appearance,"Smooth temperature rings",Find("Smooth temperature rings"));
        }
        else Toggle(appearance,"Animate AORUS artwork",Find("Animate AORUS artwork"));

        var sensors=Section("Sensors");
        Choice(sensors,"Update every",Find("Sensor polling"));
        Choice(sensors,"FPS source",Find("FPS source"));
        var app=Section("App");
        Toggle(app,"Start with Windows",Find("Start with Windows"));
        Actions(app,("Save image",Find("Save panel image")),("Help",Find("Open README")));
        Finish(("Hide to tray",Find("Hide to tray")),("Exit app",Find("Close panel (Alt+F4)")));
    }
    private void BuildShipOptions()
    {
        var motion=Section("Scene",true);
        BuildGraphicsChoice(motion);
        Toggle(motion,"Animate map and background",Find("Map and background motion"));
        var showcase=Find("Expanse showcase");
        var cycle=Section("Ship cycling"); cycle.IsEnabled=showcase.IsEnabled;
        if(!showcase.IsEnabled)cycle.Children.Add(Label("Choose The Expanse theme to cycle ships.",12));
        BuildCycleControls(cycle);
        Actions(cycle,("Previous ship",Find("Previous ship · pause cycle")),("Next ship",Find("Next ship · pause cycle")));
        motion.Children.Add(Label("Scene images"));
        Actions(motion,("Open folder",Find("Open local scene-media folder")),("Reload images",Find("Reload local scene media")));
        motion.Children[motion.Children.Count-1].IsEnabled=showcase.IsEnabled;
        Finish();
    }
    private void BuildCycleControls(StackPanel parent)
    {
        Toggle(parent,"Cycle ship themes",Find("Auto-cycle ships and personnel"));
        Choice(parent,"Change ship every",Find("Scene duration"));
    }
    private void BuildGraphicsChoice(StackPanel parent)
    {
        Choice(parent,"Rendering",Find("Rendering mode"));
        parent.Children.Add(Label("Full Fidelity: unrestricted motion. Efficient: lower GPU use with the same ship detail.",11));
    }
    private StackPanel Section(string title,bool fullWidth=false)
    {
        var body=new StackPanel();
        var caption=Label(title,13); caption.FontWeight=FontWeights.SemiBold; caption.Margin=new(0,0,0,8);
        body.Children.Add(caption);
        sections.Children.Add(new Border { Child=body, Padding=new(10), Margin=new(4,0,4,8), CornerRadius=new(6),
            Background=currentTheme().Surface,Tag=fullWidth });
        return body;
    }
    private TextBlock Label(string text,double size=12) => new() { Text=text,FontSize=size,Foreground=currentTheme().Foreground,
        TextWrapping=TextWrapping.Wrap,Margin=new(0,0,0,4) };
    private static string Caption(MenuItem item,string field)
    {
        string value=item.Header?.ToString() ?? "";
        if(field=="Update every")return value.StartsWith("1000 ms",StringComparison.Ordinal) ? "1 second (default)" : value switch {
            "500 ms"=>"0.5 seconds", "2000 ms"=>"2 seconds", "5000 ms"=>"5 seconds", _=>value };
        if(field=="Screen")return value.Replace(@"\\.\DISPLAY","Display ",StringComparison.Ordinal);
        if(field=="Layout")return value.Split('·')[0].Trim();
        return value;
    }
    private void Choice(StackPanel parent,string name,MenuItem group)
    {
        var row=new Grid { Margin=new(0,0,0,6) };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width=new GridLength(name=="Change ship every" ? 120 : 80) });
        row.ColumnDefinitions.Add(new ColumnDefinition());
        var label=Label(name); label.VerticalAlignment=VerticalAlignment.Center;
        label.TextWrapping=TextWrapping.NoWrap; label.Margin=new(0,0,8,0); row.Children.Add(label);
        var theme=currentTheme();
        var combo=new ComboBox { FontSize=13,MinHeight=30,MaxDropDownHeight=240,
            Background=theme.Background,Foreground=theme.Foreground,BorderBrush=theme.Divider,IsEnabled=group.IsEnabled,ToolTip=name };
        NavigationControls.Apply(combo);
        combo.ItemContainerStyle=(Style)Overlay.Resources["SettingsOptionStyle"];
        AutomationProperties.SetName(combo,name); selectors[name]=combo;
        foreach(var item in group.Items.OfType<MenuItem>().Where(item=>item.Visibility==Visibility.Visible))
        {
            var option=new ComboBoxItem { Content=Caption(item,name),Tag=item,IsEnabled=item.IsEnabled };
            combo.Items.Add(option); if(item.IsChecked)combo.SelectedItem=option;
        }
        if(combo.SelectedItem is null)
        {
            string value=name=="Update every" ? group.Header!.ToString()!.Replace("Sensor polling · ","",StringComparison.Ordinal)+" (custom)"
                : name=="FPS source" ? "Pinned game is not running" : "Select…";
            var current=new ComboBoxItem { Content=value,IsEnabled=false };
            combo.Items.Insert(0,current); combo.SelectedItem=current;
        }
        combo.SelectionChanged+=(_,_)=>
        {
            if(!rebuilding && combo.SelectedItem is ComboBoxItem { Tag:MenuItem item })Execute(item,true);
        };
        Grid.SetColumn(combo,1); row.Children.Add(combo); parent.Children.Add(row);
    }
    private void Toggle(StackPanel parent,string label,MenuItem item)
    {
        var checkbox=new CheckBox { Content=label,IsChecked=item.IsChecked,IsEnabled=item.IsEnabled,Foreground=currentTheme().Foreground,
            FontSize=12,MinHeight=25,VerticalContentAlignment=VerticalAlignment.Center,Margin=new(0,0,0,3) };
        AutomationProperties.SetName(checkbox,label);
        checkbox.Click+=(_,_)=>Execute(item,checkbox.IsChecked==true);
        parent.Children.Add(checkbox);
    }
    private void Segments(StackPanel parent,string label,MenuItem group)
    {
        parent.Children.Add(Label(label));
        Actions(parent,group.Items.OfType<MenuItem>().Select(item=>(item.Header!.ToString()!.Split('·')[0].Trim(),item)).ToArray());
    }
    private void Actions(Panel parent,params (string Text,MenuItem Command)[] actions)
    {
        var row=new Grid { Margin=new(0,0,0,6) };
        for(int index=0;index<actions.Length;index++)
        {
            var action=actions[index]; row.ColumnDefinitions.Add(new ColumnDefinition());
            var button=Button(action.Text,()=>Execute(action.Command,true),action.Text);
            button.IsEnabled=action.Command.IsEnabled; button.MinHeight=30; button.FontSize=12;
            button.Margin=new(index==0 ? 0 : 3,0,index==actions.Length-1 ? 0 : 3,0);
            if(action.Command.IsChecked) { button.Background=currentTheme().Accent; button.Foreground=currentTheme().Background; }
            Grid.SetColumn(button,index); row.Children.Add(button);
        }
        parent.Children.Add(row);
    }
    private void Finish(params (string Text,MenuItem Command)[] actions)
    {
        footer.Children.Add(new Border { Height=1,Background=currentTheme().Divider,Margin=new(4,3,4,8) });
        var row=new DockPanel();
        var done=Button("Done",Close,"Done"); done.MinWidth=72; done.Background=currentTheme().Surface;
        DockPanel.SetDock(done,Dock.Right); row.Children.Add(done);
        var shortcuts=new StackPanel { Orientation=Orientation.Horizontal };
        foreach(var action in actions)
        {
            var button=Button(action.Text,()=>Execute(action.Command),action.Text); button.Margin=new(0,0,6,0);
            shortcuts.Children.Add(button);
        }
        row.Children.Add(shortcuts); footer.Children.Add(row);
        var hint=Label("Changes apply immediately.",11); hint.Foreground=currentTheme().Muted; hint.Margin=new(4,6,0,0);
        footer.Children.Add(hint);
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
            BorderBrush=theme.Divider,Padding=new(8,5,8,5),MinHeight=30,HorizontalContentAlignment=HorizontalAlignment.Center,
            Cursor=System.Windows.Input.Cursors.Hand };
        AutomationProperties.SetName(button,name);
        button.Template=(ControlTemplate)Overlay.Resources["SettingsButtonTemplate"];
        button.Click+=(_,_)=>action();return button;
    }
}
