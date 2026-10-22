using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace SensorPanel;

internal sealed partial class PanelWindow
{
    // Explicit visible saved-data check; no extra work in ordinary monitoring.
    private async Task CheckStaticFleetAsync()
    {
        string output=Path.Combine(PanelPaths.Artifacts,"showcase-rotation-check");Directory.CreateDirectory(output);
        var rows=new List<object>();object? lastCadence=null;
        try
        {
            if(previewPath is null||!windowed||shipPanel is null)throw new InvalidOperationException("Showcase review requires a windowed saved-data preview.");
            var readings=text.ToDictionary(pair=>pair.Key,pair=>pair.Value.Text);
            var originalGraphics=graphicsMode;ApplyGraphicsMode(ExpanseGraphicsMode.Efficient);
            shipPanel.SetShowcaseRotation(true);
            foreach(string id in new[]{"Pella","Rocinante","Anubis"})
            {
                theme=PanelTheme.Resolve(id);ApplyTheme();Width=1280;Height=800;UpdateLayout();
                shipPanel.CheckRotationControl();shipPanel.CheckBindings();
                var viewport=shipPanel.ShipViewport;await viewport.WaitForReadyAsync();await Task.Delay(400);
                var before=await viewport.DiagnoseAsync();await Task.Delay(2200);var rotating=await viewport.DiagnoseAsync();
                double animationSeconds=rotating.GetProperty("animationSeconds").GetDouble()-before.GetProperty("animationSeconds").GetDouble();
                double drawRate=animationSeconds>0?(rotating.GetProperty("frames").GetInt64()-before.GetProperty("frames").GetInt64())/animationSeconds:0;
                lastCadence=new{Ship=id,Before=before,After=rotating,DrawRate=drawRate,NativeActive=viewport.MotionActive,shipPanel.ShowcaseRotationEnabled};
                if(rotating.GetProperty("frameLimit").GetDouble()!=30
                    ||SystemParameters.ClientAreaAnimation&&(drawRate<=0||drawRate>32))
                    throw new InvalidOperationException("Showcase animation drawing exceeded its 30 Hz budget: "+id+" / "+drawRate);
                double angle=rotating.GetProperty("rotationRadians").GetDouble()-before.GetProperty("rotationRadians").GetDouble();
                if(Math.Abs(angle-animationSeconds*Math.PI*2/30)>.00001)
                    throw new InvalidOperationException("Showcase pacing changed its elapsed-time rotation speed: "+id);
                if(!shipPanel.ShowcaseRotationEnabled||rotating.GetProperty("holotable").GetBoolean()
                    ||SystemParameters.ClientAreaAnimation&&rotating.GetProperty("rotationRadians").GetDouble()==before.GetProperty("rotationRadians").GetDouble())
                    throw new InvalidOperationException("Default showcase rotation did not advance: "+id);
                shipPanel.ToggleRotationControlForReview();await Task.Delay(300);
                var paused=await viewport.DiagnoseAsync();await Task.Delay(700);var still=await viewport.DiagnoseAsync();
                if(shipPanel.ShowcaseRotationEnabled||still.GetProperty("motion").GetBoolean()
                    ||still.GetProperty("frames").GetInt64()!=paused.GetProperty("frames").GetInt64()
                    ||still.GetProperty("rotationRadians").GetDouble()!=paused.GetProperty("rotationRadians").GetDouble())
                    throw new InvalidOperationException("The showcase pause did not hold its exact angle: "+id);
                SaveFrame(id+"-paused");
                shipPanel.CheckHolotable();await viewport.WaitForReadyAsync();await Task.Delay(300);
                var table=await viewport.DiagnoseAsync();shipPanel.CheckHolotableFraming();
                if(!ReferenceEquals(viewport,shipPanel.ShipViewport)||!table.GetProperty("interactive").GetBoolean()
                    ||table.GetProperty("sourceSha256").GetString()!=still.GetProperty("sourceSha256").GetString())
                    throw new InvalidOperationException("The holotable did not reuse the identical full model.");
                shipPanel.CloseHolotable();await Task.Delay(300);
                if(!ReferenceEquals(viewport,shipPanel.ShipViewport)||viewport.IsDisposed||viewport.IsHolotable||shipPanel.ShowcaseRotationEnabled)
                    throw new InvalidOperationException("Closing the holotable lost the viewport or paused preference.");
                shipPanel.ToggleRotationControlForReview();await Task.Delay(300);
                var resumeBefore=await viewport.DiagnoseAsync();await Task.Delay(650);var resumed=await viewport.DiagnoseAsync();
                if(!shipPanel.ShowcaseRotationEnabled||SystemParameters.ClientAreaAnimation&&resumed.GetProperty("rotationRadians").GetDouble()==resumeBefore.GetProperty("rotationRadians").GetDouble())
                    throw new InvalidOperationException("The showcase resume control did not restart rotation.");
                foreach(var size in new[]{new Size(1920,540),new Size(620,960),new Size(1280,800)})
                {Width=size.Width;Height=size.Height;UpdateLayout();await Task.Delay(200);shipPanel.CheckRotationControl();shipPanel.CheckBindings();}
                shipPanel.SetActive(false);await Task.Delay(300);var hidden=await viewport.DiagnoseAsync();await Task.Delay(450);
                var hiddenAfter=await viewport.DiagnoseAsync();
                if(hiddenAfter.GetProperty("motion").GetBoolean()||hiddenAfter.GetProperty("frames").GetInt64()!=hidden.GetProperty("frames").GetInt64())
                    throw new InvalidOperationException("Inactive showcase continued rotating.");
                shipPanel.SetActive(true);SaveFrame(id+"-rotating");
                theme=PanelTheme.All.First(t=>!t.ShipConsole);ApplyTheme();
                if(shipPanel.HasShipViewport||!viewport.IsDisposed)throw new InvalidOperationException("Leaving ship themes retained the renderer.");
                rows.Add(new{Ship=id,DrawsPerAnimationSecond=drawRate,AnimationSeconds=animationSeconds,RotationDelta=angle,Rotating=rotating,Paused=still,Holotable=table,Resumed=resumed,Inactive=hiddenAfter});
            }
            if(readings.Any(pair=>pair.Key is not ("date" or "uptime" or "status")&&text[pair.Key].Text!=pair.Value))
                throw new InvalidOperationException("Showcase controls changed sensor readings.");
            theme=PanelTheme.Resolve("Rocinante");ApplyTheme();shipPanel.SetShowcaseRotation(true);ApplyGraphicsMode(originalGraphics);UpdateLayout();SaveDisplayState();
            File.WriteAllText(Path.Combine(output,"result.json"),JsonSerializer.Serialize(new{Passed=true,SavedData=true,ProcessId=Environment.ProcessId,
                Scope="Visible native saved-data rotation, direct pause/resume control, exact paused angle, shared holotable viewport, three layouts, inactive stop and disposal on leaving ship themes. Not LIVE GPU or installation proof.",Rows=rows},JsonOptions));
        }
        catch(Exception ex)
        {
            File.WriteAllText(Path.Combine(output,"result.json"),JsonSerializer.Serialize(new{Passed=false,Error=ex.ToString(),LastCadence=lastCadence,Rows=rows},JsonOptions));
            Environment.ExitCode=1;
        }
        finally {if(!Environment.GetCommandLineArgs().Contains("--leave-review-open")||Environment.ExitCode!=0)Close();}
        void SaveFrame(string name)
        {
            UpdateLayout();var image=new RenderTargetBitmap((int)Math.Ceiling(ActualWidth),(int)Math.Ceiling(ActualHeight),96,96,PixelFormats.Pbgra32);
            image.Render((Visual)Content);var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(image));
            using var stream=File.Create(Path.Combine(output,name+".png"));encoder.Save(stream);
        }
    }
}
