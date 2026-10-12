using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace SensorPanel;

// Each sensor section keeps its original artwork proportions while sections
// move to fit landscape, strip and portrait screens.
internal sealed class PanelLayout
{
    internal sealed record Plan(string Mode, double Width, double Height, Rect Cpu, Rect Gpu, Rect System, Rect Network, Rect Board);
    private readonly Canvas board;
    private readonly Dictionary<string, TextBlock> text;
    private readonly Image left, right, brand;
    private readonly Viewbox cpu, gpu, system, network;
    private readonly UIElement topBar, bottomBar;
    public string Mode { get; private set; } = "Landscape";

    public PanelLayout(Canvas board, Dictionary<string, TextBlock> text, Image left, Image right, Image brand)
    {
        this.board = board; this.text = text; this.left = left; this.right = right; this.brand = brand;
        topBar = board.Children[0]; bottomBar = board.Children[1];
        cpu = Extract(212, 78, 424, 307, (x,y) => x >= 200 && x < 636 && y >= 78 && y < 390);
        gpu = Extract(636, 78, 388, 307, (x,y) => x >= 636 && y >= 78 && y < 390);
        system = Extract(212, 426, 414, 126, (x,y) => x >= 200 && x < 636 && y >= 426 && y < 554);
        network = Extract(636, 426, 388, 126, (x,y) => x >= 636 && y >= 426 && y < 554);
    }

    private Viewbox Extract(double originX, double originY, double width, double height, Func<double,double,bool> matches)
    {
        var section = new Canvas { Width = width, Height = height };
        foreach (var child in board.Children.Cast<UIElement>().ToArray())
        {
            double x = Canvas.GetLeft(child), y = Canvas.GetTop(child);
            if (!matches(x,y)) continue;
            board.Children.Remove(child); section.Children.Add(child);
            Canvas.SetLeft(child,x-originX); Canvas.SetTop(child,y-originY);
        }
        var host = new Viewbox { Child=section, Stretch=Stretch.Uniform, HorizontalAlignment=HorizontalAlignment.Left };
        board.Children.Add(host); return host;
    }

    internal static Plan Calculate(double aspect, string requested = "Auto")
    {
        aspect = double.IsFinite(aspect) && aspect > 0 ? aspect : 16.0/9;
        string mode = requested is "Classic" or "Wide" or "Portrait" ? requested
            : aspect >= 2.5 ? "Wide" : aspect < 0.9 ? "Portrait" : "Landscape";
        if (mode is "Landscape" or "Classic")
        {
            double height = mode == "Classic" ? 576 : Math.Max(576,1024/aspect);
            return new(mode,1024,height,new(212,78,424,307),new(636,78,388,307),
                new(212,height-150,414,126),new(636,height-150,388,126),new(220,height-175,780,28));
        }
        if (mode == "Wide")
        {
            double height = Math.Max(384,1536/aspect), rowHeight = height-128;
            const double column = 448;
            return new(mode,1536,height,new(160,70,column,rowHeight),new(160+column,70,column,rowHeight),
                new(160+column*2,70,column,rowHeight/2-5),
                new(160+column*2,75+rowHeight/2,column,rowHeight/2-5),new(160,height-48,1360,28));
        }
        double portraitHeight = Math.Max(720,512/aspect);
        double scale = Math.Min(472/424.0,(portraitHeight-190)/896);
        double spare = Math.Max(0,portraitHeight-190-(866*scale+42));
        double gap = 14+spare/3, nextY=85;
        Rect Next(double width,double height)
        {
            var rect = new Rect((512-width*scale)/2,nextY,width*scale,height*scale);
            nextY += height*scale+gap; return rect;
        }
        var cpu=Next(424,307); var gpu=Next(388,307); var system=Next(414,126); var network=Next(388,126);
        return new(mode,512,portraitHeight,cpu,gpu,system,network,new(20,portraitHeight-80,472,55));
    }

