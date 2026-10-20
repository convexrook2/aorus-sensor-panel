using System.IO;
using System.Text.Json;
using System.Windows;

namespace SensorPanel;

internal sealed partial class PanelWindow
{
    // Only the explicit visible review flag enters this handshake. The VS Code
    // terminal owns counter sampling; the app changes its OWN window state.
    // There is no listener, polling task or automatic minimize in normal use.
    private async Task CheckRenderResourcesAsync(CancellationToken token)
    {
        if(Environment.GetCommandLineArgs().Contains("--measure-fleet-pair"))
        { await CheckFleetPairAsync(token); return; }
        string phasePath=Path.Combine(PanelPaths.Artifacts,"render-resource-phase.json");
        string commandPath=Path.Combine(PanelPaths.Artifacts,"render-resource-command.json");
        string session=Guid.NewGuid().ToString("N");
        void Phase(string state,string? error=null)
        {
            string json=JsonSerializer.Serialize(new{State=state,Session=session,ProcessId=Environment.ProcessId,
                ExecutablePath=Environment.ProcessPath,CapturedAt=DateTimeOffset.UtcNow,Error=error},JsonOptions);
            File.WriteAllText(phasePath+".tmp",json);File.Move(phasePath+".tmp",phasePath,true);
        }
        async Task Wait(string expected)
        {
            var until=DateTimeOffset.UtcNow.AddMinutes(4);
            while(DateTimeOffset.UtcNow<until)
            {
                token.ThrowIfCancellationRequested();
                try
                {
                    using var command=JsonDocument.Parse(await File.ReadAllTextAsync(commandPath,token));
                    var root=command.RootElement;
                    if(root.GetProperty("session").GetString()==session
                        &&root.GetProperty("processId").GetInt32()==Environment.ProcessId
                        &&root.GetProperty("state").GetString()==expected)return;
                }
                catch(IOException){}catch(JsonException){}
                await Task.Delay(250,token);
            }
            throw new TimeoutException("Visible resource review timed out waiting for "+expected);
        }
        try
        {
            if(shipPanel?.HolotableOpen!=true)throw new InvalidOperationException("The rotating holotable must be visible for this review.");
            WindowState=WindowState.Normal;Activate();
            shipPanel.CheckHolotableFraming();Phase("visible-ready");
            await Wait("minimize");WindowState=WindowState.Minimized;
            await Task.Delay(800,token);
            var stopped=await shipPanel.ShipViewport.DiagnoseAsync();
            if(stopped.GetProperty("active").GetBoolean())throw new InvalidOperationException("The minimized renderer is active.");
            Phase("minimized-ready");await Wait("restore");
            WindowState=WindowState.Normal;Activate();await Task.Delay(800,token);
            shipPanel.CheckHolotableFraming();
            var resumed=await shipPanel.ShipViewport.DiagnoseAsync();
            if(!resumed.GetProperty("active").GetBoolean()
                ||resumed.GetProperty("frames").GetInt64()<=stopped.GetProperty("frames").GetInt64())
                throw new InvalidOperationException("The full renderer failed to resume after resource sampling.");
            await shipPanel.ShipViewport.CaptureAsync(Path.Combine(PanelPaths.Artifacts,"optimized-render-restored.png"));
            File.WriteAllText(Path.Combine(PanelPaths.Artifacts,"render-resource-lifecycle.json"),
                JsonSerializer.Serialize(new{Passed=true,ProcessId=Environment.ProcessId,Session=session,
                    Stopped=stopped,Resumed=resumed},JsonOptions));
            Phase("complete");
        }
        catch(Exception ex){WindowState=WindowState.Normal;Phase("failed",ex.ToString());throw;}
    }
}
