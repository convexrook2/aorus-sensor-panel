using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace SensorPanel;

internal sealed partial class PanelWindow
{
    // Explicit visible saved-data check; no sensor access or timer in normal runs.
    private async Task CheckSolarEfficiencyAsync()
    {
        bool baseline=Environment.GetCommandLineArgs().Contains("--solar-baseline");
        string output=Path.Combine(PanelPaths.Artifacts,"solar-efficiency",baseline?"before":"after");Directory.CreateDirectory(output);
        var samples=new List<object>();
        try
        {
            if(previewPath is null||!windowed||shipPanel?.NavigationView is not {} view)
                throw new InvalidOperationException("Solar efficiency review requires a windowed saved-data ship preview.");
            async Task<JsonElement> Sample(string phase,int milliseconds)
            {
                await Task.Delay(300);
                var before=view.Diagnose();var watch=Stopwatch.StartNew();await Task.Delay(milliseconds);
                double elapsed=watch.Elapsed.TotalSeconds;var after=view.Diagnose();
                double timerHz=(after.GetProperty("frameCount").GetInt64()-before.GetProperty("frameCount").GetInt64())/elapsed;
                double updateHz=(after.GetProperty("sceneUpdates").GetInt64()-before.GetProperty("sceneUpdates").GetInt64())/elapsed;
                samples.Add(new {Phase=phase,ElapsedSeconds=elapsed,TimerUpdatesPerSecond=timerHz,SceneUpdatesPerSecond=updateHz,Before=before,After=after});
                return after;
            }
            ApplyGraphicsMode(ExpanseGraphicsMode.Efficient);
            view.FocusBody("Sun",false);view.SetTimeRateForReview(21600);shipPanel.SetMotion(true);
            Title="X890 · Solar cadence check · unattended animation";
            await Sample("unattended-animation",3000);
            Title="X890 · Solar cadence check · camera transition";
            view.FocusBody("Earth",true);await Sample("animated-focus",1800);
            view.SetTimeRateForReview(0);await Task.Delay(400);
            Title="X890 · Solar cadence check · paused";
            var pausedBefore=view.Diagnose();await Sample("paused",1500);var pausedAfter=view.Diagnose();
            if(!baseline&&(pausedAfter.GetProperty("frameCount").GetInt64()!=pausedBefore.GetProperty("frameCount").GetInt64()
                ||pausedAfter.GetProperty("sceneUpdates").GetInt64()!=pausedBefore.GetProperty("sceneUpdates").GetInt64()))
                throw new InvalidOperationException("Paused, settled solar navigation kept refreshing.");
            // A paused simulation must still animate a requested focus transition.
            view.FocusBody("Sun",true);var transition=await Sample("paused-camera-transition",1600);
            if(!baseline&&transition.GetProperty("renderActive").GetBoolean())
                throw new InvalidOperationException("The paused camera transition did not stop when settled.");
            foreach(var (body,trueScale) in new[]{("Sun",false),("Earth",false),("Saturn",false),("Jupiter",false),("Medina",false),("Sun",true),("Earth",true)})
            {
                view.PrepareEfficiencyFrame(body,trueScale);await Task.Delay(600);
                // Allow the original lighting throttle to settle in baseline
                // captures too, then request the exact same fixed-state frame.
                view.PrepareEfficiencyFrame(body,trueScale);await Task.Delay(80);UpdateLayout();
                SaveDiagram(body+(trueScale?"-true-scale":"-schematic"));
            }
            view.PrepareEfficiencyFrame("Sun",false);view.SetMotion(true);view.SetTimeRateForReview(21600);
            Title="X890 · Solar cadence review · drag the solar map";
            void SaveDrag()=>File.WriteAllText(Path.Combine(output,"pointer.json"),JsonSerializer.Serialize(new {SavedData=true,Map=view.Diagnose(),Scope="Actual pointer gesture counters; not physical display FPS"},JsonOptions));
            view.ReviewDragCompleted+=SaveDrag;
            Closed+=(_,_)=>view.ReviewDragCompleted-=SaveDrag;
            File.WriteAllText(Path.Combine(output,"result.json"),JsonSerializer.Serialize(new {Passed=true,Baseline=baseline,SavedData=true,ProcessId=Environment.ProcessId,
                Samples=samples,Scope="Visible saved-data scene update/paint counters and deterministic map images. Not PresentMon FPS or LIVE GPU utilization."},JsonOptions));
            // Leave this ordinary review open for actual drag/zoom acceptance.
        }
        catch(Exception ex)
        {
            File.WriteAllText(Path.Combine(output,"result.json"),JsonSerializer.Serialize(new {Passed=false,Baseline=baseline,Error=ex.ToString(),Samples=samples},JsonOptions));
            Title="X890 · Solar cadence review FAILED · see terminal receipt";
        }
        void SaveDiagram(string name)
        {
            var view=shipPanel!.NavigationView!;
            var image=new RenderTargetBitmap((int)Math.Ceiling(view.ActualWidth),(int)Math.Ceiling(view.ActualHeight),96,96,PixelFormats.Pbgra32);
            image.Render(view);var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(image));
            using var stream=File.Create(Path.Combine(output,name+".png"));encoder.Save(stream);
        }
    }
}
