using System.Windows;
using System.Windows.Media;

namespace SensorPanel;

// Cache decorative drawings at their physical display size, including Viewbox
// scaling. Do not apply to live readings, moving geometry or the ship viewport.
internal static class NativeRasterCache
{
    internal static void Apply(FrameworkElement element,bool enabled)
    {
        if(!enabled){element.CacheMode=null;return;}
        if(!element.IsLoaded||!element.IsVisible||element.ActualWidth<=0||element.ActualHeight<=0
            ||Window.GetWindow(element) is not {} owner||!owner.IsAncestorOf(element))return;
        var bounds=element.TransformToAncestor(owner).TransformBounds(new Rect(0,0,element.ActualWidth,element.ActualHeight));
        var dpi=VisualTreeHelper.GetDpi(element);
        double scale=Math.Max(1,Math.Max(bounds.Width/element.ActualWidth*dpi.DpiScaleX,bounds.Height/element.ActualHeight*dpi.DpiScaleY));
        if(element.CacheMode is BitmapCache cache&&Math.Abs(cache.RenderAtScale-scale)<.000001)return;
        element.CacheMode=new BitmapCache(scale);
    }
}
