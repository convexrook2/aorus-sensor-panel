using System.IO;
using System.Text.Json;
using System.Windows;

namespace SensorPanel;

internal sealed partial class PanelWindow
{
    // Opt-in LIVE measurement only. The terminal owns the performance counters.
    private async Task CheckFleetPairAsync(CancellationToken token)
    {
        string session=Guid.NewGuid().ToString("N"),ship="Pella";
        string phasePath=Path.Combine(PanelPaths.Artifacts,"fleet-pair-phase.json");
        string commandPath=Path.Combine(PanelPaths.Artifacts,"fleet-pair-command.json");
        var evidence=new List<object>();
        void Phase(string state,string? error=null)
        {
            File.WriteAllText(phasePath+".tmp",JsonSerializer.Serialize(new{State=state,Session=session,Ship=ship,
                ProcessId=Environment.ProcessId,ExecutablePath=Environment.ProcessPath,CapturedAt=DateTimeOffset.UtcNow,Error=error},JsonOptions));
            File.Move(phasePath+".tmp",phasePath,true);
        }
        async Task Wait(string state)
        {
            var deadline=DateTimeOffset.UtcNow.AddMinutes(5);
            while(DateTimeOffset.UtcNow<deadline)
            {
                token.ThrowIfCancellationRequested();
                try
                {
                    using var file=JsonDocument.Parse(await File.ReadAllTextAsync(commandPath,token));
                    var command=file.RootElement;
                    if(command.GetProperty("session").GetString()==session && command.GetProperty("ship").GetString()==ship
                        && command.GetProperty("processId").GetInt32()==Environment.ProcessId && command.GetProperty("state").GetString()==state)return;
                }
                catch(IOException){}catch(JsonException){}
                await Task.Delay(250,token);
            }
            throw new TimeoutException("Waiting for fleet measurement command: "+state);
        }
        async Task<JsonElement> Observe(bool active)
        {
            var result=await shipPanel!.ShipViewport.DiagnoseAsync();
            if(result.GetProperty("active").GetBoolean()!=active)throw new InvalidOperationException("Renderer activity differs from window state.");
            SaveDisplayState(); return result;
        }
        try
        {
            if(previewPath is not null || shipPanel is null)throw new InvalidOperationException("Fleet pair measurement requires actual LIVE monitoring.");
            Width=1920;Height=1080;
            foreach(string selected in new[]{"Pella","Rocinante"})
            {
                ship=selected; WindowState=WindowState.Normal;
                shipPanel.CloseHolotable();theme=PanelTheme.Resolve(ship);ApplyTheme();UpdateLayout();
                await shipPanel.ShipViewport.WaitForReadyAsync();await Task.Delay(1000,token);
                var visible=await Observe(true);Phase("monitor-ready");await Wait("holotable");
                shipPanel.OpenHolotable();await shipPanel.ShipViewport.WaitForReadyAsync();await Task.Delay(1000,token);
                shipPanel.CheckHolotableFraming();var table=await Observe(true);Phase("holotable-ready");await Wait("minimize");
                WindowState=WindowState.Minimized;await Task.Delay(1200,token);
                var stopped=await Observe(false);Phase("minimized-ready");await Wait("restore");
                WindowState=WindowState.Normal;Activate();await Task.Delay(1000,token);
                var resumed=await Observe(true);
                if(resumed.GetProperty("frames").GetInt64()<=stopped.GetProperty("frames").GetInt64())throw new InvalidOperationException("Renderer failed to resume.");
                evidence.Add(new{Ship=ship,Monitor=visible,Holotable=table,Minimized=stopped,Restored=resumed});
                Phase("ship-complete");await Wait("next");
            }
            File.WriteAllText(Path.Combine(PanelPaths.Artifacts,"fleet-pair-lifecycle.json"),JsonSerializer.Serialize(new{Passed=true,Session=session,
                ProcessId=Environment.ProcessId,Preview=false,Width,Height,Evidence=evidence},JsonOptions));
            Phase("complete");Close();
        }
        catch(Exception ex){Phase("failed",ex.ToString());Close();}
    }
}
