using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace SensorPanel;

// One shared, exposure-adjusted copy of the credited bundled panorama.
// Camera zoom changes opacity only; there is no sky timer or particle renderer.
internal static class SolarSky
{
    private static BitmapSource? panorama;
    internal static BitmapSource Panorama=>panorama??=Load();
    internal static void Release()=>panorama=null;
    internal static double Opacity(double zoom)
    {
        double wide=Math.Clamp((2.4-zoom)/(2.4-.65),0,1);
        return .88*wide*wide*(3-2*wide);
    }
    private static BitmapSource Load()
    {
        var source=new FormatConvertedBitmap(SolarBodyAssets.Bitmap("skybox_milky_way.jpg"),PixelFormats.Bgra32,null,0);
        int stride=source.PixelWidth*4;var pixels=new byte[stride*source.PixelHeight];source.CopyPixels(pixels,stride,0);
        var exposure=Enumerable.Range(0,256).Select(value=>(byte)Math.Round(255*(1-Math.Exp(-3.4*value/255)))).ToArray();
        for(int i=0;i<pixels.Length;i+=4)
        {pixels[i]=exposure[pixels[i]];pixels[i+1]=exposure[pixels[i+1]];pixels[i+2]=exposure[pixels[i+2]];pixels[i+3]=255;}
        var bitmap=BitmapSource.Create(source.PixelWidth,source.PixelHeight,96,96,PixelFormats.Bgra32,null,pixels,stride);bitmap.Freeze();return bitmap;
    }
}
