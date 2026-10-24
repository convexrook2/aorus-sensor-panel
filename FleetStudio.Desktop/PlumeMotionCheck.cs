using System.IO;
using System.Text.Json;
using Microsoft.Web.WebView2.Core;

namespace FleetStudio.Desktop;

internal sealed partial class StudioWindow
{
    // Moving-camera evidence, separately from brightness and hull-light checks.
    private async Task CheckFleetPlumeMotionAsync()
    {
        string output=Path.Combine(AppContext.BaseDirectory,"plume-motion-check");
        Directory.CreateDirectory(output);var rows=new List<object>();int gpuFaults=0;
        try
        {
            if(!Rookline.AssetProtection.AppAssets.Protected||browser is null)throw new InvalidOperationException("Protected native viewer required.");
            foreach(string name in new[]{"Log.entryAdded","Runtime.consoleAPICalled","Runtime.exceptionThrown"})
                browser.CoreWebView2.GetDevToolsProtocolEventReceiver(name).DevToolsProtocolEventReceived+=(_,e)=>
                {
                    string text=e.ParameterObjectAsJson;
                    File.AppendAllText(Path.Combine(output,"gpu-log.jsonl"),name+" "+text+Environment.NewLine);
                    if(name=="Runtime.exceptionThrown"||text.Contains("GL_INVALID_",StringComparison.Ordinal)||text.Contains("WebGL: INVALID_",StringComparison.Ordinal))gpuFaults++;
                };
            await browser.CoreWebView2.CallDevToolsProtocolMethodAsync("Log.enable","{}");
            await browser.CoreWebView2.CallDevToolsProtocolMethodAsync("Runtime.enable","{}");
            for(int attempt=0;attempt<120;attempt++)
            {
                if(await browser.ExecuteScriptAsync("Boolean(window.fleetPlumeReview&&document.body.dataset.loaded&&document.querySelector('#status').hidden)")=="true")break;
                await Task.Delay(500);
            }
            foreach(string id in new[]{"Anubis","Arboghast","RocinanteS6","Pella","Nauvoo"})
            {
                await browser.ExecuteScriptAsync($"document.querySelector('#ship').value={JsonSerializer.Serialize(id)};document.querySelector('#ship').dispatchEvent(new Event('change'));");
                bool ready=false;
                for(int attempt=0;attempt<120;attempt++)
                {
                    await Task.Delay(500);
                    if(await browser.ExecuteScriptAsync($"document.body.dataset.loaded==={JsonSerializer.Serialize(id)}&&document.querySelector('#status').hidden")=="true"){ready=true;break;}
                }
                if(!ready)throw new InvalidOperationException("Model not ready: "+id);
                var initial=JsonSerializer.Deserialize<JsonElement>(await browser.ExecuteScriptAsync("window.fleetPlumeReview.diagnose()"));
                if(initial.GetProperty("errors").GetArrayLength()!=0)throw new InvalidOperationException("GPU errors on model load: "+initial);
                await browser.ExecuteScriptAsync("if(document.querySelector('#plumes').getAttribute('aria-pressed')!=='true')document.querySelector('#plumes').click();");
                await Task.Delay(350);
                var enabled=JsonSerializer.Deserialize<JsonElement>(await browser.ExecuteScriptAsync("window.fleetPlumeReview.diagnose()"));
                if(enabled.GetProperty("errors").GetArrayLength()!=0||enabled.GetProperty("pendingPlumeShadows").GetInt32()!=0)
                    throw new InvalidOperationException("First plume toggle left invalid rendering/shadow resources: "+enabled);
                List<JsonElement>? palette=null;
                if(id=="Anubis")
                {
                    palette=new List<JsonElement>();
                    foreach(string view in new[]{"hero","aft"})
                    {
                        await browser.ExecuteScriptAsync($"document.querySelector('[data-view={view}]').click();");await Task.Delay(250);
                        palette.Add(JsonSerializer.Deserialize<JsonElement>(await browser.ExecuteScriptAsync("window.fleetPlumeReview.measure()")));
                    }
                    bool green=palette.All(x=>{var rgb=x.GetProperty("plumeRgbEnergy");return rgb[1].GetDouble()>rgb[0].GetDouble()*1.15&&rgb[1].GetDouble()>rgb[2].GetDouble()*1.1;});
                    if(!green||!palette.All(x=>x.GetProperty("peakHdrRadiance").GetDouble() is >.01 and <1.05
                        &&x.GetProperty("maxPlumeToHullRatio").GetDouble()<=.180001)||!palette.Any(x=>x.GetProperty("hullLightPixels").GetInt32()>0))
                        throw new InvalidOperationException("Anubis exhaust must be compact, green-tinged and subdued, with confined hull illumination: "+JsonSerializer.Serialize(palette));
                }
                var recordings=new List<JsonElement>();
                foreach(var phase in new[]{("orbit",12,true),("flow",3,false)})
                {
                    await browser.ExecuteScriptAsync($"window.fleetPlumeReview.recordOrbit({phase.Item2},{phase.Item3.ToString().ToLowerInvariant()})");
                    JsonElement state=default;
                    for(int attempt=0;attempt<100;attempt++)
                    {
                        await Task.Delay(500);
                        state=JsonSerializer.Deserialize<JsonElement>(await browser.ExecuteScriptAsync("window.fleetPlumeReview.recording()"));
                        if(state.GetProperty("status").GetString() is "complete" or "error")break;
                    }
                    if(state.GetProperty("status").GetString()!="complete"||state.GetProperty("frames").GetInt32()<phase.Item2*5)
                        throw new InvalidOperationException("Animation recording failed: "+state);
                    if(state.GetProperty("maxPlumeToHullRatio").GetDouble()>(id=="Anubis"?.180001:.600001))
                        throw new InvalidOperationException("Plume exceeds its hull-relative size limit: "+state);
                    string encoded=JsonSerializer.Deserialize<string>(await browser.ExecuteScriptAsync("window.fleetPlumeReview.takeVideo()"))!;
                    File.WriteAllBytes(Path.Combine(output,id+"-"+phase.Item1+".webm"),Convert.FromBase64String(encoded));recordings.Add(state);
                }
                foreach(string view in new[]{"hero","side","opposite","aft","nose"})
                {
                    await browser.ExecuteScriptAsync($"document.querySelector('[data-view={view}]').click();");
                    await Task.Delay(300);
                    using var image=File.Create(Path.Combine(output,id+"-"+view+".png"));
                    await browser.CoreWebView2.CapturePreviewAsync(CoreWebView2CapturePreviewImageFormat.Png,image);
                }
                var final=JsonSerializer.Deserialize<JsonElement>(await browser.ExecuteScriptAsync("window.fleetPlumeReview.diagnose()"));
                if(final.GetProperty("errors").GetArrayLength()!=0)throw new InvalidOperationException("GPU errors after animation/orbit: "+final);
                rows.Add(new{Ship=id,SourceSha256=JsonSerializer.Deserialize<string>(await browser.ExecuteScriptAsync("document.body.dataset.modelSha256")),FirstEnable=enabled,Palette=palette,Recordings=recordings,Final=final});
                Console.WriteLine($"MOTION: {id} orbit + fixed-camera flow + drive-end/bow views; no GL errors");
            }
            if(gpuFaults>0)throw new InvalidOperationException($"The browser reported {gpuFaults} GPU errors during the review.");
            File.WriteAllText(Path.Combine(output,"result.json"),JsonSerializer.Serialize(new{Passed=true,Executable=Environment.ProcessPath,Rows=rows,
                Evidence="Native protected WebView2 canvas recording during continuous camera orbit and animated exhaust. Requires visual review; completion alone does not prove visual fidelity or a GPU budget."},new JsonSerializerOptions{WriteIndented=true}));
        }
        catch(Exception ex){File.WriteAllText(Path.Combine(output,"result.json"),JsonSerializer.Serialize(new{Passed=false,Error=ex.ToString(),Rows=rows}));}
        finally{Close();}
    }
}
