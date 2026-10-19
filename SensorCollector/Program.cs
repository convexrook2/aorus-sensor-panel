using System.Text;
using System.Text.Json;

namespace SensorCollector;

internal static class Program
{
    private static int Main(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;
        if (args.Contains("--self-test")) return SelfTests.Run();
        if (args.Contains("--self-test-managed")) return SelfTests.Run(checkNativeBridge:false);
        if (args.Contains("--list-games"))
        {
            var discovery = new GameDiscovery();
            Console.WriteLine(JsonSerializer.Serialize(new { Installed = discovery.Installed, Running = discovery.Running(), discovery.Diagnostic }, new JsonSerializerOptions { WriteIndented = true }));
            return 0;
        }
        if (args.Contains("--help"))
        {
            Console.WriteLine("SensorCollector [--libre] [--frameview | --presentmon] [--fps-pid PID] [--json] [--count N | --watch] [--interval-ms N] [--out snapshot.json]");
            Console.WriteLine("Default: Gigabyte firmware + NVIDIA NVML. --libre: embedded LibreHardwareMonitor CPU/board + NVIDIA NVML; requires existing PawnIO and administrator rights.");
            return 0;
        }
        try
        {
            int count = 1, interval = 1000, fpsProcessId = 0;
            string? output = null;
            bool json = false, libre = false, frameView = false, presentMon = false;
            for (int i = 0; i < args.Length; i++)
            {
                switch (args[i])
                {
                    case "--json": json = true; break;
                    case "--libre": libre = true; break;
                    case "--frameview": frameView = true; break;
                    case "--presentmon": presentMon = true; break;
                    case "--fps-pid": fpsProcessId = int.Parse(args[++i]); if (fpsProcessId < 1) throw new ArgumentException("FPS PID must be positive."); break;
                    case "--watch": count = int.MaxValue; break;
                    case "--count": count = int.Parse(args[++i]); if (count is < 1 or > 10000) throw new ArgumentException("Count must be 1..10000."); break;
                    case "--interval-ms": interval = int.Parse(args[++i]); if (interval is < 250 or > 60000) throw new ArgumentException("Interval must be 250..60000 ms."); break;
                    case "--out": output = Path.GetFullPath(args[++i]); break;
                    default: throw new ArgumentException($"Unknown argument: {args[i]}");
                }
            }
            if (libre && interval < 1000) throw new ArgumentException("The Libre backend requires a sampling interval of at least 1000 ms.");
            if (frameView && presentMon) throw new ArgumentException("Choose one FPS reader: --frameview or --presentmon.");
            using var collector = new Collector(libre, frameView, fpsProcessId, presentMon);
            using var cancellation = new CancellationTokenSource();
            Console.CancelKeyPress += (_, e) => { e.Cancel = true; cancellation.Cancel(); };
            var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = false };
            // Establish a meaningful interval for the initial CPU usage sample.
            cancellation.Token.WaitHandle.WaitOne(250);
            for (int sample = 0; sample < count && !cancellation.IsCancellationRequested; sample++)
            {
                var snapshot = collector.Read();
                string serialized = JsonSerializer.Serialize(snapshot, options);
                if (json) Console.WriteLine(serialized); else Print(snapshot);
                if (output is not null)
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(output)!);
                    string temporary = output + ".tmp";
                    File.WriteAllText(temporary, JsonSerializer.Serialize(snapshot,
                        new JsonSerializerOptions(options) { WriteIndented = true }), new UTF8Encoding(false));
                    File.Move(temporary, output, true);
                }
                if (sample + 1 < count) cancellation.Token.WaitHandle.WaitOne(interval);
            }
            return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex.Message); return 1; }
    }

    private static void Print(Snapshot snapshot)
    {
        Console.WriteLine($"\n{snapshot.CapturedAt.ToLocalTime():yyyy-MM-dd HH:mm:ss zzz} | administrator: {snapshot.Elevated}");
        Console.WriteLine($"Board: {snapshot.Motherboard ?? "unavailable"}\nCPU: {snapshot.Cpu ?? "unavailable"}");
        foreach (var source in snapshot.Sources) Console.WriteLine($"[{source.Status}] {source.Source}: {source.Detail}");
        foreach (var reading in snapshot.Readings)
            Console.WriteLine($"{reading.Name,-64} {(reading.Value.HasValue ? reading.Value.Value.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture) : "—"),8} {reading.Unit,-5} [{reading.Status}] {reading.Source}");
    }
}
