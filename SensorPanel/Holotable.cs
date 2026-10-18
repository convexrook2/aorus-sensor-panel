using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;

namespace SensorPanel;

internal sealed class Holotable : Grid
{
    private readonly FleetViewport viewport;
    private readonly Grid stage;
    private readonly Action close;
    private readonly List<HoloReadout> readouts = new();
    private readonly List<(HoloReadout Reading, TextBlock Source)> bindings = new();
    internal bool MotionActive => viewport.MotionActive;

    public Holotable(PanelTheme theme, Dictionary<string, TextBlock> source, bool motion,
        bool readoutPulse, Action close, FleetViewport viewport)
    {
        this.viewport=viewport;this.close=close;
        Focusable=true;ClipToBounds=true;
        Background=new SolidColorBrush(Color.FromRgb(4,12,19));
        RowDefinitions.Add(new(){Height=GridLength.Auto});
        RowDefinitions.Add(new(){Height=new GridLength(1,GridUnitType.Star)});
        RowDefinitions.Add(new(){Height=GridLength.Auto});
        var header=new StackPanel { Margin=new Thickness(22,16,22,8) };
        header.Children.Add(new TextBlock { Text=theme.Vessel!.Name+"  /  HOLOTABLE",FontSize=22,
            FontFamily=new FontFamily("Bahnschrift"),Foreground=theme.Accent });
        if(!Supports(theme.ShipId))throw new InvalidOperationException("This ship has no 3D holotable model.");
        header.Children.Add(new TextBlock { Text="3D MODEL / "+ModelCredit(theme.ShipId!)+" · FULL MODEL / PBR",
            FontSize=10,Foreground=theme.Muted });
        var buttons=new StackPanel { Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Right,Margin=new Thickness(0,8,0,0) };
        buttons.Children.Add(Command("RESET VIEW",theme,viewport.Reset));
        buttons.Children.Add(Command("CLOSE  ×",theme,close));
        header.Children.Add(buttons);Children.Add(header);

        stage=new Grid();stage.ColumnDefinitions.Add(new(){Width=new GridLength(1,GridUnitType.Star)});
        stage.ColumnDefinitions.Add(new(){Width=GridLength.Auto});
        viewport.SetInteractive(true);viewport.SetMotion(motion);viewport.CloseRequested+=close;
        stage.Children.Add(viewport);SetRow(stage,1);Children.Add(stage);
        var footer=new StackPanel { Margin=new Thickness(22,0,22,18) };
        var readings=new System.Windows.Controls.Primitives.UniformGrid { Columns=3 };
        foreach(var item in new[]{("CPU","cpuTemperature"),("GPU","gpuTemperature"),("FPS","fps"),("CPU POWER","cpuPower"),("GPU POWER","gpuPower"),("MEMORY","memory")})
        {
            var cell=new StackPanel { Margin=new Thickness(6,3,6,5) };
            cell.Children.Add(new TextBlock { Text=item.Item1,FontSize=10,Foreground=theme.Muted });
            var reading=new HoloReadout(item.Item1.StartsWith("GPU")?theme.SecondaryAccent??theme.Accent:theme.Accent,readoutPulse)
                { FontSize=25,FontFamily=new FontFamily("Consolas") };
            reading.SetBinding(TextBlock.TextProperty,new Binding(nameof(TextBlock.Text)){Source=source[item.Item2],NotifyOnTargetUpdated=true});
            readouts.Add(reading);bindings.Add((reading,source[item.Item2]));cell.Children.Add(reading);readings.Children.Add(cell);
        }
        footer.Children.Add(readings);
        footer.Children.Add(new TextBlock { Text="DRAG / TOUCH TO ORBIT   ·   SCROLL / PINCH TO ZOOM   ·   ARROWS TO ROTATE   ·   HOME TO RESET   ·   ESC TO CLOSE",
            FontSize=9,Foreground=theme.Muted,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(6,8,0,0) });
        SetRow(footer,2);Children.Add(footer);
        bool? previousPortrait=null;
        SizeChanged+=(_,_)=>
        {
            bool portrait=ActualWidth<ActualHeight;if(previousPortrait==portrait)return;previousPortrait=portrait;
            if(readings.Parent is Panel parent)parent.Children.Remove(readings);
            readings.Columns=portrait?3:1;
            readings.Width=portrait?double.NaN:Math.Clamp(ActualWidth*.2,180,280);
            readings.VerticalAlignment=portrait?VerticalAlignment.Stretch:VerticalAlignment.Center;
            readings.Margin=portrait?new Thickness(0):new Thickness(12,4,24,4);
            if(portrait)footer.Children.Insert(0,readings);else{SetColumn(readings,1);stage.Children.Add(readings);}
        };
        KeyDown+=(_,e)=>{if(e.Key==Key.Escape){close();e.Handled=true;}else if(e.Key==Key.Home){viewport.Reset();e.Handled=true;}};
    }

    internal static Button Command(string title,PanelTheme theme,Action action)
    {
        var button=new Button { Content=title,Foreground=theme.Accent,Background=theme.Surface,BorderBrush=theme.Accent,
            BorderThickness=new Thickness(1),Padding=new Thickness(12,6,12,6),Margin=new Thickness(5,0,0,0),FontSize=11,Cursor=Cursors.Hand };
        button.Click+=(_,e)=>{e.Handled=true;action();};return button;
    }
    internal void ReleaseViewport(){viewport.CloseRequested-=close;stage.Children.Remove(viewport);}
    public void SetMotion(bool enabled)=>viewport.SetMotion(enabled);
    public void SetReadoutPulse(bool enabled){foreach(var reading in readouts)reading.SetPulse(enabled);}
    internal void CheckInteraction()
    {
        if(!viewport.Interactive||!ReferenceEquals(viewport.Parent,stage))
            throw new InvalidOperationException("The holotable did not reuse the full model viewport.");
        viewport.Check();CheckBindings();
    }
    internal void CheckBindings()
    {
        if(bindings.Any(pair=>pair.Reading.Text!=pair.Source.Text))
            throw new InvalidOperationException("Holotable readings diverged from the sensor presentation.");
    }
    internal static bool Supports(string? ship)=>ShipPortrait.Supports(ship);
    internal static string ModelCredit(string ship)=>ship switch
    {
        "Razorback"=>"OWNER-SUPPLIED V2","Rocinante"=>"JAKUB.VILDOMEC",
        "Truman" or "AgathaKing"=>"MOHAMEDHUSSIEN",
        "Pella"=>"MARTIN TAICHL / CC BY 4.0 · ADAPTED",
        "Donnager" or "Dropship" or "Epstein"=>"CHRIS KUHN / OWLSTRAW",
        _=>"ORIGINAL FAN MODEL"
    };
    internal static void CheckResources()
    {
        foreach(string ship in ShipProfile.All.Select(profile=>profile.Id))
        {
            if(!File.Exists(Path.Combine(AppContext.BaseDirectory,"FleetViewer","models",ship+".glb.gz")))
                throw new InvalidDataException("The full ship model is missing: "+ship);
        }
    }
}
