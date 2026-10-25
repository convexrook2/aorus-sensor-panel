using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;

namespace SensorCollector;

// Portable console helper correlates Windows ETW present/display events. No overlay,
// injected game hook, installed service, or monitor-Hz substitution is used.
public sealed class PresentMonSource : IDisposable
{
    public const string SourceName = "Windows ETW / PresentMon";
    private readonly object gate = new();
    private readonly CancellationTokenSource cancellation = new();
    private readonly PresentMonFrames frames = new();
    private readonly GameDiscovery gameDiscovery = new();
    private readonly PresentMonDemand demand = new();
    private RunningGame[] runningGames = [];
    private int[] capturePids = [];
    private readonly Thread worker;
    private int requestedPid;
    private string diagnostic = "Waiting for game frame events.";
    private string selectionDetail = "Automatic game selection.";
    private string selectedName = "";
    private string failureState = "no-data";
    private readonly string sessionName = $"X890Panel-{Environment.ProcessId}-{Guid.NewGuid():N}";
    private int previousGamePid;
    private bool disposed;
    private bool runtimeFrames;

    public PresentMonSource(int processId = 0)
    {
        requestedPid = Math.Max(0, processId);
        worker = new Thread(Capture) { IsBackground = true, Name = "Windows FPS events" };
        worker.Start();
    }

    public int ProcessId { get => Volatile.Read(ref requestedPid); set => Volatile.Write(ref requestedPid, Math.Max(0, value)); }
    public FpsApplication[] Applications
    {
        get { lock (gate) return frames.Games(runningGames, QpcMs); }
    }
    private static double QpcMs => Stopwatch.GetTimestamp() * 1000.0 / Stopwatch.Frequency;

    public (List<Reading> Readings, SourceStatus Status) Read()
    {
        GetWindowThreadProcessId(GetForegroundWindow(), out uint foreground);
        var running = gameDiscovery.Running().ToArray();
        Volatile.Write(ref capturePids,running.Select(game=>game.ProcessId).ToArray());
        demand.Update(running.Length > 0);
        lock (gate)
        {
            double qpcMs = QpcMs;
            runningGames = running;
            int requested = ProcessId;
            var game = GameDiscovery.Select(running, (int)foreground, previousGamePid, requested);
            int pid = game?.ProcessId ?? 0;
            previousGamePid = pid;
            selectedName = game?.Name ?? "";
            selectionDetail = game is null
                ? (requested > 0 ? $"Selected PID {requested} is not a recognized running game. " : "No recognized game is running. ") + gameDiscovery.Diagnostic
                : $"{(requested > 0 ? "Pinned" : "Automatically selected")} {game.Name}; {game.Source}; executable {game.Executable}. Desktop focus retains the running game; desktop and ordinary app FPS are excluded.";
            if (pid == 0) return Missing("no-game", selectionDetail);
            var selected = frames.Read(pid, qpcMs);
            if (selected.Length == 0) return Missing(failureState, $"Waiting for PID {pid} display events. {diagnostic} {selectionDetail}");
            var newest = selected.Last();
            double fps = AverageFps(selected.Select(f => f.DisplayMs));
            if (!double.IsFinite(fps) || fps <= 0) return Missing("no-data", "No valid frame intervals.");
            var sampledAt = DateTimeOffset.UtcNow.AddMilliseconds(-(qpcMs - newest.QpcMs));
            string measurement = runtimeFrames ? "application-present FPS (successful runtime presents; not displayed FPS)" : "displayed FPS";
            string intervalColumn = runtimeFrames ? "msBetweenPresents" : "MsBetweenDisplayChange";
            string detail = $"{newest.Application}, PID {pid}, swap chain {newest.SwapChain}; {measurement} = 1000 × {selected.Length} / sum of {intervalColumn} over the latest one-second event window. NA/zero intervals and panel frames excluded. Windows ETW via portable PresentMon; busiest swap chain selected for this PID. {selectionDetail}";
            return ([new($"presentmon/{pid}/{newest.SwapChain}/fps", selectedName, fps, "FPS", SourceName, "ok", detail, SampledAt: sampledAt)], new(SourceName, "available", detail));
        }
    }

