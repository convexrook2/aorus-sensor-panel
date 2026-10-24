using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace SensorPanel;

// Explicit, owner-visible LIVE review flow. No timer or work in normal runs.
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
            if(!shipPanel.HasShipViewport)throw new InvalidOperationException("The showcase renderer is missing.");
            var portrait=JsonSerializer.SerializeToElement(shipPanel.PortraitDiagnostics,JsonOptions);
            string? expectedHash=portrait.GetProperty("receipt").GetProperty("sourceSha256").GetString();
            checks.Add(new{Phase="showcase-before-open",FallbackPortrait=portrait,Renderer=await shipPanel.ShipViewport.DiagnoseAsync(),LiveShipRenderer=true});
            async Task<JsonElement> Record(string phase)
            {
                UpdateLayout();
                var deadline=DateTimeOffset.UtcNow.AddSeconds(15);
                JsonElement renderer;
                do
                {
                    await Task.Delay(200,token);
                    renderer=await shipPanel.ShipViewport.DiagnoseAsync();
                    double aspect=shipPanel.ShipViewport.ActualWidth/shipPanel.ShipViewport.ActualHeight;
                    if(renderer.GetProperty("holotable").GetBoolean() && renderer.GetProperty("interactive").GetBoolean()
                        && Math.Abs(renderer.GetProperty("framing").GetProperty("projectionAspect").GetDouble()-aspect)<.015)break;
                    if(DateTimeOffset.UtcNow>deadline)throw new TimeoutException("Holotable layout did not settle: "+phase);
                }while(true);
                shipPanel.CheckHolotableFraming();
                var framing=renderer.GetProperty("framing");
                foreach(string centre in new[]{"centreNdc","boundsCentreNdc"})
                    foreach(string axis in new[]{"x","y"})
                        if(Math.Abs(framing.GetProperty(centre).GetProperty(axis).GetDouble())>(centre=="centreNdc"?.005:.08))
                            throw new InvalidOperationException("Ship moved off-centre: "+phase+" / "+centre+" / "+axis);
                if(framing.GetProperty("panEnabled").GetBoolean())throw new InvalidOperationException("Panning can move the ship off-centre.");
                if(!renderer.GetProperty("displayStage").GetBoolean() || renderer.GetProperty("frameLimit") is var frameLimit
                    && frameLimit.ValueKind!=JsonValueKind.Null && frameLimit.GetInt32()!=30)
                    throw new InvalidOperationException("The full-detail stage or its optional animation limit is missing.");
                if(renderer.GetProperty("sourceSha256").GetString()!=expectedHash)
                    throw new InvalidOperationException("The holotable differs from the accepted portrait source.");
                checks.Add(new{Phase=phase,Renderer=renderer});return renderer;
            }
            shipPanel.CheckHolotable();
            await Record("first-open-without-reset");
            await shipPanel.ShipViewport.CaptureAsync(Path.Combine(PanelPaths.Artifacts,"live-holotable-first-open.png"));
            await Task.Delay(400,token);
            var idleBefore=await shipPanel.ShipViewport.DiagnoseAsync();
            await Task.Delay(900,token);
            var idleAfter=await shipPanel.ShipViewport.DiagnoseAsync();
            if(idleAfter.GetProperty("motion").GetBoolean()
                ||idleAfter.GetProperty("rotationRadians").GetDouble()!=idleBefore.GetProperty("rotationRadians").GetDouble()
                ||SystemParameters.ClientAreaAnimation&&idleAfter.GetProperty("hologram").GetProperty("elapsed").GetDouble()<=idleBefore.GetProperty("hologram").GetProperty("elapsed").GetDouble())
                throw new InvalidOperationException("The holographic platform did not animate independently of the stationary ship.");
            checks.Add(new{Phase="hologram-with-stationary-ship",Before=idleBefore,After=idleAfter});
            shipPanel.ShipViewport.Inspect(.25,.10,1.2);await Task.Delay(250,token);
            var inspected=await shipPanel.ShipViewport.DiagnoseAsync();
            if(inspected.GetProperty("framing").GetProperty("cameraPosition").GetRawText()!=idleAfter.GetProperty("framing").GetProperty("cameraPosition").GetRawText()
                ||inspected.GetProperty("framing").GetProperty("cameraQuaternion").GetRawText()!=idleAfter.GetProperty("framing").GetProperty("cameraQuaternion").GetRawText()
                ||inspected.GetProperty("inspection").GetProperty("quaternion").GetRawText()==idleAfter.GetProperty("inspection").GetProperty("quaternion").GetRawText()
                ||Math.Abs(inspected.GetProperty("inspection").GetProperty("zoom").GetDouble()-1.2)>.01)
                throw new InvalidOperationException("Inspection must rotate and zoom only the ship, keeping the projected platform's camera fixed.");
            checks.Add(new{Phase="programmatic-orbit-and-zoom",Renderer=inspected});
            shipPanel.ShipViewport.Reset();await Task.Delay(250,token);
            if(SystemParameters.ClientAreaAnimation)
            {
                shipPanel.SetHolotableRotation(true);
                await shipPanel.ShipViewport.StartRenderProfileAsync();
                var turningBefore=await shipPanel.ShipViewport.DiagnoseAsync();
                var watch=System.Diagnostics.Stopwatch.StartNew();
                await Task.Delay(3000,token);
                var turningAfter=await shipPanel.ShipViewport.DiagnoseAsync();
                double fps=(turningAfter.GetProperty("frames").GetInt64()-turningBefore.GetProperty("frames").GetInt64())/watch.Elapsed.TotalSeconds;
                if(turningAfter.GetProperty("renderProfile").GetProperty("state").GetString()!="complete"
                    ||turningAfter.GetProperty("animationSeconds").GetDouble()-turningBefore.GetProperty("animationSeconds").GetDouble()<1
                    ||Math.Abs(turningAfter.GetProperty("rotationRadians").GetDouble()-turningBefore.GetProperty("rotationRadians").GetDouble())<.1
                    ||fps<10 ||turningAfter.GetProperty("frameLimit").ValueKind!=JsonValueKind.Null&&fps>33)
                    throw new InvalidOperationException("Optional rotation did not advance within the selected rendering budget.");
                checks.Add(new{Phase="optional-automatic-rotation",RenderedFps=fps,Before=turningBefore,After=turningAfter});
                shipPanel.SetHolotableRotation(false);
            }
            var oldViewport=shipPanel.ShipViewport;
            shipPanel.CloseHolotable();await Task.Delay(300,token);
            if(!ReferenceEquals(oldViewport,shipPanel.ShipViewport)||oldViewport.IsDisposed||oldViewport.IsHolotable)
                throw new InvalidOperationException("Closing the holotable failed to return the shared renderer to the showcase.");
            checks.Add(new{Phase="close-returns-shared-renderer",OldViewportDisposed=oldViewport.IsDisposed,LiveShipRenderer=shipPanel.HasShipViewport});
            shipPanel.CheckHolotable();
            if(!ReferenceEquals(oldViewport,shipPanel.ShipViewport))throw new InvalidOperationException("Opening the holotable allocated another renderer.");
            await Record("reopen-with-shared-renderer");
            Width=880;Height=620;await Record("landscape-resize");
            Width=680;Height=860;await Record("portrait-resize");
            Width=originalWidth;Height=originalHeight;await Record("restore-original-size");
            WindowState=WindowState.Minimized;await Task.Delay(600,token);
            var minimizedBefore=await shipPanel.ShipViewport.DiagnoseAsync();
            await Task.Delay(900,token);
            var minimizedAfter=await shipPanel.ShipViewport.DiagnoseAsync();
            if(minimizedAfter.GetProperty("active").GetBoolean()
                ||minimizedAfter.GetProperty("frames").GetInt64()!=minimizedBefore.GetProperty("frames").GetInt64())
                throw new InvalidOperationException("The minimized holotable continued rendering.");
            checks.Add(new{Phase="minimized-rendering-stopped",Before=minimizedBefore,After=minimizedAfter});
            WindowState=WindowState.Normal;await Task.Delay(800,token);await Record("restored-after-minimize");
            await shipPanel.ShipViewport.CaptureAsync(Path.Combine(PanelPaths.Artifacts,"live-holotable-centred.png"));
            var bitmap=new RenderTargetBitmap((int)Math.Ceiling(ActualWidth),(int)Math.Ceiling(ActualHeight),96,96,PixelFormats.Pbgra32);
            bitmap.Render((Visual)Content);
            var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using(var stream=File.Create(Path.Combine(PanelPaths.Artifacts,"live-holotable-layout.png")))encoder.Save(stream);
            File.WriteAllText(path,JsonSerializer.Serialize(new{Passed=true,StartedAt=started,CompletedAt=DateTimeOffset.UtcNow,
                Preview=false,ProcessId=Environment.ProcessId,Theme=theme.Id,Checks=checks,
                Evidence="Visible native LIVE monitor: shared showcase/holotable viewport, programmatic orbit/zoom, optional holotable rotation, resize and minimize/restore; no manual-input or GPU-utilization proof"},JsonOptions));
        }
        catch(Exception ex)
        {
            WindowState=WindowState.Normal;Width=originalWidth;Height=originalHeight;
            File.WriteAllText(path,JsonSerializer.Serialize(new{Passed=false,StartedAt=started,Error=ex.ToString(),Checks=checks},JsonOptions));
            throw;
        }
    }
}
