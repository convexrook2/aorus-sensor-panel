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
            const string stateScript="({ship:document.body.dataset.loaded,ready:document.querySelector('#status').hidden,status:document.querySelector('#status').textContent,quality:document.body.dataset.quality,technical:document.querySelector('#technical').textContent,sourceSha256:document.body.dataset.modelSha256,orientation:document.body.dataset.orientation,sourceLights:Number(document.body.dataset.shipLights||0),engines:Number(document.body.dataset.engineCount||0),plumes:document.body.dataset.plumes==='true',frames:Number(document.querySelector('#motion').dataset.frames||0),angle:document.querySelector('#motion').dataset.cameraAngle})";
            foreach(string ship in new[]{"Guanshiyin","Pella","Scirocco","RocinanteS6","Arboghast"})
            {
                await browser.ExecuteScriptAsync($"document.querySelector('#ship').value='{ship}';document.querySelector('#ship').dispatchEvent(new Event('change'));");
                JsonElement sample=default;
                for(int attempt=0;attempt<120;attempt++)
                {
                    await Task.Delay(500);
                    sample=JsonSerializer.Deserialize<JsonElement>(await browser.ExecuteScriptAsync(stateScript));
                    if(sample.TryGetProperty("ship",out var id)&&id.GetString()==ship&&sample.GetProperty("ready").GetBoolean())break;
                }
                if(!sample.TryGetProperty("ship",out var loaded)||loaded.GetString()!=ship||!sample.GetProperty("ready").GetBoolean()||sample.GetProperty("quality").GetString()!="full")throw new InvalidOperationException("Protected full model did not load: "+ship+"; state: "+sample.GetRawText());
                JsonElement? exhaust=null;
                if(ship=="Guanshiyin")
                {
                    const string corrected="1a5ec3894f687c5effbb58808224639e8ce6539dc290625c79d67f681615a017";
                    int restoredAreas=JsonSerializer.Deserialize<int>(await browser.ExecuteScriptAsync("Number(document.body.dataset.sourceAreaLights||0)"));
                    if(restoredAreas!=31||sample.GetProperty("sourceSha256").GetString()!=corrected
                        ||sample.GetProperty("sourceLights").GetInt32()!=31||sample.GetProperty("engines").GetInt32()!=1
                        ||!sample.GetProperty("technical").GetString()!.Contains("3,174,446"))
                        throw new InvalidOperationException("Guanshiyin full gold mast wrap103, full geometry, source lights or raised drive differ; restored areas="+restoredAreas+"; state="+sample.GetRawText());
                    await browser.ExecuteScriptAsync("document.querySelector('#lighting').value='studio';document.querySelector('#lighting').dispatchEvent(new Event('change'));");
                    foreach(var pose in new[]{
                        (Name:"bow-gold-port",Eye:"[14,15,5]"),
                        (Name:"bow-gold-starboard",Eye:"[-14,15,5]")})
                    {
                        await browser.ExecuteScriptAsync($"window.fleetPlumeReview.sourceView({pose.Eye},[0,7.3,0.35],5);");
                        await Task.Delay(800);
                        using var capture=File.Create(Path.Combine(output,"Guanshiyin-"+pose.Name+".png"));
                        await browser.CoreWebView2.CapturePreviewAsync(CoreWebView2CapturePreviewImageFormat.Png,capture);
                    }
                    foreach(var pose in new[]{
                        (Name:"dual-rails-port",Eye:"[14,15,5]"),
                        (Name:"dual-rails-starboard",Eye:"[-14,15,5]"),
                        (Name:"dual-rails-axial",Eye:"[0,25,-0.1]")})
                    {
                        await browser.ExecuteScriptAsync($"window.fleetPlumeReview.sourceView({pose.Eye},[0,7.3,-0.35],4.5);");
                        await Task.Delay(800);
                        using var capture=File.Create(Path.Combine(output,"Guanshiyin-"+pose.Name+".png"));
                        await browser.CoreWebView2.CapturePreviewAsync(CoreWebView2CapturePreviewImageFormat.Png,capture);
                    }
                    foreach(var pose in new[]{
                        (Name:"gold-shell-side",Eye:"[20,7.2,0.8]"),
                        (Name:"gold-shell-quarter",Eye:"[14,15,5]"),
                        (Name:"gold-shell-above",Eye:"[10,11,12]")})
                    {
                        await browser.ExecuteScriptAsync($"window.fleetPlumeReview.sourceView({pose.Eye},[0,7.2,0.65],5);");
                        await Task.Delay(800);
                        using var capture=File.Create(Path.Combine(output,"Guanshiyin-"+pose.Name+".png"));
                        await browser.CoreWebView2.CapturePreviewAsync(CoreWebView2CapturePreviewImageFormat.Png,capture);
                    }
                    await browser.ExecuteScriptAsync("document.querySelector('#ship-lights').click();");
                    var unlit=JsonSerializer.Deserialize<JsonElement>(await browser.ExecuteScriptAsync("({lights:Number(document.body.dataset.activeSourceLights),emitters:Number(document.body.dataset.activeEmitters)})"));
                    if(unlit.GetProperty("lights").GetInt32()!=0||unlit.GetProperty("emitters").GetInt32()!=0)
                        throw new InvalidOperationException("Guanshiyin light control did not disable source lamps and emitters.");
                    await browser.ExecuteScriptAsync("document.querySelector('#ship-lights').click();document.querySelector('#lighting').value='ship';document.querySelector('#lighting').dispatchEvent(new Event('change'));");
                    foreach(var pose in new[]{
                        (Name:"lighting-gills",Eye:"[24,12,-10]",Aim:"[0,1,-0.1]",Zoom:"3"),
                        (Name:"lighting-rear",Eye:"[25,-20,13]",Aim:"[0,-1,0.5]",Zoom:"1.5"),
                        (Name:"lighting-bow",Eye:"[14,18,5]",Aim:"[0,6.9,-0.2]",Zoom:"5")})
                    {
                        await browser.ExecuteScriptAsync($"window.fleetPlumeReview.sourceView({pose.Eye},{pose.Aim},{pose.Zoom});");
                        await Task.Delay(800);
                        using var capture=File.Create(Path.Combine(output,"Guanshiyin-"+pose.Name+".png"));
                        await browser.CoreWebView2.CapturePreviewAsync(CoreWebView2CapturePreviewImageFormat.Png,capture);
                    }
                    var gpu=JsonSerializer.Deserialize<JsonElement>(await browser.ExecuteScriptAsync("window.fleetPlumeReview.diagnose()"));
                    if(gpu.GetProperty("errors").GetArrayLength()!=0)throw new InvalidOperationException("Guanshiyin lighting produced WebGL errors: "+gpu.GetRawText());
                    await browser.ExecuteScriptAsync("document.querySelector('#lighting').value='studio';document.querySelector('#lighting').dispatchEvent(new Event('change'));document.querySelector('#plumes').click();");
                    await Task.Delay(600);
                    exhaust=JsonSerializer.Deserialize<JsonElement>(await browser.ExecuteScriptAsync(stateScript));
                    if(!exhaust.Value.GetProperty("plumes").GetBoolean())throw new InvalidOperationException("Guanshiyin registered drive plume did not enable.");
                    foreach(string pose in new[]{"hero","side","opposite","aft","nose","rear"})
                    {
                        await browser.ExecuteScriptAsync($"document.querySelector('[data-view={pose}]')?.click();");
                        await Task.Delay(600);
                        using var capture=File.Create(Path.Combine(output,"Guanshiyin-"+pose+".png"));
                        await browser.CoreWebView2.CapturePreviewAsync(CoreWebView2CapturePreviewImageFormat.Png,capture);
                    }
                    await browser.ExecuteScriptAsync("document.querySelector('[data-view=hero]').click();");
                }
                if(ship is "Pella" or "Scirocco")
                {
                    string accepted=ship=="Pella"
                        ? "df080b0037ceb485ad4c8005e63c1a4b471171c60bf276ae3ea8ef365cce6d78"
                        : "7cbe6ab1719ded37da2de370a51072ccc9c252b82f26097c7b8b145659563320";
                    int lights=ship=="Pella"?34:14;
                    if(sample.GetProperty("sourceSha256").GetString()!=accepted
                        ||sample.GetProperty("sourceLights").GetInt32()!=lights||sample.GetProperty("engines").GetInt32()!=4)
                        throw new InvalidOperationException(ship+" accepted model, lights or four registered drives differ.");
                    await browser.ExecuteScriptAsync("if(document.querySelector('#plumes').getAttribute('aria-pressed')!=='true')document.querySelector('#plumes').click();");
                    await Task.Delay(500);
                    exhaust=JsonSerializer.Deserialize<JsonElement>(await browser.ExecuteScriptAsync(stateScript));
                    if(!exhaust.Value.GetProperty("plumes").GetBoolean())throw new InvalidOperationException(ship+" plume control did not enable the exhaust.");
                }
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
        catch(Exception ex){
            if(browser?.CoreWebView2 is { } core){using var image=File.Create(Path.Combine(output,"failure.png"));await core.CapturePreviewAsync(CoreWebView2CapturePreviewImageFormat.Png,image);}
            File.WriteAllText(Path.Combine(output,"result.json"),JsonSerializer.Serialize(new{Passed=false,Error=ex.ToString(),Rows=rows}));
        }
        finally{Close();}
    }
}
