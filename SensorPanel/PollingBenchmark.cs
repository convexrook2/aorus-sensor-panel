using System.Diagnostics;
using System.IO;
using System.Management;
using System.Text.Json;
using System.Windows.Threading;

namespace SensorPanel;

internal sealed class PollingBenchmark : IDisposable
{
    private readonly PanelWindow window;
    private readonly Action<int> setInterval;
    private readonly Func<(long Cycles, long Ticks)> counters;
    private readonly DispatcherTimer timer;
    private readonly Process process = Process.GetCurrentProcess();
    private Process? helper;
    private readonly List<object> rows = [];
    private readonly int[] intervals = [1000, 500, 5000];
    private int stage = -1;
    private DateTimeOffset started = DateTimeOffset.UtcNow;
    private TimeSpan cpu, helperCpu;
    private long cycles, ticks;
    public PollingBenchmark(PanelWindow window, Action<int> setInterval, Func<(long Cycles, long Ticks)> counters)
    {
        this.window = window; this.setInterval = setInterval; this.counters = counters;
        timer = new DispatcherTimer(TimeSpan.FromSeconds(1), DispatcherPriority.Background, (_, _) => Tick(), window.Dispatcher);
        timer.Start();
    }
    private void BeginStage()
    {
        setInterval(intervals[stage]); started = DateTimeOffset.UtcNow;
        (cycles,ticks) = counters(); cpu = process.TotalProcessorTime; helperCpu = helper?.TotalProcessorTime ?? TimeSpan.Zero;
    }
    private void Tick()
    {
        double elapsed = (DateTimeOffset.UtcNow-started).TotalSeconds;
        if (stage < 0)
        {
            if (elapsed < 10) return; // Exclude artwork loading/JIT from the first sample.
            using var search = new ManagementObjectSearcher($"SELECT ProcessId FROM Win32_Process WHERE ParentProcessId={Environment.ProcessId} AND Name='PresentMon-2.6.0-x64.exe'");
            using var children = search.Get();
            foreach (ManagementObject child in children) { helper = Process.GetProcessById((int)(uint)child["ProcessId"]); break; }
            stage=0; BeginStage(); return;
        }
        if (elapsed < 20) return;
        process.Refresh(); helper?.Refresh();
        var current = counters(); long count = current.Cycles-cycles;
        double appCpu = 100*(process.TotalProcessorTime-cpu).TotalSeconds/elapsed/Environment.ProcessorCount;
        double fpsCpu = helper is null || helper.HasExited ? 0 : 100*(helper.TotalProcessorTime-helperCpu).TotalSeconds/elapsed/Environment.ProcessorCount;
        rows.Add(new { IntervalMs=intervals[stage], Seconds=elapsed, CollectionCycles=count, ActualCollectionsPerSecond=count/elapsed,
            MeanCollectionMs=count>0 ? (current.Ticks-ticks)*1000.0/Stopwatch.Frequency/count : (double?)null,
            PanelCPUPercent=appCpu, FPSHelperCPUPercent=fpsCpu, CombinedCPUPercent=appCpu+fpsCpu,
            WorkingSetMiB=process.WorkingSet64/1048576.0, PrivateCommittedMiB=process.PrivateMemorySize64/1048576.0 });
        if (++stage < intervals.Length) { BeginStage(); return; }
        timer.Stop();
        File.WriteAllText(Path.Combine(PanelPaths.Artifacts,"polling-benchmark.json"),JsonSerializer.Serialize(new
        {
            CompletedAt=DateTimeOffset.UtcNow, LogicalProcessors=Environment.ProcessorCount, PanelPID=Environment.ProcessId,
            AnimationIntervalMs=30, Rows=rows, Notes="Visible full panel plus its FPS helper. Twenty-second samples after warm-up; not a controlled game-FPS-loss test. GPU utilization was not measured. Interval requests are start-to-start, capped by collection time."
        },new JsonSerializerOptions {WriteIndented=true}));
        window.Close();
    }
    public void Dispose() { timer.Stop(); helper?.Dispose(); process.Dispose(); }
}
