using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;

namespace SensorPanel;

internal sealed partial class PanelWindow
{
    private async Task CheckSolarRadianceAsync()
    {
        string output=Path.Combine(PanelPaths.Artifacts,"solar-radiance-20261023","native");
        Directory.CreateDirectory(output);var rows=new List<object>();bool passed=false;
        try
        {
            if(previewPath is null||!windowed)throw new InvalidOperationException("Solar radiance review requires a windowed saved-data preview.");
            var view=new SolarSystemView();view.SetGraphicsMode(ExpanseGraphicsMode.Efficient);
            Content=view;Width=1024;Height=720;Title="X890 · Bright Sun / Milky Way review · saved data, not live";
            UpdateLayout();await Task.Delay(400);view.SetMotion(false);
            view.PrepareTrueScaleReview("Sun");view.ZoomForReview(.65);await Task.Delay(250);
            Save("wide-Milky-Way");rows.Add(new{Phase="wide",Map=view.Diagnose()});
            double wideOpacity=view.Diagnose().GetProperty("milkyWayOpacity").GetDouble();
            view.PrepareTrueScaleReview("Sun");await Task.Delay(200);Save("system");
            view.ZoomForReview(2);await Task.Delay(200);rows.Add(new{Phase="fading",Map=view.Diagnose()});
            double midwayOpacity=view.Diagnose().GetProperty("milkyWayOpacity").GetDouble();
            view.PrepareTrueScaleReview("Sun");view.ZoomForReview(1800);await Task.Delay(250);
            var close=Save("Sun-close");rows.Add(new{Phase="Sun-close",Map=view.Diagnose()});
            if(wideOpacity<.8||midwayOpacity>=wideOpacity||view.Diagnose().GetProperty("milkyWayOpacity").GetDouble()!=0)
                throw new InvalidOperationException("Milky Way did not appear at wide zoom and disappear for close inspection.");
            double radius=view.Diagnose().GetProperty("sunPhysicalRadiusPixels").GetDouble();
            double meanSunLuminance=DiskLuminance(close,view.SunCenterForReview,radius*.72);
            view.SetSolarLightForReview(false);await Task.Delay(200);var noLamp=Save("Sun-no-point-light");view.SetSolarLightForReview(true);
            var sunMaterial=((MaterialGroup)SolarBodyAssets.Load("Sun").Children.OfType<GeometryModel3D>().First().Material).Children.OfType<EmissiveMaterial>().Single();
            var sunImage=(BitmapSource)((ImageBrush)sunMaterial.Brush).ImageSource;
            var sunEncoder=new PngBitmapEncoder();sunEncoder.Frames.Add(BitmapFrame.Create(sunImage));using(var sunFile=File.Create(Path.Combine(output,"Sun-surface-texture.png")))sunEncoder.Save(sunFile);
            File.WriteAllText(Path.Combine(output,"Sun-exposure-probe.json"),JsonSerializer.Serialize(new{LampOn=meanSunLuminance,LampOff=DiskLuminance(noLamp,view.SunCenterForReview,radius*.72),Colour=sunMaterial.Color.ToString(),sunMaterial.Color.ScR,Assembly=typeof(App).Assembly.Location},JsonOptions));
            if(meanSunLuminance<.58||meanSunLuminance>.98)throw new InvalidOperationException("Rendered solar disk exposure is outside its readable range: "+meanSunLuminance);

            // Verify that solar emission lights a real diffuse mesh, beyond its corona.
            view.PrepareTrueScaleReview("Mars");view.RotateForReview(Math.PI,0);await Task.Delay(250);var lit=Save("Mars-sunlight-on");
            view.SetSolarLightForReview(false);await Task.Delay(250);var dark=Save("Mars-sunlight-off");
            view.SetSolarLightForReview(true);
            var a=Pixels(lit);var b=Pixels(dark);int changed=0;double additionalLuminance=0;
            for(int i=0;i<a.Length;i+=4)
            {
                double delta=Luminance(a,i)-Luminance(b,i);
                if(delta>.01){changed++;additionalLuminance+=delta;}
            }
            if(changed<100||additionalLuminance<10)throw new InvalidOperationException("Solar point light did not illuminate the rendered planet.");

            view.SetTrueScale(false);view.Check();
            view.PrepareTrueScaleReview("Sun");view.ZoomForReview(.65);
            long paused=view.Diagnose().GetProperty("sceneUpdates").GetInt64();await Task.Delay(350);
            if(view.Diagnose().GetProperty("sceneUpdates").GetInt64()!=paused)throw new InvalidOperationException("Paused sky introduced continuous scene updates.");
            view.SetTimeRateForReview(1);view.SetMotion(true);await Task.Delay(250);view.Visibility=Visibility.Collapsed;await Task.Delay(150);
            long hidden=view.Diagnose().GetProperty("sceneUpdates").GetInt64();await Task.Delay(250);
            if(view.Diagnose().GetProperty("sceneUpdates").GetInt64()!=hidden)throw new InvalidOperationException("Hidden solar lighting kept updating.");
            view.Visibility=Visibility.Visible;view.SetMotion(false);view.SetTimeRateForReview(0);
            view.Width=420;view.Height=260;UpdateLayout();view.PrepareTrueScaleReview("Sun");view.ZoomForReview(.65);await Task.Delay(200);Save("compact-system");
            view.Width=double.NaN;view.Height=double.NaN;UpdateLayout();
            view.PrepareTrueScaleReview("Sun");view.ZoomForReview(.65);await Task.Delay(200);Save("review");view.SetMotion(true);
            File.WriteAllText(Path.Combine(output,"result.json"),JsonSerializer.Serialize(new{Passed=true,SavedData=true,ProcessId=Environment.ProcessId,
                MeanSolarDiskLuminance=meanSunLuminance,SolarLightAffectedPixels=changed,AdditionalPlanetLuminance=additionalLuminance,
                WideSkyOpacity=wideOpacity,MidwaySkyOpacity=midwayOpacity,CloseSkyOpacity=0,SkyFadesWithZoom=true,
                PausedSceneStopped=true,HiddenSceneStopped=true,GeometryAndEphemerisChecks=true,Rows=rows,
                Scope="Rendered native saved-data navigation; no installation, live hardware, physical photometry or whole-process GPU measurement."},JsonOptions));passed=true;
            RenderTargetBitmap Save(string name)
            {
                var bitmap=new RenderTargetBitmap((int)Math.Ceiling(view.ActualWidth),(int)Math.Ceiling(view.ActualHeight),96,96,PixelFormats.Pbgra32);
                var drawing=new DrawingVisual();using(var dc=drawing.RenderOpen())dc.DrawRectangle(new VisualBrush(view),null,new Rect(0,0,view.ActualWidth,view.ActualHeight));
                bitmap.Render(drawing);var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));
                using var stream=File.Create(Path.Combine(output,name+".png"));encoder.Save(stream);return bitmap;
            }
        }
        catch(Exception ex)
        {File.WriteAllText(Path.Combine(output,"result.json"),JsonSerializer.Serialize(new{Passed=false,Error=ex.ToString(),Rows=rows},JsonOptions));Environment.ExitCode=1;}
        finally{if(!passed||!Environment.GetCommandLineArgs().Contains("--leave-review-open"))Close();}
    }
    private static byte[] Pixels(BitmapSource image)
    {var pixels=new byte[image.PixelWidth*image.PixelHeight*4];image.CopyPixels(pixels,image.PixelWidth*4,0);return pixels;}
    private static double Luminance(byte[] pixels,int i)=>(pixels[i+2]*.2126+pixels[i+1]*.7152+pixels[i]*.0722)/255;
    private static double DiskLuminance(BitmapSource image,Point centre,double radius)
    {
        var pixels=Pixels(image);double sum=0;int count=0;
        for(int y=Math.Max(0,(int)(centre.Y-radius));y<Math.Min(image.PixelHeight,centre.Y+radius);y++)
        for(int x=Math.Max(0,(int)(centre.X-radius));x<Math.Min(image.PixelWidth,centre.X+radius);x++)
        {if(Math.Pow(x-centre.X,2)+Math.Pow(y-centre.Y,2)>radius*radius)continue;sum+=Luminance(pixels,(y*image.PixelWidth+x)*4);count++;}
        return sum/Math.Max(1,count);
    }
}
