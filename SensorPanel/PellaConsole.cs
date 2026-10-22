using System.Windows;
using System.Windows.Media;

namespace SensorPanel;

internal sealed partial class ShipPanel
{
    // Preserve the release instruments and navigation, with more model width.
    private static Plan PellaPlan(Plan plan)
    {
        if(plan.Mode=="Portrait")return plan;
        double sidebarWidth=plan.Mode=="Wide"?280:244;
        double bodyX=plan.Sidebar.X+sidebarWidth+18;
        double right=plan.Mode=="Wide"?plan.Memory.X-16:plan.Width-22;
        double columnWidth=(right-bodyX-18)/2;
        return plan with
        {
            Sidebar=new(plan.Sidebar.X,plan.Sidebar.Y,sidebarWidth,plan.Sidebar.Height),
            Cpu=new(bodyX,plan.Cpu.Y,columnWidth,plan.Cpu.Height),
            Gpu=new(bodyX+columnWidth+18,plan.Gpu.Y,columnWidth,plan.Gpu.Height),
            Navigation=new(bodyX,plan.Navigation.Y,right-bodyX,plan.Navigation.Height),
            Memory=plan.Mode=="Wide"?plan.Memory:new(bodyX,plan.Memory.Y,columnWidth,plan.Memory.Height),
            Network=plan.Mode=="Wide"?plan.Network:new(bodyX+columnWidth+18,plan.Network.Y,columnWidth,plan.Network.Height),
            Board=new(bodyX,plan.Board.Y,plan.Width-bodyX-22,plan.Board.Height)
        };
    }
}

// Static grid over the existing Expanse backdrop. No scope walls or reticle;
// the cached drawing changes only when its surface resizes.
internal sealed class PellaGrid : FrameworkElement
{
    private readonly Color ink;
    private DrawingGroup? drawing;
    private Size cachedSize;
    internal PellaGrid(Color ink)
    {
        this.ink=ink;IsHitTestVisible=false;SizeChanged+=(_,_)=>InvalidateVisual();
        LayoutUpdated+=(_,_)=>NativeRasterCache.Apply(this,IsVisible);
    }
    protected override void OnRender(DrawingContext dc)
    {
        if(ActualWidth<=0||ActualHeight<=0)return;
        var size=new Size(ActualWidth,ActualHeight);
        if(drawing is null||cachedSize!=size)
        {
            cachedSize=size;drawing=new DrawingGroup();
            using(var c=drawing.Open())
            {
                var fine=new Pen(new SolidColorBrush(ink){Opacity=.13},.6);
                var major=new Pen(new SolidColorBrush(ink){Opacity=.22},.7);
                fine.Freeze();major.Freeze();
                for(int x=0;x<size.Width;x+=16)c.DrawLine(x%80==0?major:fine,new(x,0),new(x,size.Height));
                for(int y=0;y<size.Height;y+=16)c.DrawLine(y%80==0?major:fine,new(0,y),new(size.Width,y));
            }
            drawing.Freeze();
        }
        dc.DrawDrawing(drawing);
    }
}
