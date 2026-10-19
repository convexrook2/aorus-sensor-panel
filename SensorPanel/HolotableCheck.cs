using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace SensorPanel;

// Explicit, owner-visible live review flow. No timer or work in normal runs.
internal sealed partial class PanelWindow
{
    private async Task CheckLiveHolotableAsync(CancellationToken token)
    {
        string path=Path.Combine(PanelPaths.Artifacts,"live-holotable-check.json");
        var started=DateTimeOffset.UtcNow;
        var checks=new List<object>();
        double originalWidth=Width,originalHeight=Height;
        try
        {
            if(shipPanel is null || !theme.ShipConsole || !shipPanel.HolotableAvailable)
                throw new InvalidOperationException("Select a full-model ship for the live holotable check.");
            string? expectedHash=null;
            async Task Record(string phase)
            {
                UpdateLayout();
                var deadline=DateTimeOffset.UtcNow.AddSeconds(12);
                JsonElement renderer;
                do
                {
                    await Task.Delay(200,token);
                    renderer=await shipPanel.ShipViewport.DiagnoseAsync();
                    double aspect=shipPanel.ShipViewport.ActualWidth/shipPanel.ShipViewport.ActualHeight;
                    var frame=renderer.GetProperty("framing");
                    if(renderer.GetProperty("holotable").GetBoolean() && !renderer.GetProperty("interactive").GetBoolean()
                        && Math.Abs(frame.GetProperty("projectionAspect").GetDouble()-aspect)<.015)break;
                    if(DateTimeOffset.UtcNow>deadline)throw new TimeoutException("Holotable layout did not settle: "+phase);
                }while(true);
                shipPanel.CheckHolotableFraming();
                var framing=renderer.GetProperty("framing");
                foreach(string centre in new[]{"centreNdc","boundsCentreNdc"})
                    foreach(string axis in new[]{"x","y"})
                        if(Math.Abs(framing.GetProperty(centre).GetProperty(axis).GetDouble())>(centre=="centreNdc"?.005:.08))
                            throw new InvalidOperationException("Ship moved off-centre: "+phase+" / "+centre+" / "+axis);
                if(framing.GetProperty("panEnabled").GetBoolean())throw new InvalidOperationException("Panning can move the ship off-centre.");
                if(!renderer.GetProperty("displayStage").GetBoolean() || renderer.GetProperty("frameLimit").GetInt32()!=30)
                    throw new InvalidOperationException("The passive stage or its render limit is missing.");
                string? hash=renderer.GetProperty("sourceSha256").GetString();expectedHash??=hash;
                if(hash!=expectedHash)throw new InvalidOperationException("The model changed during holotable review.");
                checks.Add(new{Phase=phase,Renderer=renderer});
            }
            shipPanel.CheckHolotable();
            await Record("first-open-without-reset");
            await shipPanel.ShipViewport.CaptureAsync(Path.Combine(PanelPaths.Artifacts,"live-holotable-first-open.png"));
            await shipPanel.ShipViewport.StartRenderProfileAsync();
            var turningBefore=await shipPanel.ShipViewport.DiagnoseAsync();
            var rotationWatch=System.Diagnostics.Stopwatch.StartNew();
            await Task.Delay(3000,token);
            var turningAfter=await shipPanel.ShipViewport.DiagnoseAsync();
            if(turningAfter.GetProperty("renderProfile").GetProperty("state").GetString()!="complete")
                throw new InvalidOperationException("The requested renderer profile did not complete.");
            double renderedFps=(turningAfter.GetProperty("frames").GetInt64()-turningBefore.GetProperty("frames").GetInt64())/rotationWatch.Elapsed.TotalSeconds;
            if(turningAfter.GetProperty("animationSeconds").GetDouble()-turningBefore.GetProperty("animationSeconds").GetDouble()<1
                || Math.Abs(turningAfter.GetProperty("rotationRadians").GetDouble()-turningBefore.GetProperty("rotationRadians").GetDouble())<.1
                || renderedFps<10 || renderedFps>33)
                throw new InvalidOperationException("The passive holotable did not rotate within its 30 fps budget.");
            checks.Add(new{Phase="automatic-rotation",RenderedFps=renderedFps,Before=turningBefore,After=turningAfter});
            await shipPanel.ShipViewport.CaptureAsync(Path.Combine(PanelPaths.Artifacts,"live-holotable-rotated.png"));
            shipPanel.CloseHolotable();await Task.Delay(300,token);
            shipPanel.CheckHolotable();await Record("reopen-without-reset");
            Width=880;Height=620;await Record("landscape-resize");
            Width=680;Height=860;await Record("portrait-resize");
            Width=originalWidth;Height=originalHeight;await Record("restore-original-size");
            WindowState=WindowState.Minimized;await Task.Delay(600,token);
            var minimizedBefore=await shipPanel.ShipViewport.DiagnoseAsync();
            await Task.Delay(900,token);
            var minimizedAfter=await shipPanel.ShipViewport.DiagnoseAsync();
            if(minimizedAfter.GetProperty("active").GetBoolean()
                || minimizedAfter.GetProperty("frames").GetInt64()!=minimizedBefore.GetProperty("frames").GetInt64())
                throw new InvalidOperationException("The minimized showcase continued rendering.");
            checks.Add(new{Phase="minimized-rendering-stopped",Before=minimizedBefore,After=minimizedAfter});
            WindowState=WindowState.Normal;await Task.Delay(800,token);await Record("restored-after-minimize");
            await shipPanel.ShipViewport.CaptureAsync(Path.Combine(PanelPaths.Artifacts,"live-holotable-centred.png"));
            var bitmap=new RenderTargetBitmap((int)Math.Ceiling(ActualWidth),(int)Math.Ceiling(ActualHeight),96,96,PixelFormats.Pbgra32);
            bitmap.Render((Visual)Content);
            var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using(var stream=File.Create(Path.Combine(PanelPaths.Artifacts,"live-holotable-layout.png")))encoder.Save(stream);
            File.WriteAllText(path,JsonSerializer.Serialize(new{Passed=true,StartedAt=started,CompletedAt=DateTimeOffset.UtcNow,
                Preview=false,ProcessId=Environment.ProcessId,Theme=theme.Id,Checks=checks,
                Evidence="Visible native live showcase: programmatic open/reopen/resize, automatic rotation, minimize/restore; no manual model interaction"},JsonOptions));
        }
        catch(Exception ex)
        {
            WindowState=WindowState.Normal;Width=originalWidth;Height=originalHeight;
            File.WriteAllText(path,JsonSerializer.Serialize(new{Passed=false,StartedAt=started,Error=ex.ToString(),Checks=checks},JsonOptions));
            throw;
        }
    }
}
