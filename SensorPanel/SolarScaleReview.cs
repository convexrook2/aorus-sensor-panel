using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace SensorPanel;

internal sealed partial class PanelWindow
{
    // Explicit visible review of the product's native navigation component.
    // Saved-data only; no hardware collection, profile changes or installation.
    private async Task CheckTrueScaleAsync()
    {
        bool baseline=Environment.GetCommandLineArgs().Contains("--solar-baseline"),passed=false;
        string output=Path.Combine(PanelPaths.Artifacts,"solar-true-scale-20261023",baseline?"baseline-native":"native");
        Directory.CreateDirectory(output);var rows=new List<object>();
        try
        {
            if(previewPath is null||!windowed)throw new InvalidOperationException("True-scale review requires a windowed saved-data preview.");
            var view=new SolarSystemView();view.SetGraphicsMode(ExpanseGraphicsMode.Efficient);
            Content=view;Width=1024;Height=720;Title="X890 · Native Sol navigation review · saved data, not live";
            UpdateLayout();await Task.Delay(500);view.SetMotion(false);view.SetTimeRateForReview(0);
            double maximumSelectedError=0;
            foreach(var (body,magnification) in new[]{("Sun",1d),("Mercury",1d),("Venus",1d),("Earth",3d),("Jupiter",1d),("Saturn",1d),("SolGate",1d)})
            {
                view.PrepareTrueScaleReview(body,magnification);await Task.Delay(450);UpdateLayout();
                var alignment=Alignment();
                rows.Add(new{Body=body,Magnification=magnification,Map=view.Diagnose(),Alignment=alignment});Save(body+"-true-scale");
            }
            var epochRows=new List<object>();
            foreach(double epoch in new[]{0d,.4193,1d})
            foreach(string body in new[]{"Mercury","Venus","Earth","Mars","Jupiter","Saturn","Uranus","Neptune","Ceres","Eros","Luna","Io","Europa","Ganymede","Callisto","Phoebe"})
            {
                view.PrepareTrueScaleReview(body);view.SetEpochForReview(epoch);view.RotateForReview(.39,.11);
                epochRows.Add(new{Body=body,EpochFraction=epoch,Alignment=Alignment()});
            }
            bool aligned=maximumSelectedError<=.65;
            if(!baseline&&!aligned)throw new InvalidOperationException("A selected body misses its orbit path by "+maximumSelectedError+" pixels.");
            view.SetTrueScale(false);view.Check();
            view.FocusBody("SolGate",false);await Task.Delay(250);Save("SolGate-schematic");
            view.PrepareTrueScaleReview("SolGate");view.RotateForReview(.7,.04);await Task.Delay(250);Save("SolGate-three-quarter");
            view.RotateForReview(Math.PI,0);await Task.Delay(250);Save("SolGate-reverse");
            view.PrepareTrueScaleReview("SolGate");view.SetTimeRateForReview(1);view.SetMotion(true);
            await Task.Delay(550);long advancing=FieldUpdates();await Task.Delay(400);
            if(FieldUpdates()<=advancing)throw new InvalidOperationException("The native map aperture did not animate.");
            view.SetMotion(false);long paused=FieldUpdates();await Task.Delay(300);
            if(FieldUpdates()!=paused)throw new InvalidOperationException("The map aperture advanced with motion disabled.");
            view.Visibility=Visibility.Collapsed;view.SetMotion(true);long hidden=FieldUpdates();await Task.Delay(300);
            if(FieldUpdates()!=hidden)throw new InvalidOperationException("A hidden native map aperture continued rendering.");
            view.Visibility=Visibility.Visible;await Task.Delay(300);view.SetMotion(false);view.SetTimeRateForReview(0);
            view.SetGraphicsMode(ExpanseGraphicsMode.FullFidelity);view.SetTimeRateForReview(1);view.SetMotion(true);
            long fullBefore=view.Diagnose().GetProperty("frameCount").GetInt64();await Task.Delay(350);
            var full=view.Diagnose();
            if(full.GetProperty("frameCount").GetInt64()<=fullBefore||full.GetProperty("frameLimit").ValueKind!=JsonValueKind.Null)
                throw new InvalidOperationException("Full Fidelity lost native composition animation.");
            view.SetMotion(false);long fullPaused=view.Diagnose().GetProperty("frameCount").GetInt64();await Task.Delay(200);
            if(view.Diagnose().GetProperty("frameCount").GetInt64()!=fullPaused)throw new InvalidOperationException("Paused Full Fidelity retained its render callback.");
            view.SetTimeRateForReview(0);view.SetGraphicsMode(ExpanseGraphicsMode.Efficient);
            // Exercise the actual component in the monitor's compact map space.
            view.Width=420;view.Height=260;UpdateLayout();view.PrepareTrueScaleReview("Sun");await Task.Delay(200);Save("compact-system");
            if(!view.Diagnose().GetProperty("medinaInsetFullyVisible").GetBoolean())throw new InvalidOperationException("The compact Ring Space inset was clipped.");
            view.PrepareTrueScaleReview("SolGate");await Task.Delay(200);Save("compact-SolGate");
            view.Width=double.NaN;view.Height=double.NaN;UpdateLayout();
            File.WriteAllText(Path.Combine(output,"result.json"),JsonSerializer.Serialize(new{Passed=aligned,Baseline=baseline,SavedData=true,ProcessId=Environment.ProcessId,MaximumSelectedPathErrorPixels=maximumSelectedError,Rows=rows,
                EpochChecks=epochRows,ApertureAnimated=true,AperturePaused=true,ApertureHiddenStopped=true,FullFidelityCompositionAndPause=true,CompactInsetFullyVisible=true,
                Scope="Native navigation component, rendered saved-data pixels and body/path alignment. Not LIVE monitoring, installed app or whole-process GPU evidence."},JsonOptions));
            passed=true;view.PrepareTrueScaleReview("SolGate");view.SetMotion(true);
            JsonElement Alignment()
            {
                var alignment=JsonSerializer.SerializeToElement(view.PathAlignmentForReview(),JsonOptions);
                foreach(var item in alignment.EnumerateArray())if(item.GetProperty("selected").GetBoolean())
                {
                    if(item.GetProperty("distancePixels").ValueKind!=JsonValueKind.Number)throw new InvalidOperationException("Selected body has no visible orbit path.");
                    maximumSelectedError=Math.Max(maximumSelectedError,item.GetProperty("distancePixels").GetDouble());
                }
                return alignment;
            }
            long FieldUpdates()=>view.Diagnose().GetProperty("solRing").GetProperty("fieldUpdates").GetInt64();
            void Save(string name)
            {
                var image=new RenderTargetBitmap((int)Math.Ceiling(view.ActualWidth),(int)Math.Ceiling(view.ActualHeight),96,96,PixelFormats.Pbgra32);
                var drawing=new DrawingVisual();using(var dc=drawing.RenderOpen())dc.DrawRectangle(new VisualBrush(view),null,new Rect(0,0,view.ActualWidth,view.ActualHeight));
                image.Render(drawing);var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(image));
                using var stream=File.Create(Path.Combine(output,name+".png"));encoder.Save(stream);
            }
        }
        catch(Exception ex)
        {File.WriteAllText(Path.Combine(output,"result.json"),JsonSerializer.Serialize(new{Passed=false,Baseline=baseline,Error=ex.ToString(),Rows=rows},JsonOptions));Environment.ExitCode=1;}
        finally{if(!passed||!Environment.GetCommandLineArgs().Contains("--leave-review-open"))Close();}
    }
}
