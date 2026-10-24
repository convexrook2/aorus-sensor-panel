using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Controls.Primitives;
using System.Windows.Markup;

namespace SensorPanel;

internal sealed class Holotable : Grid
{
    private readonly FleetViewport viewport;
    private readonly Grid stage;
    private readonly Action close;
    private readonly System.Windows.Controls.Primitives.ToggleButton rotation;
    private readonly System.Windows.Controls.Primitives.ToggleButton hologram;
    private bool active=true;
    private readonly List<HoloReadout> readouts = new();
    private readonly List<(HoloReadout Reading, TextBlock Source)> bindings = new();
    internal bool MotionActive => viewport.MotionActive;

    public Holotable(PanelTheme theme, Dictionary<string, TextBlock> source,
        bool readoutPulse, Action close, FleetViewport viewport)
    {
        this.viewport=viewport;this.close=close;
        Focusable=true;ClipToBounds=true;
        Background=new SolidColorBrush(Color.FromRgb(4,12,19));
        RowDefinitions.Add(new(){Height=GridLength.Auto});
        RowDefinitions.Add(new(){Height=new GridLength(1,GridUnitType.Star)});
        RowDefinitions.Add(new(){Height=GridLength.Auto});
        if(!Supports(theme.ShipId))throw new InvalidOperationException("This ship has no 3D holotable model.");
        var backdrop=new ExpanseBackdrop();backdrop.SetMotion(false);backdrop.Show(theme.ShipId!);
        SetRowSpan(backdrop,3);Children.Add(backdrop);
        if(theme.ConsoleStyle=="Pella")
        {
            var grid=new PellaGrid(theme.Accent.Color){Opacity=.28};
            SetRowSpan(grid,3);Children.Add(grid);
        }
        var header=new DockPanel { Margin=new Thickness(30,22,30,0) };
        var back=Command("BACK TO MONITOR  ×",theme,close);
        back.VerticalAlignment=VerticalAlignment.Top;DockPanel.SetDock(back,Dock.Right);header.Children.Add(back);
        var identity=new StackPanel();
        identity.Children.Add(new TextBlock { Text=theme.Vessel!.Faction+"  /  HOLOTABLE",FontSize=11,
            FontFamily=new FontFamily("Bahnschrift"),Foreground=theme.Accent });
        identity.Children.Add(new TextBlock { Text=theme.Vessel.Name,FontSize=32,
            FontFamily=new FontFamily("Bahnschrift SemiCondensed"),Foreground=theme.Foreground });
        header.Children.Add(identity);Children.Add(header);

        // Give the ship the full central stage. A one-sided instrument rail
        // displaced the viewport's centre to the left of the holotable.
        stage=new Grid();
        viewport.SetHolotableMode(true,theme.Accent.Color);viewport.SetMotion(false);viewport.CloseRequested+=close;
        stage.Children.Add(viewport);SetRow(stage,1);Children.Add(stage);
        var footer=new StackPanel { Margin=new Thickness(30,0,30,20),Background=new SolidColorBrush(Color.FromArgb(170,4,12,19)) };
        var controls=new StackPanel { Orientation=Orientation.Horizontal,Margin=new Thickness(0,0,0,8) };
        rotation=new ToggleButton { Content="AUTO ROTATE · OFF",IsChecked=false,Style=ControlStyle(theme,true),MinWidth=145,
            Padding=new Thickness(12,7,12,7),FontSize=11 };
        rotation.Checked+=(_,_)=>{rotation.Content="AUTO ROTATE · ON";viewport.SetMotion(active);};
        rotation.Unchecked+=(_,_)=>{rotation.Content="AUTO ROTATE · OFF";viewport.SetMotion(false);};
        controls.Children.Add(rotation);controls.Children.Add(Command("RESET VIEW",theme,viewport.Reset));
        hologram=new ToggleButton { Content="HOLOGRAM MOTION · ON",IsChecked=true,Style=ControlStyle(theme,true),MinWidth=185,
            Padding=new Thickness(12,7,12,7),FontSize=11,Margin=new Thickness(5,0,0,0) };
        hologram.Checked+=(_,_)=>{hologram.Content="HOLOGRAM MOTION · ON";viewport.SetHologramMotion(active);};
        hologram.Unchecked+=(_,_)=>{hologram.Content="HOLOGRAM MOTION · OFF";viewport.SetHologramMotion(false);};
        controls.Children.Add(hologram);viewport.SetHologramMotion(true);
        footer.Children.Add(controls);
        var readings=new System.Windows.Controls.Primitives.UniformGrid { Columns=3 };
        foreach(var item in new[]{("CPU","cpuTemperature"),("GPU","gpuTemperature"),("FPS","fps"),("CPU POWER","cpuPower"),("GPU POWER","gpuPower"),("MEMORY","memory")})
        {
            var cell=new StackPanel { Margin=new Thickness(6,3,6,5) };
            cell.Children.Add(new TextBlock { Text=item.Item1,FontSize=10,Foreground=theme.Muted });
            var reading=new HoloReadout(theme.Foreground,readoutPulse)
                { FontSize=21,FontFamily=new FontFamily("Bahnschrift SemiCondensed") };
            reading.SetBinding(TextBlock.TextProperty,new Binding(nameof(TextBlock.Text)){Source=source[item.Item2],NotifyOnTargetUpdated=true});
            readouts.Add(reading);bindings.Add((reading,source[item.Item2]));cell.Children.Add(reading);readings.Children.Add(cell);
        }
        footer.Children.Add(readings);
        footer.Children.Add(new TextBlock { Text="DRAG TO TURN SHIP · WHEEL TO ZOOM SHIP · HOME TO RESET · ESC TO RETURN\nMODEL / "+ModelCredit(theme.ShipId!),
            FontSize=9,Foreground=theme.Muted,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(6,9,0,0) });
        SetRow(footer,2);Children.Add(footer);
        bool? previousPortrait=null;
        SizeChanged+=(_,_)=>
        {
            bool portrait=ActualWidth<ActualHeight;if(previousPortrait==portrait)return;previousPortrait=portrait;
            readings.Columns=portrait?3:6;
        };
        KeyDown+=(_,e)=>{if(e.Key==Key.Escape){close();e.Handled=true;}};
    }

