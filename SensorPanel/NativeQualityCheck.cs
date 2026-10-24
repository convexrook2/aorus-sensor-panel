using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace SensorPanel;

internal sealed partial class PanelWindow
{
    // Explicit visible saved-data diagnostic only; not live sensor or GPU proof.
    private async Task CheckNativeQualityAsync()
    {
        bool protect=Environment.GetCommandLineArgs().Contains("--check-protected-assets");
        bool hologramReview=Environment.GetCommandLineArgs().Contains("--check-holographic-stage"),passed=false;
        string output=Path.Combine(PanelPaths.Artifacts,hologramReview?"holoview-hologram-check":protect?"rl-protected-native-check":"native-pixel-check");
        Directory.CreateDirectory(output);
        var rows=new List<object>();
        try
        {
            if(previewPath is null||!windowed||shipPanel is null)
                throw new InvalidOperationException("Native pixel check requires a windowed saved-data preview.");
            if(protect&&!Rookline.AssetProtection.AppAssets.Protected)throw new InvalidOperationException("Protected check requires assets.rl.");
            foreach(string selected in hologramReview?new[]{"Pella","Arboghast"}:protect?new[]{"Pella","Rocinante"}:new[]{theme.Id})
            {
                if(protect||hologramReview){shipPanel.CloseHolotable();theme=PanelTheme.Resolve(selected);ApplyTheme();}
                foreach(var size in protect?new[]{new Size(1920,1080)}:new[]{new Size(1024,576),new Size(1920,1080)})
                {
                    Width=size.Width;Height=size.Height;UpdateLayout();await Task.Delay(400);
                    shipPanel.CloseHolotable();
                    if(!shipPanel.HasShipViewport)throw new InvalidOperationException("The showcase renderer is missing.");
                    await shipPanel.ShipViewport.WaitForReadyAsync();
                    // JavaScript readiness can precede WPF's first composed ship frame.
                    // Settle this diagnostic capture; normal presentation is unchanged.
                    await Task.Delay(600);UpdateLayout();
                    var portrait=JsonSerializer.SerializeToElement(shipPanel.PortraitDiagnostics,JsonOptions);
                    var receipt=portrait.GetProperty("receipt");
                    if(receipt.GetProperty("width").GetInt32()<1024||receipt.GetProperty("height").GetInt32()<2048)
                        throw new InvalidOperationException("The prepared portrait is below its required source resolution.");
                    string prefix=(protect||hologramReview?selected+"-":"")+$"{size.Width:0}-";
                    var showcase=await shipPanel.ShipViewport.DiagnoseAsync();CheckExhaust(showcase);
                    rows.Add(new{View=prefix+"showcase",FallbackPortrait=portrait,Renderer=showcase,LiveShipRenderer=shipPanel.HasShipViewport});
                    SaveNative(prefix+"showcase");
                    shipPanel.OpenHolotable();await shipPanel.ShipViewport.WaitForReadyAsync();UpdateLayout();
                    shipPanel.ShipViewport.Reset();await Task.Delay(600);
                    var renderer=await shipPanel.ShipViewport.DiagnoseAsync();
                    CheckExhaust(renderer);
                    var host=JsonSerializer.SerializeToElement(shipPanel.ShipViewport.PixelDiagnostics(),JsonOptions);
                    if(!renderer.GetProperty("interactive").GetBoolean()||!renderer.GetProperty("holotable").GetBoolean()
                        ||renderer.GetProperty("sourceSha256").GetString()!=receipt.GetProperty("sourceSha256").GetString())
                        throw new InvalidOperationException("The interactive holotable does not use the portrait's identical full model.");
                    // Allow WPF's asynchronous capture to consume the arranged frame.
                    for(int attempt=0;attempt<12&&(Math.Abs(host.GetProperty("captureWidth").GetDouble()-renderer.GetProperty("width").GetDouble())>1
                        ||Math.Abs(host.GetProperty("captureHeight").GetDouble()-renderer.GetProperty("height").GetDouble())>1);attempt++)
                    {
                        shipPanel.ShipViewport.Reset();await Task.Delay(250);
                        renderer=await shipPanel.ShipViewport.DiagnoseAsync();
                        host=JsonSerializer.SerializeToElement(shipPanel.ShipViewport.PixelDiagnostics(),JsonOptions);
                    }
                    if(renderer.GetProperty("width").GetDouble()+1<host.GetProperty("screenWidth").GetDouble()
                        ||renderer.GetProperty("height").GetDouble()+1<host.GetProperty("screenHeight").GetDouble())
                        throw new InvalidOperationException("The model canvas is still being enlarged: "+prefix+"holotable");
                    await Task.Delay(400);
                    var idleBefore=await shipPanel.ShipViewport.DiagnoseAsync();var watch=System.Diagnostics.Stopwatch.StartNew();await Task.Delay(700);
                    var idleAfter=await shipPanel.ShipViewport.DiagnoseAsync();
                    double platformRate=(idleAfter.GetProperty("frames").GetInt64()-idleBefore.GetProperty("frames").GetInt64())/watch.Elapsed.TotalSeconds;
                    if(idleAfter.GetProperty("motion").GetBoolean()
                        ||idleAfter.GetProperty("rotationRadians").GetDouble()!=idleBefore.GetProperty("rotationRadians").GetDouble())
                        throw new InvalidOperationException("Hologram animation moved the inspected ship.");
                    var projected=idleAfter.GetProperty("hologram");
                    if(!projected.GetProperty("visible").GetBoolean()||projected.GetProperty("solidPlatform").GetBoolean()
                        ||projected.GetProperty("bounds").EnumerateObject().Any(p=>Math.Abs(p.Value.GetDouble())>=.99)
                        ||SystemParameters.ClientAreaAnimation&&(projected.GetProperty("elapsed").GetDouble()<=idleBefore.GetProperty("hologram").GetProperty("elapsed").GetDouble()
                        ||platformRate<=0||idleAfter.GetProperty("frameLimit").ValueKind!=JsonValueKind.Null&&platformRate>33))
                        throw new InvalidOperationException("The light platform did not animate independently within the selected cadence.");
                    var buttonStates=shipPanel.Children.OfType<Holotable>().Single().CheckControlStates();
                    rows.Add(new{View=prefix+"holotable",Host=host,Renderer=renderer,IdleBefore=idleBefore,IdleAfter=idleAfter,PlatformDrawRate=platformRate,ButtonStates=buttonStates});
                    await shipPanel.ShipViewport.CaptureAsync(Path.Combine(output,prefix+"holotable-webview.png"));
                    SaveNative(prefix+"holotable");
                    if(hologramReview&&size.Width==1920)
                    {
                        var viewport=shipPanel.ShipViewport;
                        shipPanel.SetHolotableRotation(true);await Task.Delay(200);SaveNative(prefix+"buttons-selected");
                        shipPanel.SetHolotableRotation(false);viewport.Reset();await Task.Delay(250);
                        viewport.SetHologramMotion(false);await Task.Delay(300);
                        var held=await viewport.DiagnoseAsync();await Task.Delay(500);var heldAfter=await viewport.DiagnoseAsync();
                        if(heldAfter.GetProperty("frames").GetInt64()!=held.GetProperty("frames").GetInt64()
                            ||heldAfter.GetProperty("hologram").GetProperty("elapsed").GetDouble()!=held.GetProperty("hologram").GetProperty("elapsed").GetDouble())
                            throw new InvalidOperationException("Pausing the hologram did not hold its view.");
                        viewport.SetHologramMotion(true);await Task.Delay(250);
                        shipPanel.SetActive(false);await Task.Delay(300);
                        var inactive=await viewport.DiagnoseAsync();await Task.Delay(500);var inactiveAfter=await viewport.DiagnoseAsync();
                        if(inactiveAfter.GetProperty("frames").GetInt64()!=inactive.GetProperty("frames").GetInt64()
                            ||inactiveAfter.GetProperty("hologram").GetProperty("animated").GetBoolean())
                            throw new InvalidOperationException("Inactive holoview kept drawing its light platform.");
                        shipPanel.SetActive(true);await Task.Delay(300);
                        var resumed=await viewport.DiagnoseAsync();
                        if(SystemParameters.ClientAreaAnimation&&resumed.GetProperty("hologram").GetProperty("elapsed").GetDouble()<=heldAfter.GetProperty("hologram").GetProperty("elapsed").GetDouble())
                            throw new InvalidOperationException("The hologram did not resume after becoming active.");
                        rows.Add(new{View=prefix+"hologram-lifecycle",Held=held,HeldAfter=heldAfter,Inactive=inactive,InactiveAfter=inactiveAfter,Resumed=resumed});
                        var beforeDrag=await viewport.DiagnoseAsync();viewport.Inspect(.35,.18,1.2);await Task.Delay(250);
                        var afterDrag=await viewport.DiagnoseAsync();
                        foreach(string key in new[]{"cameraPosition","cameraQuaternion","zoom"})
                            if(beforeDrag.GetProperty("framing").GetProperty(key).GetRawText()!=afterDrag.GetProperty("framing").GetProperty(key).GetRawText())
                                throw new InvalidOperationException("Dragging or zooming moved the platform's render camera: "+key);
                        if(beforeDrag.GetProperty("inspection").GetProperty("quaternion").GetRawText()==afterDrag.GetProperty("inspection").GetProperty("quaternion").GetRawText()
                            ||Math.Abs(afterDrag.GetProperty("inspection").GetProperty("zoom").GetDouble()-1.2)>.00001)
                            throw new InvalidOperationException("Dragging or zooming did not move only the ship.");
                        rows.Add(new{View=prefix+"ship-only-inspection",Before=beforeDrag,After=afterDrag});
                        viewport.Reset();await Task.Delay(200);
                        await viewport.StartMotionRecordingAsync();
                        for(int sample=0;sample<42;sample++){await Task.Delay(150);viewport.Inspect(.035,sample<21?-.005:.005,1);}
                        await viewport.SaveMotionRecordingAsync(Path.Combine(output,prefix+"hologram-orbit.webm"));
                        viewport.Reset();await Task.Delay(400);
                    }
                    if(!hologramReview&&selected=="Arboghast"&&size.Width==1920)
                    {
                        shipPanel.ShipViewport.SetInspectionStage(false);
                        shipPanel.ShipViewport.Inspect(0,.95,1);await Task.Delay(600);
                        rows.Add(new{View=prefix+"drive-end",Renderer=await shipPanel.ShipViewport.DiagnoseAsync()});
                        await shipPanel.ShipViewport.CaptureAsync(Path.Combine(output,prefix+"drive-end-webview.png"));
                        SaveNative(prefix+"drive-end");
                        shipPanel.ShipViewport.Inspect(Math.PI,0,1);await Task.Delay(600);
                        rows.Add(new{View=prefix+"opposite-drive-end",Renderer=await shipPanel.ShipViewport.DiagnoseAsync()});
                        SaveNative(prefix+"opposite-drive-end");
                    }
                    var oldViewport=shipPanel.ShipViewport;shipPanel.CloseHolotable();
                    if(!ReferenceEquals(oldViewport,shipPanel.ShipViewport)||oldViewport.IsDisposed||oldViewport.IsHolotable)
                        throw new InvalidOperationException("The holotable failed to return the shared viewport to the showcase.");
                }
            }
            if(hologramReview&&Environment.GetCommandLineArgs().Contains("--leave-review-open"))
            {shipPanel.OpenHolotable();await shipPanel.ShipViewport.WaitForReadyAsync();UpdateLayout();}
            File.WriteAllText(Path.Combine(output,"result.json"),JsonSerializer.Serialize(new{Passed=true,SavedData=true,Protected=Rookline.AssetProtection.AppAssets.Protected,ProtectedReferenceSuite=protect,Theme=theme.Id,Rows=rows,
                ProcessId=Environment.ProcessId,Evidence="Visible native saved-data showcase and holographic holotable pixels, source-model provenance, independently animated platform with a stationary ship, pause/inactive freeze and shared viewport return; no live hardware or GPU-utilization proof"},JsonOptions));
            passed=true;
        }
        catch(Exception ex)
        {
            File.WriteAllText(Path.Combine(output,"result.json"),JsonSerializer.Serialize(new{Passed=false,Error=ex.ToString(),Rows=rows},JsonOptions));
            Application.Current.Shutdown(1);
        }
        finally{if(!passed||!Environment.GetCommandLineArgs().Contains("--leave-review-open"))Close();}

        static void CheckExhaust(JsonElement renderer)
        {
            string? id=renderer.GetProperty("id").GetString();
            if(id is not ("Arboghast" or "Knight"))return;
            int drives=id=="Knight"?3:4,lamps=id=="Knight"?17:8;
            var effect=renderer.GetProperty("engineEffects");
            if(effect.ValueKind!=JsonValueKind.Object||effect.GetProperty("engineCount").GetInt32()!=drives
                ||effect.GetProperty("visiblePlumes").GetInt32()!=drives||!effect.GetProperty("burning").GetBoolean()
                ||effect.GetProperty("activeSourceLights").GetInt32()!=lamps)
                throw new InvalidOperationException($"{id} must retain {drives} drive plumes and {lamps} source work lights.");
        }

        void SaveNative(string name)
        {
            UpdateLayout();
            var bitmap=new RenderTargetBitmap((int)Math.Ceiling(ActualWidth),(int)Math.Ceiling(ActualHeight),96,96,PixelFormats.Pbgra32);
            bitmap.Render((Visual)Content);
            var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using var stream=File.Create(Path.Combine(output,name+"-native.png"));encoder.Save(stream);
        }
    }
}
