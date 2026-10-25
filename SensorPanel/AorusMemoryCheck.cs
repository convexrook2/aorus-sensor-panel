using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows;

namespace SensorPanel;

// The separate console probe owns sampling; the target only reports lifecycle.
internal sealed class AorusMemorySampler : IDisposable
{
    internal static string Folder { get; private set; } = "";
    internal static bool Active { get; private set; }
    internal static AorusMemorySampler? Start(string[] args)
    {
        if(!args.Any(a=>a is "--check-aorus-memory" or "--check-aorus-memory-live"))return null;
        if(!args.Contains("--memory-external"))throw new ArgumentException("Run this review through tools/AorusMemoryProbe so the sampler is outside the app.");
        int index=Array.IndexOf(args,"--memory-output");
        if(index<0||index+1>=args.Length)throw new ArgumentException("The memory probe must supply its output folder.");
        Folder=Path.GetFullPath(args[index+1]);Directory.CreateDirectory(Folder);Active=true;
        Phase("aorus-startup");return new();
    }
    internal static void Phase(string phase)
    {
        if(!Active)return;
        File.WriteAllText(Path.Combine(Folder,"progress.json"),JsonSerializer.Serialize(new{Phase=phase,AtUtc=DateTimeOffset.UtcNow,ProcessId=Environment.ProcessId}));
    }
    public void Dispose()=>Active=false;
}

internal sealed partial class PanelWindow
{
    private async Task CheckAorusMemoryAsync()
    {
        var stages = new List<object>();
        try
        {
            bool live=previewPath is null;
            var args=Environment.GetCommandLineArgs();
            int secondsIndex=Array.IndexOf(args,"--memory-phase-seconds");
            int seconds=secondsIndex>=0?int.Parse(args[secondsIndex+1]):5;
            if(seconds is <5 or >300)throw new ArgumentOutOfRangeException("--memory-phase-seconds");
            Task Pause()=>Task.Delay(TimeSpan.FromSeconds(seconds));
            int themeIndex=Array.IndexOf(args,"--memory-theme-seconds");
            int themeSeconds=themeIndex>=0?int.Parse(args[themeIndex+1]):5;
            if(themeSeconds is <5 or >300)throw new ArgumentOutOfRangeException("--memory-theme-seconds");
            void Record(string stage)=>stages.Add(new{Stage=stage,CollectionCycles=Interlocked.Read(ref collectionCycles),ArtTicks=artworkAnimation?.TickCount,ArtFrame=artworkAnimation?.FrameIndex,EncodedArtBytes=artworkAnimation?.EncodedBytes,Width=ActualWidth,Height=ActualHeight,Theme=theme.Id,ManagedBytes=GC.GetTotalMemory(false),HeapCommitted=GC.GetGCMemoryInfo().TotalCommittedBytes,HeapAllowance=GC.GetGCMemoryInfo().TotalAvailableMemoryBytes,AorusCollections=aorusCollections,AorusCollectionMaxMilliseconds=aorusCollectionMaxMilliseconds});
            if (!windowed||live&&!Environment.GetCommandLineArgs().Contains("--check-aorus-memory-live"))throw new InvalidOperationException("Memory review requires a visible window and an explicit saved-data or LIVE diagnostic flag.");
            await Task.Delay(3000);
            if(live&&Interlocked.Read(ref collectionCycles)<2)throw new InvalidOperationException("LIVE sensor collection has not produced two samples.");
            AorusMemorySampler.Phase("aorus-visible"); await Pause();Record("visible");
            Hide(); AorusMemorySampler.Phase("aorus-tray"); await Pause();Record("tray");
            Show(); AorusMemorySampler.Phase("aorus-restored"); await Pause();Record("restored");
            if(args.Contains("--memory-all-themes"))
                foreach(var choice in PanelTheme.All.Where(t=>!t.ShipConsole))
                {AorusMemorySampler.Phase("aorus-theme-"+choice.Id);ChooseTheme(choice);await themeChange;settings?.Open();if(settings is not null)await settings.CheckPopupThemeAsync();await Task.Delay(TimeSpan.FromSeconds(themeSeconds));settings?.Close();Record("theme-"+choice.Id);}
            if(args.Contains("--memory-dump")){AorusMemorySampler.Phase("diagnostic-hold");await Task.Delay(30000);}
            if (!Environment.GetCommandLineArgs().Contains("--memory-aorus-only"))
            {
                AorusMemorySampler.Phase("expanse");
                theme = PanelTheme.Resolve("Rocinante"); ApplyTheme();
                await shipPanel!.ShipViewport.WaitForReadyAsync(); await Task.Delay(1500);
                var solar = new WeakReference(shipPanel.NavigationView!);
                var bounds = RestoreBounds;
                var windowState = WindowState;
                stages.Add(new { Stage = "expanse", Renderer = await shipPanel.ShipViewport.DiagnoseAsync() });
                AorusMemorySampler.Phase("expanse-retirement");
                ChooseTheme(PanelTheme.Resolve("Cyber")); await themeChange;
                if(theme.ShipConsole)throw new InvalidOperationException("The old Expanse runtime did not retire; AORUS was not activated.");
                if(RestoreBounds!=bounds||WindowState!=windowState)throw new InvalidOperationException("Experience switching changed window placement/state.");
                AorusMemorySampler.Phase("aorus-after-expanse"); await Pause();Record("after-expanse");
                stages.Add(new { Stage = "aorus-after-expanse", SolarStillReachable = solar.IsAlive, HasSolarView = shipPanel?.NavigationView is not null, HasShipRenderer = shipPanel?.HasShipViewport==true });
                if(Environment.GetCommandLineArgs().Contains("--memory-hold-after-return"))
                {
                    File.WriteAllText(Path.Combine(AorusMemorySampler.Folder,"holding.json"),JsonSerializer.Serialize(new{ProcessId=Environment.ProcessId,SolarStillReachable=solar.IsAlive}));
                    await Task.Delay(30000);
                }
                Hide(); AorusMemorySampler.Phase("aorus-tray-after-expanse"); await Pause();Record("tray-after-expanse");
            }
            File.WriteAllText(Path.Combine(AorusMemorySampler.Folder, "lifecycle.json"), JsonSerializer.Serialize(new { ProcessId=Environment.ProcessId,Completed = true, SavedData = !live, LiveSensorCycles=Interlocked.Read(ref collectionCycles),Stages = stages }, JsonOptions));
        }
        catch (Exception ex)
        {
            Environment.ExitCode = 1;
            File.WriteAllText(Path.Combine(AorusMemorySampler.Folder, "error.txt"), ex.ToString());
        }
        finally { Close(); }
    }
}