    internal static Button Command(string title,PanelTheme theme,Action action)
    {
        var button=new Button { Content=title,Style=ControlStyle(theme,false),
            Padding=new Thickness(12,7,12,7),Margin=new Thickness(5,0,0,0),FontSize=11 };
        button.Click+=(_,e)=>{e.Handled=true;action();};return button;
    }
    private static readonly ControlTemplate controlChrome=(ControlTemplate)XamlReader.Parse("""
        <ControlTemplate xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
            xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
            xmlns:p="clr-namespace:System.Windows.Controls.Primitives;assembly=PresentationFramework" TargetType="{x:Type p:ButtonBase}">
          <Border x:Name="Chrome" Background="{TemplateBinding Background}" BorderBrush="{TemplateBinding BorderBrush}"
              BorderThickness="{TemplateBinding BorderThickness}" CornerRadius="4" SnapsToDevicePixels="True">
            <ContentPresenter Margin="{TemplateBinding Padding}" HorizontalAlignment="Center" VerticalAlignment="Center"
                RecognizesAccessKey="True" TextElement.Foreground="{TemplateBinding Foreground}" />
          </Border>
        </ControlTemplate>
        """);
    private static Style ControlStyle(PanelTheme theme,bool toggle)
    {
        var style=new Style(toggle?typeof(ToggleButton):typeof(Button));
        SolidColorBrush Tint(double amount)
        {
            var accent=theme.Accent.Color;
            return new(Color.FromRgb((byte)(11+(accent.R-11)*amount),(byte)(25+(accent.G-25)*amount),(byte)(38+(accent.B-38)*amount)));
        }
        style.Setters.Add(new Setter(Control.TemplateProperty,controlChrome));
        style.Setters.Add(new Setter(Control.ForegroundProperty,new SolidColorBrush(Color.FromRgb(234,245,255))));
        style.Setters.Add(new Setter(Control.BackgroundProperty,Tint(0)));
        style.Setters.Add(new Setter(Control.BorderBrushProperty,Tint(.45)));
        style.Setters.Add(new Setter(Control.BorderThicknessProperty,new Thickness(1)));
        style.Setters.Add(new Setter(Control.MinHeightProperty,34d));
        style.Setters.Add(new Setter(FrameworkElement.CursorProperty,Cursors.Hand));
        style.Setters.Add(new Setter(FrameworkElement.FocusVisualStyleProperty,null));
        void State(DependencyProperty property,object value,params Setter[] setters)
        {var trigger=new Trigger{Property=property,Value=value};foreach(var setter in setters)trigger.Setters.Add(setter);style.Triggers.Add(trigger);}
        if(toggle)State(ToggleButton.IsCheckedProperty,true,new(Control.BackgroundProperty,Tint(.18)),new(Control.BorderBrushProperty,theme.Accent),new(Control.FontWeightProperty,FontWeights.SemiBold));
        State(UIElement.IsMouseOverProperty,true,new(Control.BackgroundProperty,Tint(.26)),new(Control.BorderBrushProperty,theme.Accent));
        State(ButtonBase.IsPressedProperty,true,new(Control.BackgroundProperty,Tint(.36)),new(Control.BorderBrushProperty,Brushes.White));
        State(UIElement.IsKeyboardFocusedProperty,true,new Setter(Control.BorderBrushProperty,Brushes.White));
        State(UIElement.IsEnabledProperty,false,new Setter(UIElement.OpacityProperty,.45));
        return style;
    }
    // Explicit native review only: inspect the actual rendered template colours
    // in both toggle states, then restore each control without saving preferences.
    internal object[] CheckControlStates()
    {
        var states=new List<object>();
        foreach(var button in new[]{rotation,hologram})
        {
            bool? saved=button.IsChecked;
            try
            {
                foreach(bool selected in new[]{false,true})
                {
                    button.IsChecked=selected;button.ApplyTemplate();button.UpdateLayout();
                    if(button.Template.FindName("Chrome",button) is not Border {Background:SolidColorBrush background}
                        ||button.Foreground is not SolidColorBrush foreground)throw new InvalidOperationException("Holoview button template is missing its controlled colours.");
                    double a=Luminance(foreground.Color),b=Luminance(background.Color),contrast=(Math.Max(a,b)+.05)/(Math.Min(a,b)+.05);
                    if(contrast<4.5)throw new InvalidOperationException("Holoview button text is unreadable: "+button.Content);
                    states.Add(new{Label=button.Content,Selected=selected,Contrast=contrast,Foreground=foreground.Color.ToString(),Background=background.Color.ToString()});
                }
            }
            finally{button.IsChecked=saved;}
        }
        return states.ToArray();
        static double Luminance(Color c)
        {static double Linear(byte x){double n=x/255d;return n<=.04045?n/12.92:Math.Pow((n+.055)/1.055,2.4);}return .2126*Linear(c.R)+.7152*Linear(c.G)+.0722*Linear(c.B);}
    }
    internal void ReleaseViewport(){viewport.CloseRequested-=close;stage.Children.Remove(viewport);}
    public void SetMotion(bool enabled){rotation.IsChecked=enabled;viewport.SetMotion(active&&enabled);}
    public void SetActive(bool enabled){active=enabled;viewport.SetMotion(active&&rotation.IsChecked==true);viewport.SetHologramMotion(active&&hologram.IsChecked==true);}
    public void SetReadoutPulse(bool enabled){foreach(var reading in readouts)reading.SetPulse(enabled);}
    internal void CheckInteraction()
    {
        if(!viewport.Interactive||!viewport.IsHolotable||!viewport.IsHitTestVisible||!ReferenceEquals(viewport.Parent,stage))
            throw new InvalidOperationException("The holotable did not expose its full model for orbit and zoom.");
        viewport.Check();CheckBindings();
    }
    internal void CheckBindings()
    {
        if(bindings.Any(pair=>pair.Reading.Text!=pair.Source.Text))
            throw new InvalidOperationException("Holotable readings diverged from the sensor presentation.");
    }
    internal void CheckCenteredLayout()
    {
        var centre=viewport.TranslatePoint(new Point(viewport.ActualWidth/2,viewport.ActualHeight/2),this);
        if(viewport.ActualWidth<=0 || viewport.ActualHeight<=0 || Math.Abs(centre.X-ActualWidth/2)>.5)
            throw new InvalidOperationException("The holotable viewport is not centred in the full display.");
    }
    internal static bool Supports(string? ship)=>FleetViewport.Supports(ship);
    internal static string ModelCredit(string ship)=>ship switch
    {
        "SolRing"=>"ORIGINAL V3 FAN RECONSTRUCTION",
        "Arboghast"=>"X890 RECONSTRUCTION / CHRIS DANELON REFERENCES",
        "Knight"=>"SYFY / VOODOO · CC BY 4.0 · X890 ADAPTATION",
        "Razorback"=>"OWNER-SUPPLIED V2","Rocinante"=>"MARTIN TAICHL / CC BY 4.0",
        "Truman" or "AgathaKing"=>"MOHAMEDHUSSIEN",
        "Pella"=>"MARTIN TAICHL / CC BY 4.0 · ADAPTED",
        "Anubis"=>"MARTIN TAICHL / CC BY 4.0 · ADAPTED",
        "Donnager" or "Dropship" or "Epstein"=>"CHRIS KUHN / OWLSTRAW",
        _=>"ORIGINAL FAN MODEL"
    };
    internal static void CheckResources()
    {
        foreach(string ship in ShipProfile.All.Select(profile=>profile.Id))
        {
            if(!Rookline.AssetProtection.AppAssets.Exists("FleetViewer/models/"+ship+".glb.gz"))
                throw new InvalidDataException("The full ship model is missing: "+ship);
        }
    }
}
