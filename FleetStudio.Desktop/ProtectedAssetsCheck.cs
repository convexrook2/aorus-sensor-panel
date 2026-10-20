using System.IO;
using System.Text.Json;
using Microsoft.Web.WebView2.Core;

namespace FleetStudio.Desktop;

internal sealed partial class StudioWindow
{
    // Explicit, visible publish check. Never runs during ordinary use.
    private async Task CheckProtectedAsync()
    {
        string output=Path.Combine(AppContext.BaseDirectory,"protected-check");
        Directory.CreateDirectory(output);var rows=new List<object>();
        try
        {
            if(!Rookline.AssetProtection.AppAssets.Protected||browser is null)throw new InvalidOperationException("Protected assets required.");
            for(int attempt=0;attempt<120;attempt++)
            {
                if(await browser.ExecuteScriptAsync("Boolean(document.body.dataset.loaded&&document.querySelector('#status').hidden)")=="true")break;
                await Task.Delay(500);
            }
            foreach(string ship in new[]{"Pella","RocinanteS6"})
            {
                await browser.ExecuteScriptAsync($"document.querySelector('#ship').value='{ship}';document.querySelector('#ship').dispatchEvent(new Event('change'));");
                JsonElement sample=default;
                for(int attempt=0;attempt<120;attempt++)
                {
                    await Task.Delay(500);
                    sample=JsonSerializer.Deserialize<JsonElement>(await browser.ExecuteScriptAsync("({ship:document.body.dataset.loaded,ready:document.querySelector('#status').hidden,quality:document.body.dataset.quality,technical:document.querySelector('#technical').textContent,frames:Number(document.querySelector('#motion').dataset.frames||0),angle:document.querySelector('#motion').dataset.cameraAngle})"));
                    if(sample.TryGetProperty("ship",out var id)&&id.GetString()==ship&&sample.GetProperty("ready").GetBoolean())break;
                }
                if(!sample.TryGetProperty("ship",out var loaded)||loaded.GetString()!=ship||!sample.GetProperty("ready").GetBoolean()||sample.GetProperty("quality").GetString()!="full")throw new InvalidOperationException("Protected full model did not load: "+ship);
                await browser.ExecuteScriptAsync("if(document.querySelector('#rotate').getAttribute('aria-pressed')!=='true')document.querySelector('#rotate').click();");
                await Task.Delay(1500);
                var resumed=JsonSerializer.Deserialize<JsonElement>(await browser.ExecuteScriptAsync("({frames:Number(document.querySelector('#motion').dataset.frames||0),angle:document.querySelector('#motion').dataset.cameraAngle})"));
                if(resumed.GetProperty("frames").GetInt64()<=sample.GetProperty("frames").GetInt64()||resumed.GetProperty("angle").GetString()==sample.GetProperty("angle").GetString())throw new InvalidOperationException("Protected rotation did not advance.");
                rows.Add(new{Ship=ship,Loaded=sample,Rotating=resumed});
                using var image=File.Create(Path.Combine(output,ship+".png"));
                await browser.CoreWebView2.CapturePreviewAsync(CoreWebView2CapturePreviewImageFormat.Png,image);
            }
            File.WriteAllText(Path.Combine(output,"result.json"),JsonSerializer.Serialize(new{Passed=true,Protected=true,Rows=rows},new JsonSerializerOptions{WriteIndented=true}));
        }
        catch(Exception ex){File.WriteAllText(Path.Combine(output,"result.json"),JsonSerializer.Serialize(new{Passed=false,Error=ex.ToString(),Rows=rows}));}
        finally{Close();}
    }
}
