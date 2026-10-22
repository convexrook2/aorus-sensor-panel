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
        string output=Path.Combine(PanelPaths.Artifacts,protect?"rl-protected-native-check":"native-pixel-check");
        Directory.CreateDirectory(output);
        var rows=new List<object>();
        try
        {
            if(previewPath is null||!windowed||shipPanel is null)
                throw new InvalidOperationException("Native pixel check requires a windowed saved-data preview.");
            if(protect&&!Rookline.AssetProtection.AppAssets.Protected)throw new InvalidOperationException("Protected check requires assets.rl.");
            foreach(string selected in protect?new[]{"Pella","Rocinante"}:new[]{theme.Id})
            {
                if(protect){shipPanel.CloseHolotable();theme=PanelTheme.Resolve(selected);ApplyTheme();}
                foreach(var size in protect?new[]{new Size(1920,1080)}:new[]{new Size(1024,576),new Size(1920,1080)})
                {
                    Width=size.Width;Height=size.Height;UpdateLayout();await Task.Delay(400);
                    shipPanel.CloseHolotable();
                    if(!shipPanel.HasShipViewport)throw new InvalidOperationException("The showcase renderer is missing.");
                    await shipPanel.ShipViewport.WaitForReadyAsync();
                    var portrait=JsonSerializer.SerializeToElement(shipPanel.PortraitDiagnostics,JsonOptions);
                    var receipt=portrait.GetProperty("receipt");
                    if(receipt.GetProperty("width").GetInt32()<1024||receipt.GetProperty("height").GetInt32()<2048)
                        throw new InvalidOperationException("The prepared portrait is below its required source resolution.");
                    string prefix=(protect?selected+"-":"")+$"{size.Width:0}-";
                    rows.Add(new{View=prefix+"showcase",FallbackPortrait=portrait,Renderer=await shipPanel.ShipViewport.DiagnoseAsync(),LiveShipRenderer=shipPanel.HasShipViewport});
                    SaveNative(prefix+"showcase");
                    shipPanel.OpenHolotable();await shipPanel.ShipViewport.WaitForReadyAsync();UpdateLayout();
                    shipPanel.ShipViewport.Reset();await Task.Delay(600);
                    var renderer=await shipPanel.ShipViewport.DiagnoseAsync();
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
                    var idleBefore=await shipPanel.ShipViewport.DiagnoseAsync();await Task.Delay(700);
                    var idleAfter=await shipPanel.ShipViewport.DiagnoseAsync();
                    if(idleAfter.GetProperty("motion").GetBoolean()||idleAfter.GetProperty("frames").GetInt64()!=idleBefore.GetProperty("frames").GetInt64())
                        throw new InvalidOperationException("The default holotable did not stop drawing when idle.");
                    rows.Add(new{View=prefix+"holotable",Host=host,Renderer=renderer,IdleBefore=idleBefore,IdleAfter=idleAfter});
                    await shipPanel.ShipViewport.CaptureAsync(Path.Combine(output,prefix+"holotable-webview.png"));
                    SaveNative(prefix+"holotable");
                    var oldViewport=shipPanel.ShipViewport;shipPanel.CloseHolotable();
                    if(!ReferenceEquals(oldViewport,shipPanel.ShipViewport)||oldViewport.IsDisposed||oldViewport.IsHolotable)
                        throw new InvalidOperationException("The holotable failed to return the shared viewport to the showcase.");
                }
            }
            File.WriteAllText(Path.Combine(output,"result.json"),JsonSerializer.Serialize(new{Passed=true,SavedData=true,Protected=protect,Theme=theme.Id,Rows=rows,
                Evidence="Visible native saved-data showcase and holotable pixels, source-model provenance, idle holotable frames and shared viewport return; no live hardware or GPU-utilization proof"},JsonOptions));
        }
        catch(Exception ex)
        {
            File.WriteAllText(Path.Combine(output,"result.json"),JsonSerializer.Serialize(new{Passed=false,Error=ex.ToString(),Rows=rows},JsonOptions));
            Application.Current.Shutdown(1);
        }
        finally{Close();}

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
