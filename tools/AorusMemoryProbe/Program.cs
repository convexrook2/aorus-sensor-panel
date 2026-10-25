using System.Diagnostics;
using System.Text.Json;
using System.Security.Principal;
using AorusMemoryDiagnostics;

try
{
    string Arg(string key){int i=Array.IndexOf(args,key);return i>=0&&i+1<args.Length?args[i+1]:throw new ArgumentException("Missing "+key);}
    string executable=Path.GetFullPath(Arg("--exe")),folder=Path.GetFullPath(Arg("--output"));
    Directory.CreateDirectory(folder);
    if(args.Contains("--live"))
    {
        using var identity=WindowsIdentity.GetCurrent();
        if(!new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator))throw new InvalidOperationException("Run the visible probe as administrator for LIVE hardware access and complete process-tree measurement.");
    }
    var launch=new ProcessStartInfo(executable){UseShellExecute=false};
    foreach(string value in new[]{"--windowed","--memory-external","--memory-output",folder})launch.ArgumentList.Add(value);
    if(args.Contains("--live")){launch.ArgumentList.Add("--theme");launch.ArgumentList.Add("Cyber");launch.ArgumentList.Add("--check-aorus-memory-live");}
    else{launch.ArgumentList.Add("--preview");launch.ArgumentList.Add(Path.GetFullPath(Arg("--preview")));launch.ArgumentList.Add("--preview-theme");launch.ArgumentList.Add("Cyber");launch.ArgumentList.Add("--check-aorus-memory");}
    foreach(string flag in new[]{"--memory-aorus-only","--memory-all-themes"})if(args.Contains(flag))launch.ArgumentList.Add(flag);
    bool diagnostic=args.Contains("--dump-tool");if(diagnostic)launch.ArgumentList.Add("--memory-dump");
    foreach(string option in new[]{"--memory-phase-seconds","--memory-theme-seconds"})if(args.Contains(option)){launch.ArgumentList.Add(option);launch.ArgumentList.Add(Arg(option));}
    Console.WriteLine("Visible native memory review: "+executable);
    Console.WriteLine("20 ms app / 200 ms child sampling. The console probe is outside the measured app tree.");
    using var process=Process.Start(launch)??throw new InvalidOperationException("The native monitor did not launch.");
    var samples=new List<Sample>();var observed=new Dictionary<int,ProcessMemory.Helper>();
    ProcessMemory.Helper[] helpers=[];
    string phase="aorus-startup";var elapsed=Stopwatch.StartNew();int captures=0;
    bool dumped=false;
    while(!process.HasExited)
    {
        long started=Stopwatch.GetTimestamp();
        try
        {
            using var progress=JsonDocument.Parse(File.ReadAllText(Path.Combine(folder,"progress.json")));
            if(progress.RootElement.GetProperty("ProcessId").GetInt32()==process.Id)
            {
                string next=progress.RootElement.GetProperty("Phase").GetString()!;
                if(next!=phase){phase=next;Console.WriteLine($"{elapsed.Elapsed.TotalSeconds:0.0}s · {phase}");}
            }
        }
        catch(IOException){}catch(JsonException){}
        if(diagnostic&&!dumped&&phase=="diagnostic-hold")
        {
            dumped=true;
            var dump=new ProcessStartInfo(Path.GetFullPath(Arg("--dump-tool"))){UseShellExecute=false};
            foreach(string value in new[]{"collect","--process-id",process.Id.ToString(),"--output",Path.Combine(folder,"after-settings.dmp")})dump.ArgumentList.Add(value);
            using var capture=Process.Start(dump)??throw new InvalidOperationException("Heap diagnostic did not launch.");capture.WaitForExit();
            if(capture.ExitCode!=0)throw new InvalidOperationException("Heap capture failed.");
        }
        if(captures++%10==0)
        {
            helpers=ProcessMemory.ReadHelpers(process.Id);
        }
        // Refresh known children for every sample. A discovery snapshot must
        // not keep counting a GPU/renderer that has already exited.
        helpers=helpers.Select(ProcessMemory.RefreshHelper).OfType<ProcessMemory.Helper>().ToArray();
        foreach(var helper in helpers)if(!observed.TryGetValue(helper.ProcessId,out var old)||helper.PrivateResident>old.PrivateResident)observed[helper.ProcessId]=helper;
        if(ProcessMemory.TryRead(process.Handle,out var memory))
            samples.Add(new(elapsed.Elapsed.TotalSeconds,phase,memory.PrivateResident,memory.Resident,memory.PrivateCommit,memory.PrivateResident+(ulong)helpers.Sum(h=>(long)h.PrivateResident)));
        int delay=20-(int)Stopwatch.GetElapsedTime(started).TotalMilliseconds;if(delay>0)Thread.Sleep(delay);
    }
    process.WaitForExit();
    bool completed=false;
    string lifecycleFile=Path.Combine(folder,"lifecycle.json");
    if(File.Exists(lifecycleFile))
    {
        using var lifecycle=JsonDocument.Parse(File.ReadAllText(lifecycleFile));
        completed=lifecycle.RootElement.GetProperty("processId").GetInt32()==process.Id&&lifecycle.RootElement.GetProperty("completed").GetBoolean();
    }
    bool passed=!diagnostic&&completed&&process.ExitCode==0&&samples.Count>0&&samples.Where(s=>s.Phase.StartsWith("aorus-",StringComparison.Ordinal)).All(s=>s.TreePrivateResident<100_000_000);
    var options=new JsonSerializerOptions{WriteIndented=true};
    File.WriteAllText(Path.Combine(folder,"samples.json"),JsonSerializer.Serialize(samples,options));
    File.WriteAllText(Path.Combine(folder,"memory.json"),JsonSerializer.Serialize(new
    {
        ProcessId=process.Id,Executable=executable,Passed=passed,ExitCode=process.ExitCode,ElapsedSeconds=elapsed.Elapsed.TotalSeconds,
        DiagnosticCapture=diagnostic,TargetSamplingMilliseconds=20,AverageSamplingMilliseconds=samples.Count>1?1000*(samples[^1].Seconds-samples[0].Seconds)/(samples.Count-1):0,HelperDiscoverySamplingMilliseconds=200,HelperRefresh="Each app sample",BudgetBytes=100_000_000,Helpers=observed.Values,
        Phases=samples.GroupBy(s=>s.Phase).Select(g=>new{Phase=g.Key,Samples=g.Count(),PeakPrivateResident=g.Max(s=>s.PrivateResident),PeakResident=g.Max(s=>s.Resident),PeakPrivateCommit=g.Max(s=>s.PrivateCommit),PeakTreePrivateResident=g.Max(s=>s.TreePrivateResident),Last=g.Last(),Under100MB=g.Max(s=>s.TreePrivateResident)<100_000_000}),
        Scope="External sampling of private resident RAM for the complete native app tree. Shared resident and private commit reported separately. Normal production allocation/lifecycle collection remains active; no diagnostic GC or live-page trimming. Bounded observation, not a guarantee about unsampled spikes or future runtime."
    },options));
    Console.WriteLine((passed?"PASS":"FAIL")+": native AORUS memory review. Results: "+folder);
    return passed?0:1;
}
catch(Exception error){Console.Error.WriteLine(error);return 1;}

internal sealed record Sample(double Seconds,string Phase,ulong PrivateResident,ulong Resident,ulong PrivateCommit,ulong TreePrivateResident);
