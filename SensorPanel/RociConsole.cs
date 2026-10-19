using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace SensorPanel;

internal sealed partial class ShipPanel
{
    // Static identity and instrument rules from the reviewed console study.
    // The existing collector bindings, solar scene and full model stay live.
    private void AddRociIdentity(Rect sidebar)
    {
        var emblem=new BitmapImage(new Uri("pack://application:,,,/Ships/Rocinante-Crew.png"));
        emblem.Freeze();
        var logo=new Image { Source=emblem, Stretch=Stretch.Uniform, IsHitTestVisible=false };
        Put(this,new Border { Background=PanelTheme.Resolve("Rocinante").Divider },new(sidebar.X+12,sidebar.Bottom-155,sidebar.Width-24,1));
        Put(this,logo,new(sidebar.X+12,sidebar.Bottom-148,Math.Min(112,sidebar.Width-24),130));
        if(sidebar.Width>180)
        {
            var caption=Label("ROCINANTE\n\nCORVETTE\nCLASS",11,PanelTheme.Resolve("Rocinante").Muted);
            caption.FontFamily=new FontFamily("Consolas");
            Put(this,caption,new(sidebar.X+130,sidebar.Bottom-120,sidebar.Width-138,100));
        }
    }

    private static void RociInstrumentPlane(Canvas card,Rect bounds,PanelTheme theme)
    {
        var red=new SolidColorBrush(Color.FromRgb(158,60,66));red.Freeze();
        Put(card,new Border { Background=theme.Surface,Opacity=.88 },new(0,0,bounds.Width,bounds.Height));
        Put(card,new Border { Background=theme.Divider },new(0,0,bounds.Width,1));
        Put(card,new Border { Background=red,Opacity=.7 },new(0,29,bounds.Width,1));
        Put(card,new Border { Background=red },new(bounds.Width-18,29,18,2));
        Put(card,new Border { Background=theme.Divider,Opacity=.65 },new(bounds.Width-1,0,1,bounds.Height));
    }
}
