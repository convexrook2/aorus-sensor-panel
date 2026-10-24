using System.IO;
using System.Text.Json;
using Microsoft.Web.WebView2.Core;

namespace FleetStudio.Desktop;

internal sealed partial class StudioWindow
{
    private async Task CheckFleetPlumeLightingAsync()
    {
        string output=Path.Combine(AppContext.BaseDirectory,"plume-lighting-check");Directory.CreateDirectory(output);
        var rows=new List<object>();
        try
        {
            if(!Rookline.AssetProtection.AppAssets.Protected||browser is null)throw new InvalidOperationException("Protected desktop assets required.");
            for(int attempt=0;attempt<120;attempt++)
            {
                if(await browser.ExecuteScriptAsync("Boolean(window.fleetPlumeReview&&document.body.dataset.loaded&&document.querySelector('#status').hidden)")=="true")break;
                await Task.Delay(500);
            }
            using var catalogue=JsonDocument.Parse(Rookline.AssetProtection.AppAssets.Read("FleetStudio/catalog.json"));
            foreach(var entry in catalogue.RootElement.EnumerateArray())
            {
                string id=entry.GetProperty("id").GetString()!;
                await browser.ExecuteScriptAsync($"document.querySelector('#ship').value={JsonSerializer.Serialize(id)};document.querySelector('#ship').dispatchEvent(new Event('change'));");
                bool ready=false;
                for(int attempt=0;attempt<120;attempt++)
                {
                    await Task.Delay(500);
                    if(await browser.ExecuteScriptAsync($"document.body.dataset.loaded==={JsonSerializer.Serialize(id)}&&document.querySelector('#status').hidden")=="true"){ready=true;break;}
                }
                if(!ready)throw new InvalidOperationException("Model not ready: "+id);
                if(id=="SolRing")
                {
                    var gate=JsonSerializer.Deserialize<JsonElement>(await browser.ExecuteScriptAsync("window.fleetPlumeReview.state()"));
                    if(gate.GetProperty("engineCount").GetInt32()!=0)throw new InvalidOperationException("Gate acquired a drive effect.");
                    rows.Add(new{Ship=id,Gate=true,State=gate});continue;
                }
                await browser.ExecuteScriptAsync("if(document.querySelector('#rotate').getAttribute('aria-pressed')==='true')document.querySelector('#rotate').click();if(document.querySelector('#plumes').getAttribute('aria-pressed')!=='true')document.querySelector('#plumes').click();if(document.querySelector('#ship-lights').getAttribute('aria-pressed')==='true')document.querySelector('#ship-lights').click();document.querySelector('#lighting').value='ship';document.querySelector('#lighting').dispatchEvent(new Event('change'));window.fleetPlumeReview.freeze(true);");
                var measurements=new List<JsonElement>();
                foreach(string view in new[]{"hero","aft"})
                {
                    await browser.ExecuteScriptAsync($"document.querySelector('[data-view={view}]').click();");await Task.Delay(250);
                    measurements.Add(JsonSerializer.Deserialize<JsonElement>(await browser.ExecuteScriptAsync("window.fleetPlumeReview.measure()")));
                    await Task.Delay(150);
                    using var image=File.Create(Path.Combine(output,id+"-"+view+".png"));
                    await browser.CoreWebView2.CapturePreviewAsync(CoreWebView2CapturePreviewImageFormat.Png,image);
                }
                bool stealth=id=="Anubis";
                bool emission=stealth
                    ?measurements.All(x=>x.GetProperty("peakHdrRadiance").GetDouble() is >.01 and <1.05&&x.GetProperty("maxPlumeToHullRatio").GetDouble()<=.180001)
                    :measurements.Any(x=>x.GetProperty("peakHdrRadiance").GetDouble()>1.05&&x.GetProperty("emissivePixels").GetInt32()>0);
                bool lighting=measurements.Any(x=>x.GetProperty("hullLightPixels").GetInt32()>0&&x.GetProperty("hullLightEnergy").GetDouble()>.0001);
                bool sources=measurements.All(x=>x.GetProperty("activePlumeLights").GetInt32()==x.GetProperty("engineCount").GetInt32());
                rows.Add(new{Ship=id,Stealth=stealth,Emission=emission,HullIllumination=lighting,ActiveLights=sources,Views=measurements});
                await browser.ExecuteScriptAsync("if(document.querySelector('#ship-lights').getAttribute('aria-pressed')!=='true')document.querySelector('#ship-lights').click();document.querySelector('#lighting').value='studio';document.querySelector('#lighting').dispatchEvent(new Event('change'));document.querySelector('[data-view=hero]').click();");
                await Task.Delay(400);
                using(var studio=File.Create(Path.Combine(output,id+"-studio.png")))
                    await browser.CoreWebView2.CapturePreviewAsync(CoreWebView2CapturePreviewImageFormat.Png,studio);
                Console.WriteLine($"PLUMES: {id} emission={emission} hullLight={lighting} lights={sources}");
            }
            bool passed=rows.Count==catalogue.RootElement.GetArrayLength()&&rows.All(row=>
            {
                var item=JsonSerializer.SerializeToElement(row);return item.TryGetProperty("Gate",out _)
                    ||item.GetProperty("Emission").GetBoolean()&&item.GetProperty("HullIllumination").GetBoolean()&&item.GetProperty("ActiveLights").GetBoolean();
            });
            File.WriteAllText(Path.Combine(output,"result.json"),JsonSerializer.Serialize(new{Passed=passed,Protected=true,Executable=Environment.ProcessPath,Rows=rows,
                Evidence="Native WebView2 rendering; isolated floating-point plume radiance and real hull-light on/off difference, without changing source materials. No live sensors or GPU-budget proof."},new JsonSerializerOptions{WriteIndented=true}));
        }
        catch(Exception ex){File.WriteAllText(Path.Combine(output,"result.json"),JsonSerializer.Serialize(new{Passed=false,Error=ex.ToString(),Rows=rows}));}
        finally{Close();}
    }
}
