using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace SensorPanel;

internal sealed partial class PanelWindow
{
    // Explicit visible diagnostic only. No sensor access or normal-run overhead.
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
            await shipPanel.ShipViewport.WaitForReadyAsync();shipPanel.SetMotion(protect);
            foreach(string selected in protect?new[]{"Pella","Rocinante"}:new[]{theme.Id})
            {
            if(protect){shipPanel.CloseHolotable();theme=PanelTheme.Resolve(selected);ApplyTheme();shipPanel.SetMotion(true);}
            foreach(var size in protect?new[]{new Size(1920,1080)}:new[]{new Size(1024,576),new Size(1920,1080)})
            {
                Width=size.Width;Height=size.Height;UpdateLayout();
                await Task.Delay(700);
                foreach(bool table in new[]{false,true})
                {
                    if(table)shipPanel.OpenHolotable();else shipPanel.CloseHolotable();
                    await shipPanel.ShipViewport.WaitForReadyAsync();UpdateLayout();
                    shipPanel.ShipViewport.Reset();await Task.Delay(600);
                    string name=(protect?selected+"-":"")+$"{size.Width:0}-"+(table?"holotable":"portrait");
                    var renderer=await shipPanel.ShipViewport.DiagnoseAsync();
                    var host=JsonSerializer.SerializeToElement(shipPanel.ShipViewport.PixelDiagnostics(),JsonOptions);
                    if(protect)
                    {
                        await Task.Delay(700);var later=await shipPanel.ShipViewport.DiagnoseAsync();
                        if(!later.GetProperty("active").GetBoolean()||later.GetProperty("frames").GetInt64()<=renderer.GetProperty("frames").GetInt64())
                            throw new InvalidOperationException("Protected viewport rotation did not advance: "+name);
                    }
                    // Allow the asynchronous WPF capture to consume the new-size
                    // frame before judging a paused scene after reparenting.
                    for(int attempt=0;attempt<12&&(Math.Abs(host.GetProperty("captureWidth").GetDouble()-renderer.GetProperty("width").GetDouble())>1
                        ||Math.Abs(host.GetProperty("captureHeight").GetDouble()-renderer.GetProperty("height").GetDouble())>1);attempt++)
                    {
                        shipPanel.ShipViewport.Reset();await Task.Delay(250);
                        renderer=await shipPanel.ShipViewport.DiagnoseAsync();
                        host=JsonSerializer.SerializeToElement(shipPanel.ShipViewport.PixelDiagnostics(),JsonOptions);
                    }
                    rows.Add(new{View=name,Host=host,Renderer=renderer});
                    if(renderer.GetProperty("width").GetDouble()+1<host.GetProperty("screenWidth").GetDouble()
                        ||renderer.GetProperty("height").GetDouble()+1<host.GetProperty("screenHeight").GetDouble())
                        throw new InvalidOperationException("The model canvas is still being enlarged: "+name);
                    await shipPanel.ShipViewport.CaptureAsync(Path.Combine(output,name+"-webview.png"));
                    var bitmap=new RenderTargetBitmap((int)Math.Ceiling(ActualWidth),(int)Math.Ceiling(ActualHeight),96,96,PixelFormats.Pbgra32);
                    bitmap.Render((Visual)Content);
                    var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));
                    using var stream=File.Create(Path.Combine(output,name+"-native.png"));encoder.Save(stream);
                }
            }
            }
            File.WriteAllText(Path.Combine(output,"result.json"),JsonSerializer.Serialize(new{Passed=true,SavedData=true,Protected=protect,Theme=theme.Id,Rows=rows},JsonOptions));
        }
        catch(Exception ex)
        {File.WriteAllText(Path.Combine(output,"result.json"),JsonSerializer.Serialize(new{Passed=false,Error=ex.ToString(),Rows=rows},JsonOptions));}
        finally{Close();}
    }
}
