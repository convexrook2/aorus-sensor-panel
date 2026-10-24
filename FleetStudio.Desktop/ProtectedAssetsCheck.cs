using System.IO;
using System.Text.Json;
using System.Security.Cryptography;
using Microsoft.Web.WebView2.Core;

namespace FleetStudio.Desktop;

internal sealed partial class StudioWindow
{
    // Explicit, visible publish check. Never runs during ordinary use.
    private async Task CheckProtectedAsync()
    {
        if(motionCheck){await CheckFleetPlumeMotionAsync();return;}
        if(plumeCheck){await CheckFleetPlumeLightingAsync();return;}
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
            const string stateScript="({ship:document.body.dataset.loaded,ready:document.querySelector('#status').hidden,quality:document.body.dataset.quality,technical:document.querySelector('#technical').textContent,sourceSha256:document.body.dataset.modelSha256,orientation:document.body.dataset.orientation,sourceLights:Number(document.body.dataset.shipLights||0),engines:Number(document.body.dataset.engineCount||0),plumes:document.body.dataset.plumes==='true',frames:Number(document.querySelector('#motion').dataset.frames||0),angle:document.querySelector('#motion').dataset.cameraAngle})";
            foreach(string ship in new[]{"Pella","RocinanteS6","Arboghast"})
            {
                await browser.ExecuteScriptAsync($"document.querySelector('#ship').value='{ship}';document.querySelector('#ship').dispatchEvent(new Event('change'));");
                JsonElement sample=default;
                for(int attempt=0;attempt<120;attempt++)
                {
                    await Task.Delay(500);
                    sample=JsonSerializer.Deserialize<JsonElement>(await browser.ExecuteScriptAsync(stateScript));
                    if(sample.TryGetProperty("ship",out var id)&&id.GetString()==ship&&sample.GetProperty("ready").GetBoolean())break;
                }
                if(!sample.TryGetProperty("ship",out var loaded)||loaded.GetString()!=ship||!sample.GetProperty("ready").GetBoolean()||sample.GetProperty("quality").GetString()!="full")throw new InvalidOperationException("Protected full model did not load: "+ship);
                JsonElement? exhaust=null;
                if(ship=="Arboghast")
                {
                    if(sample.GetProperty("sourceSha256").GetString()!="10744079e7ee154daac7345bac020ca6f0bf6c256032c9636244baa461333778"
                        ||sample.GetProperty("orientation").GetString()!="survey-disc"||sample.GetProperty("sourceLights").GetInt32()!=8||sample.GetProperty("engines").GetInt32()!=4)
                        throw new InvalidOperationException("Arboghast model, orientation, work lights or registered drives differ from pass08.");
                    await browser.ExecuteScriptAsync("if(document.querySelector('#plumes').getAttribute('aria-pressed')!=='true')document.querySelector('#plumes').click();");
                    await Task.Delay(500);
                    exhaust=JsonSerializer.Deserialize<JsonElement>(await browser.ExecuteScriptAsync(stateScript));
                    if(!exhaust.Value.GetProperty("plumes").GetBoolean())throw new InvalidOperationException("Arboghast plume control did not enable the exhaust.");
                }
                await browser.ExecuteScriptAsync("if(document.querySelector('#rotate').getAttribute('aria-pressed')!=='true')document.querySelector('#rotate').click();");
                await Task.Delay(1500);
                var resumed=JsonSerializer.Deserialize<JsonElement>(await browser.ExecuteScriptAsync("({frames:Number(document.querySelector('#motion').dataset.frames||0),angle:document.querySelector('#motion').dataset.cameraAngle})"));
                if(resumed.GetProperty("frames").GetInt64()<=sample.GetProperty("frames").GetInt64()||resumed.GetProperty("angle").GetString()==sample.GetProperty("angle").GetString())throw new InvalidOperationException("Protected rotation did not advance.");
                rows.Add(new{Ship=ship,Loaded=sample,Rotating=resumed,Exhaust=exhaust});
                using(var image=File.Create(Path.Combine(output,ship+".png")))
                    await browser.CoreWebView2.CapturePreviewAsync(CoreWebView2CapturePreviewImageFormat.Png,image);
                if(ship=="Arboghast")
                {
                    await browser.ExecuteScriptAsync("if(document.querySelector('#rotate').getAttribute('aria-pressed')==='true')document.querySelector('#rotate').click();document.querySelector('[data-view=aft]').click();");
                    await Task.Delay(600);
                    using var drive=File.Create(Path.Combine(output,ship+"-drive-end.png"));
                    await browser.CoreWebView2.CapturePreviewAsync(CoreWebView2CapturePreviewImageFormat.Png,drive);
                }
            }
            File.WriteAllText(Path.Combine(output,"result.json"),JsonSerializer.Serialize(new{Passed=true,Protected=true,Executable=Environment.ProcessPath,
                Version=typeof(App).Assembly.GetName().Version?.ToString(),ArchiveSha256=Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory,"assets.rl")))),Rows=rows},new JsonSerializerOptions{WriteIndented=true}));
        }
        catch(Exception ex){File.WriteAllText(Path.Combine(output,"result.json"),JsonSerializer.Serialize(new{Passed=false,Error=ex.ToString(),Rows=rows}));}
        finally{Close();}
    }
}
