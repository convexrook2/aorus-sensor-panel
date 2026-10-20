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
        viewport.SetHolotableMode(true,theme.Accent.Color);viewport.SetMotion(motion);viewport.CloseRequested+=close;
        stage.Children.Add(viewport);SetRow(stage,1);Children.Add(stage);
        var footer=new StackPanel { Margin=new Thickness(30,0,30,20),Background=new SolidColorBrush(Color.FromArgb(170,4,12,19)) };
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
        footer.Children.Add(new TextBlock { Text="MODEL / "+ModelCredit(theme.ShipId!)+"     ·     ESC TO RETURN",
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
        var button=new Button { Content=title,Foreground=theme.Accent,Background=theme.Surface,BorderBrush=theme.Accent,
            BorderThickness=new Thickness(1),Padding=new Thickness(12,6,12,6),Margin=new Thickness(5,0,0,0),FontSize=11,Cursor=Cursors.Hand };
        button.Click+=(_,e)=>{e.Handled=true;action();};return button;
    }
    internal void ReleaseViewport(){viewport.CloseRequested-=close;stage.Children.Remove(viewport);}
    public void SetMotion(bool enabled)=>viewport.SetMotion(enabled);
    public void SetReadoutPulse(bool enabled){foreach(var reading in readouts)reading.SetPulse(enabled);}
    internal void CheckInteraction()
    {
        if(viewport.Interactive||!viewport.IsHolotable||viewport.IsHitTestVisible||!ReferenceEquals(viewport.Parent,stage))
            throw new InvalidOperationException("The passive holotable did not reuse the full model viewport without navigation controls.");
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
    internal static bool Supports(string? ship)=>ShipPortrait.Supports(ship);
    internal static string ModelCredit(string ship)=>ship switch
    {
        "Razorback"=>"OWNER-SUPPLIED V2","Rocinante"=>"MARTIN TAICHL / CC BY 4.0",
        "Truman" or "AgathaKing"=>"MOHAMEDHUSSIEN",
        "Pella"=>"MARTIN TAICHL / CC BY 4.0 · ADAPTED",
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