    private void Capture()
    {
        string? executable = FindExecutable("PresentMon-2.6.0-x890-compat-x64.exe");
        runtimeFrames = executable is not null;
        executable ??= FindExecutable("PresentMon-2.6.0-x64.exe");
        if (executable is null) { SetDiagnostic("Portable PresentMon 2.6.0 executable is missing."); return; }
        while (!cancellation.IsCancellationRequested)
        {
            CancellationToken capture = default;
            Process? process = null;
            Task<string>? errors = null;
            Task? stopping = null;
            CancellationTokenRegistration stopCapture = default;
            try
            {
                capture = demand.Acquire(cancellation.Token);
                capture.ThrowIfCancellationRequested();
                var start = new ProcessStartInfo(executable) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
                foreach (string argument in new[] { "--session_name", sessionName, "--output_stdout", "--no_console_stats", "--qpc_time_ms", "--no_track_input", "--no_track_gpu", "--exclude", "SensorPanel.exe" }) start.ArgumentList.Add(argument);
                if (runtimeFrames) { start.ArgumentList.Add("--v1_metrics"); start.ArgumentList.Add("--no_track_display"); }
                process = Process.Start(start) ?? throw new InvalidOperationException("PresentMon did not start.");
                // Keep this session while any recognized game runs, including
                // desktop focus. Stop off the sensor thread; closing the child
                // also releases a quiet synchronous redirected-pipe read.
                stopCapture = capture.Register(() => stopping = Task.Run(() => RequestStop(process)));
                errors = process.StandardError.ReadToEndAsync();
                Dictionary<string, int>? columns = null;
                string? line;
                while ((line = process.StandardOutput.ReadLine()) is not null)
                {
                    if (capture.IsCancellationRequested) continue;
                    if (line.StartsWith("Application,", StringComparison.Ordinal))
                    {
                        columns = Csv(line).Select((name, index) => (name, index)).ToDictionary(x => x.name, x => x.index, StringComparer.OrdinalIgnoreCase);
                        SetDiagnostic("PresentMon CSV columns: " + line);
                        continue;
                    }
                    if (columns is null) continue;
                    // Ordinary desktop/browser frames cannot supply game FPS.
                    // Reject them before allocating every CSV field or storing
                    // a frame history for every presenting application.
                    if(!columns.TryGetValue("ProcessID",out int pidColumn)||
                        !int.TryParse(CsvField(line.AsSpan(),pidColumn),out int eligiblePid)||
                        !Volatile.Read(ref capturePids).Contains(eligiblePid))continue;
                    var values = Csv(line);
                    string Field(string key) => columns.TryGetValue(key, out int index) && index < values.Count ? values[index] : "";
                    if (!int.TryParse(Field("ProcessID"), out int framePid) || framePid == Environment.ProcessId ||
                        Field("Application").Equals("SensorPanel.exe", StringComparison.OrdinalIgnoreCase)) continue;
                    if (!TryFrameTime(Field, runtimeFrames, out double stamp, out double interval)) continue;
                    if (runtimeFrames && Field("Dropped") != "0") continue;
                    lock (gate)
                    {
                        frames.Add(new(Field("Application"), framePid, Field("SwapChainAddress"), stamp, interval), QpcMs);
                        failureState = "no-data";
                    }
                }
                process.WaitForExit();
                string error = errors.GetAwaiter().GetResult().Trim();
                if (process.ExitCode != 0 && !capture.IsCancellationRequested)
                {
                    lock (gate) failureState = error.Contains("access denied", StringComparison.OrdinalIgnoreCase) ? "access-denied" : "unavailable";
                    SetDiagnostic($"PresentMon exit {process.ExitCode}: {error}");
                }
            }
            catch (OperationCanceledException) when (capture.IsCancellationRequested || cancellation.IsCancellationRequested) { }
            catch (Exception ex) { SetDiagnostic(ex.Message); }
            finally
            {
                stopCapture.Dispose();
                stopping?.GetAwaiter().GetResult();
                if (process is not null)
                {
                    // Drain stdout during normal ETW shutdown so a full pipe
                    // cannot prevent the helper's output thread from exiting.
                    var remaining = process.StandardOutput.ReadToEndAsync();
                    if (!process.HasExited) RequestStop(process);
                    try { Task.WhenAll(remaining, errors ?? Task.FromResult("")).Wait(TimeSpan.FromSeconds(1)); }
                    catch (AggregateException) { }
                    process.Dispose();
                }
                demand.Release();
            }
            if (!capture.IsCancellationRequested) cancellation.Token.WaitHandle.WaitOne(5000);
        }
    }