    public void Apply(double aspect,string requested)
    {
        var plan = Calculate(aspect,requested); Mode=plan.Mode;
        board.Width=plan.Width; board.Height=plan.Height;
        Place(cpu,plan.Cpu); Place(gpu,plan.Gpu); Place(system,plan.System); Place(network,plan.Network);
        bool portrait=Mode=="Portrait", wide=Mode=="Wide";
        left.Visibility=right.Visibility=portrait ? Visibility.Collapsed : Visibility.Visible;
        Place(left,new(0,0,wide ? 150 : 200,plan.Height));
        Place(right,new(plan.Width-20,0,20,plan.Height));
        Place(topBar,new(portrait ? 0 : wide ? 150 : 178,0,plan.Width-(portrait ? 0 : wide ? 150 : 178),18));
        Place(bottomBar,new(portrait ? 0 : wide ? 150 : 115,plan.Height-18,plan.Width-(portrait ? 0 : wide ? 150 : 115),18));
        double contentLeft=portrait ? 20 : wide ? 160 : 220;
        Place(text["boardName"],new(contentLeft,portrait ? 20 : 19,portrait ? 425 : plan.Width-contentLeft-306,32));
        text["boardName"].FontSize=portrait ? 20 : 25;
        Place(text["date"],new(portrait ? 20 : plan.Width-234,portrait ? 52 : 21,220,25));
        Place(brand,new(portrait ? 461 : plan.Width-300,17,35,27));
        Place(text["boardSensors"],plan.Board); text["boardSensors"].TextWrapping=TextWrapping.Wrap;
        text["boardSensors"].FontSize=portrait ? 14 : 15;
        Place(text["fanMade"],new(portrait ? 368 : 26,portrait ? 54 : plan.Height-25,120,22));
        text["fanMade"].Foreground=portrait ? Brushes.Black : Brushes.White;
        Place(text["status"],new(contentLeft,plan.Height-19,portrait ? 300 : 470,15));
        Place(text["controls"],new(plan.Width-279,plan.Height-19,264,15));
        text["controls"].Visibility=portrait ? Visibility.Collapsed : Visibility.Visible;
    }

    private static void Place(UIElement element,Rect bounds)
    {
        if (element is FrameworkElement child) { child.Width=bounds.Width; child.Height=bounds.Height; }
        Canvas.SetLeft(element,bounds.X); Canvas.SetTop(element,bounds.Y);
    }

    internal static void Check()
    {
        foreach (var size in new[] {(1920,1080),(1024,600),(800,600),(800,800),(640,360),(1920,480),(1920,462),(1280,480),(480,1920),(480,800),(1080,1920),(3840,2160)})
        {
            var plan=Calculate(size.Item1/(double)size.Item2);
            Rect[] sections=[plan.Cpu,plan.Gpu,plan.System,plan.Network];
            var viewport=new Rect(0,0,plan.Width,plan.Height);
            if (sections.Any(r=>r.Width<=0 || r.Height<=0 || !viewport.Contains(r)))
                throw new InvalidOperationException($"Layout leaves the canvas at {size}.");
            for(int i=0;i<sections.Length;i++) for(int j=i+1;j<sections.Length;j++)
                if (Rect.Intersect(sections[i],sections[j]) is { IsEmpty:false, Width:>0, Height:>0 })
                    throw new InvalidOperationException($"Sensor sections overlap at {size}.");
            var display=new DisplayInfo("test",-size.Item1,100,size.Item1,size.Item2,false);
            var fit=Displays.Placement(display,true); var window=Displays.Placement(display,false);
            if (fit.Left!=display.Left || fit.Top!=display.Top || fit.Width!=display.Width || fit.Height!=display.Height ||
                window.Width>display.Width || window.Height>display.Height)
                throw new InvalidOperationException("Fit and restore must stay inside the selected monitor, including negative desktop coordinates.");
        }
    }
}
