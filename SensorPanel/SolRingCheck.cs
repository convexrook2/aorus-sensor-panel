using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace SensorPanel;

internal sealed partial class PanelWindow
{
    // Explicit visible diagnostic; normal monitoring has no extra timers/work.
    private async Task CheckSolRingAsync()
    {
        string output=Path.Combine(PanelPaths.Artifacts,"sol-ring-native-check");
        Directory.CreateDirectory(output);
        var rows=new List<object>();
        try
        {
            if(previewPath is null || !windowed || shipPanel is null)
                throw new InvalidOperationException("Sol Ring check requires a windowed saved-data preview.");
            theme=PanelTheme.Resolve("SolRing");ApplyTheme();Width=1280;Height=800;UpdateLayout();
            if(!shipPanel.HasShipViewport)throw new InvalidOperationException("The gate showcase renderer is missing.");
            if(shipPanel.FeaturedShipId!="SolRing" || shipPanel.NavigationFocus!="SolGate" || !shipPanel.HolotableAvailable
                || !ExpanseMedia.Ships.Contains("SolRing"))throw new InvalidOperationException("Gate selector/theme/navigation routing failed.");
            foreach(bool table in new[]{false,true})
            {
                if(!table){rows.Add(new{View="showcase",Renderer=await shipPanel.ShipViewport.DiagnoseAsync()});continue;}
                if(table)shipPanel.OpenHolotable();else shipPanel.CloseHolotable();
                await shipPanel.ShipViewport.WaitForReadyAsync();shipPanel.SetHolotableRotation(true);await Task.Delay(700);
                var before=await shipPanel.ShipViewport.DiagnoseAsync();
                await Task.Delay(1300);var after=await shipPanel.ShipViewport.DiagnoseAsync();
                CheckGate(after);
                if(SystemParameters.ClientAreaAnimation && (after.GetProperty("frames").GetInt64()<=before.GetProperty("frames").GetInt64()
                    || after.GetProperty("aperture").GetProperty("time").GetDouble()<=before.GetProperty("aperture").GetProperty("time").GetDouble()))
                    throw new InvalidOperationException("Gate rotation/aperture did not advance.");
                shipPanel.CheckBindings();if(table)shipPanel.CheckHolotableFraming();
                rows.Add(new{View=table?"holotable":"monitor",Before=before,After=after});
                await shipPanel.ShipViewport.CaptureAsync(Path.Combine(output,table?"holotable-webview.png":"monitor-webview.png"));
                UpdateLayout();var bitmap=new RenderTargetBitmap((int)Math.Ceiling(ActualWidth),(int)Math.Ceiling(ActualHeight),96,96,PixelFormats.Pbgra32);
                bitmap.Render((Visual)Content);var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));
                using var stream=File.Create(Path.Combine(output,table?"holotable-native.png":"monitor-native.png"));encoder.Save(stream);
            }
            shipPanel.SetHolotableRotation(false);await Task.Delay(300);
            var paused=await shipPanel.ShipViewport.DiagnoseAsync();await Task.Delay(500);
            var still=await shipPanel.ShipViewport.DiagnoseAsync();
            if(still.GetProperty("aperture").GetProperty("time").GetDouble()!=paused.GetProperty("aperture").GetProperty("time").GetDouble())
                throw new InvalidOperationException("Aperture animated while motion was disabled.");
            shipPanel.SetHolotableRotation(true);WindowState=WindowState.Minimized;await Task.Delay(700);
            var hidden=await shipPanel.ShipViewport.DiagnoseAsync();await Task.Delay(700);
            var later=await shipPanel.ShipViewport.DiagnoseAsync();
            if(later.GetProperty("active").GetBoolean()||later.GetProperty("frames").GetInt64()!=hidden.GetProperty("frames").GetInt64()
                ||later.GetProperty("aperture").GetProperty("time").GetDouble()!=hidden.GetProperty("aperture").GetProperty("time").GetDouble())
                throw new InvalidOperationException("Minimized gate continued drawing/animating.");
            WindowState=WindowState.Normal;await Task.Delay(900);CheckGate(await shipPanel.ShipViewport.DiagnoseAsync());
            shipPanel.CloseHolotable();
            foreach(string other in new[]{"Pella","Rocinante","SolRing"})
            {
                shipPanel.CloseHolotable();theme=PanelTheme.Resolve(other);ApplyTheme();shipPanel.OpenHolotable();
                await shipPanel.ShipViewport.WaitForReadyAsync();await Task.Delay(300);
                var reloaded=await shipPanel.ShipViewport.DiagnoseAsync();shipPanel.CheckBindings();
                if(other=="SolRing")CheckGate(reloaded);
                else if(reloaded.GetProperty("orientation").GetString()!="ship" || reloaded.GetProperty("aperture").ValueKind!=JsonValueKind.Null)
                    throw new InvalidOperationException("Gate rendering leaked into another ship.");
                rows.Add(new{View="switch-back-"+other,Renderer=reloaded});
            }
            File.WriteAllText(Path.Combine(output,"result.json"),JsonSerializer.Serialize(new{Passed=true,SavedData=true,
                Protected=Rookline.AssetProtection.AppAssets.Protected,Rows=rows,Paused=still,Minimized=later},JsonOptions));
        }
        catch(Exception ex){Environment.ExitCode=1;File.WriteAllText(Path.Combine(output,"result.json"),JsonSerializer.Serialize(new{Passed=false,Error=ex.ToString(),Rows=rows},JsonOptions));}
        finally{Close();}

        static void CheckGate(JsonElement data)
        {
            if(data.GetProperty("id").GetString()!="SolRing" || data.GetProperty("orientation").GetString()!="gate"
                ||data.GetProperty("sourceSha256").GetString()!="c80193dd8327d6bda2a55664f34617b2910130f6e961ef772dc23293599283d1"
                ||data.GetProperty("triangles").GetInt32()!=282272 || !data.GetProperty("aperture").GetProperty("doubleSided").GetBoolean()
                || !data.GetProperty("aperture").GetProperty("visible").GetBoolean() || Math.Abs(data.GetProperty("gateNormal")[1].GetDouble())>.001)
                throw new InvalidOperationException("Gate fidelity, upright orientation or aperture failed.");
        }
    }
}