    private void SetDiagnostic(string message) { lock (gate) diagnostic = message; }
    private (List<Reading> Readings, SourceStatus Status) Missing(string status, string detail)
        => ([new("presentmon/status", selectedName, null, "FPS", SourceName, "unavailable", detail)], new(SourceName, status, detail));
    internal static double AverageFps(IEnumerable<double> intervals)
    {
        var valid = intervals.Where(x => double.IsFinite(x) && x > 0).ToArray();
        return valid.Length >= 2 ? 1000.0 * valid.Length / valid.Sum() : double.NaN;
    }
    internal static List<string> Csv(string line)
    {
        var fields = new List<string>(); var field = new System.Text.StringBuilder(); bool quoted = false;
        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];
            if (c == '"') { if (quoted && i + 1 < line.Length && line[i + 1] == '"') { field.Append('"'); i++; } else quoted = !quoted; }
            else if (c == ',' && !quoted) { fields.Add(field.ToString()); field.Clear(); }
            else field.Append(c);
        }
        fields.Add(field.ToString()); return fields;
    }
    internal static ReadOnlySpan<char> CsvField(ReadOnlySpan<char> line,int column)
    {
        int start=0,current=0;bool quoted=false;
        for(int i=0;i<=line.Length;i++)
        {
            if(i<line.Length&&line[i]=='"')
            {
                if(quoted&&i+1<line.Length&&line[i+1]=='"'){i++;continue;}
                quoted=!quoted;
            }
            if(i==line.Length||line[i]==','&&!quoted)
            {
                if(current++==column)
                {
                    var field=line[start..i];
                    return field.Length>=2&&field[0]=='"'&&field[^1]=='"'?field[1..^1]:field;
                }
                start=i+1;
            }
        }
        return [];
    }
    internal static bool TryFrameTime(Func<string, string> field, bool runtime, out double stamp, out double interval)
    {
        string qpc = field("QPCTimeInMs");
        if (qpc.Length == 0) qpc = field("CPUStartQPCTimeInMs");
        if (qpc.Length == 0) qpc = field("CPUStartQPCTime");
        bool seconds = false;
        if (qpc.Length == 0) { qpc = field("QPCTime"); seconds = true; }
        bool valid = double.TryParse(qpc, NumberStyles.Float, CultureInfo.InvariantCulture, out stamp);
        if (seconds) stamp *= 1000;
        return double.TryParse(field(runtime ? "msBetweenPresents" : "MsBetweenDisplayChange"), NumberStyles.Float,
            CultureInfo.InvariantCulture, out interval) && valid && double.IsFinite(stamp) && double.IsFinite(interval) && interval > 0;
    }
    private static string? FindExecutable(string fileName)
    {
        string local = Path.Combine(AppContext.BaseDirectory, fileName);
        if (File.Exists(local)) return local;
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            string candidate = Path.Combine(directory.FullName, "tools", "PresentMon", fileName);
            if (File.Exists(candidate)) return candidate;
        }
        return null;
    }
    public void Dispose()
    {
        if (disposed) return; disposed = true; cancellation.Cancel();
        worker.Join(TimeSpan.FromSeconds(6));
        if (!worker.IsAlive) { demand.Dispose(); cancellation.Dispose(); }
    }
    private void RequestStop(Process process)
    {
        try
        {
            // Official console's MainThread message loop exits normally on
            // WM_QUIT, then calls pmSession.Stop and drains its output threads.
            foreach (ProcessThread thread in process.Threads)
                using (thread) PostThreadMessage((uint)thread.Id, 0x0012, 0, 0);
            if (process.WaitForExit(1000)) return;
            // Stop only the session name created by this instance before any
            // last-resort child termination. Public PresentMon control command.
            var stop = new ProcessStartInfo(process.StartInfo.FileName) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardError = true, RedirectStandardOutput = true };
            foreach (string argument in new[] { "--session_name", sessionName, "--terminate_existing_session" }) stop.ArgumentList.Add(argument);
            using var controller = Process.Start(stop);
            if (controller is not null)
            {
                var error = controller.StandardError.ReadToEndAsync();
                var output = controller.StandardOutput.ReadToEndAsync();
                controller.WaitForExit(2000);
            }
            if (!process.WaitForExit(1000)) process.Kill();
        }
        catch (InvalidOperationException) { }
        catch (System.ComponentModel.Win32Exception) { }
    }
    [DllImport("user32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool PostThreadMessage(uint thread, uint message, nint wparam, nint lparam);
    [DllImport("user32.dll")] private static extern nint GetForegroundWindow();
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(nint window, out uint processId);
}
