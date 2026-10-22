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
            // Only this opt-in review window takes focus. Do not use an
            // external activation/input route that can dismiss its holotable.
            if(state.EndsWith("-ready",StringComparison.Ordinal)&&state!="minimized-ready")Activate();
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
                if(shipPanel?.FeaturedShipId!=ship)
                    throw new OperationCanceledException("Ship selection changed during measurement; leave the owner's review in place.");
                if(state is "rotate" or "minimize" or "restore" && shipPanel?.HolotableOpen!=true)
                    throw new OperationCanceledException("Holotable closed during measurement; leave the owner's review in place.");
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
            if(!shipPanel!.HolotableOpen)
            {
                if(!shipPanel.HasShipViewport)throw new InvalidOperationException("The showcase renderer is missing.");
                var showcase=await shipPanel.ShipViewport.DiagnoseAsync();
                SaveDisplayState();return showcase;
            }
            var result=await shipPanel!.ShipViewport.DiagnoseAsync();
            if(result.GetProperty("active").GetBoolean()!=active)throw new InvalidOperationException("Renderer activity differs from window state.");
            SaveDisplayState(); return result;
        }
        try
        {
            if(previewPath is not null || shipPanel is null)throw new InvalidOperationException("Fleet pair measurement requires actual LIVE monitoring.");
            ApplyGraphicsMode(Environment.GetCommandLineArgs().Contains("--measure-full-fidelity")?ExpanseGraphicsMode.FullFidelity:ExpanseGraphicsMode.Efficient);
            Width=1920;Height=1080;
            foreach(string selected in new[]{"Pella","Rocinante"})
            {
                ship=selected; WindowState=WindowState.Normal;
                shipPanel.CloseHolotable();theme=PanelTheme.Resolve(ship);ApplyTheme();UpdateLayout();
                shipPanel.SetShowcaseRotation(true);shipPanel.SetNavigationMotionForReview(true);
                await Task.Delay(1000,token);
                var visible=await Observe(true);var showcaseHost=shipPanel.ShipViewport.PixelDiagnostics();Phase("monitor-ready");
                if(Environment.GetCommandLineArgs().Contains("--measure-map-only"))
                {
                    await Wait("pause-map");shipPanel.SetNavigationMotionForReview(false);await Task.Delay(1000,token);
                    SaveDisplayState();
                    var paused=shipPanel.NavigationView!.Diagnose();await Task.Delay(500,token);
                    var still=shipPanel.NavigationView.Diagnose();
                    if(still.GetProperty("renderActive").GetBoolean()||still.GetProperty("frameCount").GetInt64()!=paused.GetProperty("frameCount").GetInt64())
                        throw new InvalidOperationException("Map-only pause failed to stop its frames.");
                    Phase("map-paused-ready");
                    JsonElement? frozenShip=null,shipPausedMapActive=null;
                    if(Environment.GetCommandLineArgs().Contains("--measure-rotation-pause"))
                    {
                        await Wait("pause-rotation");shipPanel.SetShowcaseRotation(false);await Task.Delay(700,token);
                        var frozenBefore=await Observe(true);await Task.Delay(400,token);frozenShip=await Observe(true);
                        if(frozenShip.Value.GetProperty("motion").GetBoolean()||frozenShip.Value.GetProperty("frames").GetInt64()!=frozenBefore.GetProperty("frames").GetInt64())
                            throw new InvalidOperationException("Showcase pause did not stop its frames.");
                        Phase("all-paused-ready");await Wait("animate-map");shipPanel.SetNavigationMotionForReview(true);await Task.Delay(700,token);
                        shipPausedMapActive=await Observe(true);Phase("showcase-paused-ready");
                    }
                    await Wait("next");
                    evidence.Add(new{Ship=ship,Monitor=visible,ShowcaseHost=showcaseHost,PausedMap=still,FrozenShip=frozenShip,ShipPausedMapActive=shipPausedMapActive});continue;
                }
                await Wait("holotable");
                shipPanel.OpenHolotable();await shipPanel.ShipViewport.WaitForReadyAsync();await Task.Delay(1000,token);
                shipPanel.CheckHolotableFraming();var table=await Observe(true);Phase("holotable-ready");await Wait("rotate");
                shipPanel.SetHolotableRotation(true);await Task.Delay(1000,token);
                var rotating=await Observe(true);Phase("rotating-ready");await Wait("minimize");
                WindowState=WindowState.Minimized;await Task.Delay(1200,token);
                var stopped=await Observe(false);Phase("minimized-ready");await Wait("restore");
                WindowState=WindowState.Normal;Activate();await Task.Delay(1000,token);
                var resumed=await Observe(true);
                if(resumed.GetProperty("frames").GetInt64()<=stopped.GetProperty("frames").GetInt64())throw new InvalidOperationException("Renderer failed to resume.");
                shipPanel.CloseHolotable();
                if(!shipPanel.HasShipViewport||shipPanel.ShipViewport.IsHolotable)throw new InvalidOperationException("Closing the holotable did not restore the showcase.");
                evidence.Add(new{Ship=ship,Monitor=visible,HolotableIdle=table,HolotableRotating=rotating,Minimized=stopped,Restored=resumed});
                Phase("ship-complete");await Wait("next");
            }
            File.WriteAllText(Path.Combine(PanelPaths.Artifacts,"fleet-pair-lifecycle.json"),JsonSerializer.Serialize(new{Passed=true,Session=session,
                ProcessId=Environment.ProcessId,Preview=false,Width,Height,Evidence=evidence},JsonOptions));
            Phase("complete");Close();
        }
        catch(OperationCanceledException ex){Phase("interrupted",ex.Message);}
        catch(Exception ex){Phase("failed",ex.ToString());Close();}
    }
}
