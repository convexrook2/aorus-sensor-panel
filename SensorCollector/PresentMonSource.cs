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
    private readonly Dictionary<string, Queue<Frame>> frames = new();
    private readonly GameDiscovery gameDiscovery = new();
    private readonly Thread worker;
    private int requestedPid, activePid;
    private string diagnostic = "Waiting for a running game.";
    private string selectionDetail = "Automatic game selection.";
    private string selectedName = "";
    private string failureState = "no-data";
    private Process? helper;
    private readonly string sessionName = $"X890Panel-{Environment.ProcessId}-{Guid.NewGuid():N}";
    private int previousGamePid;
    private DateTimeOffset nextAutomaticSelection;
    private bool disposed;
    private record Frame(string Application, int Pid, string SwapChain, double QpcMs, double DisplayMs);

    public PresentMonSource(int processId = 0)
    {
        requestedPid = processId;
        worker = new Thread(Capture) { IsBackground = true, Name = "Windows FPS events" };
        worker.Start();
    }

    public int ProcessId { get => Volatile.Read(ref requestedPid); set => Volatile.Write(ref requestedPid, Math.Max(0, value)); }

    public (List<Reading> Readings, SourceStatus Status) Read()
    {
        int pid = ProcessId;
        if (pid == 0)
        {
            if (DateTimeOffset.UtcNow >= nextAutomaticSelection)
            {
            nextAutomaticSelection = DateTimeOffset.UtcNow.AddSeconds(1);
            GetWindowThreadProcessId(GetForegroundWindow(), out uint foreground);
            var automaticGame = GameDiscovery.Select(gameDiscovery.Running(), (int)foreground, previousGamePid);
            pid = automaticGame?.ProcessId ?? 0; previousGamePid = pid;
            selectedName = automaticGame?.Name ?? "";
            selectionDetail = automaticGame is null ? "No recognized game is running. " + gameDiscovery.Diagnostic
                : $"Automatically selected {automaticGame.Name}; {automaticGame.Source}; executable {automaticGame.Executable}. Desktop focus retains the running game.";
            }
            pid = previousGamePid;
        }
        else { selectionDetail = $"Explicit PID override {pid}."; selectedName = $"PID {pid}"; }
        if (pid == Environment.ProcessId) pid = 0;
        lock (gate)
        {
            // Pinning a PID keeps the selected game's FPS visible while the owner
            // interacts with the panel or another application.
            if (activePid != pid) { activePid = pid; frames.Clear(); diagnostic = pid > 0 ? $"Waiting for PID {pid} display events." : selectionDetail; }
            if (pid == 0) return Missing("no-game", selectionDetail);
            double qpcMs = Stopwatch.GetTimestamp() * 1000.0 / Stopwatch.Frequency;
            var candidates = frames.Values.Where(q => q.Count >= 2 && q.Last().Pid == pid && qpcMs - q.Last().QpcMs is >= 0 and <= 3000)
                .OrderByDescending(q => q.Count).ToArray();
            if (candidates.Length == 0) return Missing(failureState, diagnostic + " " + selectionDetail);
            var selected = candidates[0];
            var newest = selected.Last();
            double fps = AverageFps(selected.Select(f => f.DisplayMs));
            if (!double.IsFinite(fps) || fps <= 0) return Missing("no-data", "No valid displayed-frame intervals.");
            var sampledAt = DateTimeOffset.UtcNow.AddMilliseconds(-(qpcMs - newest.QpcMs));
            string detail = $"{newest.Application}, PID {pid}, swap chain {newest.SwapChain}; displayed FPS = 1000 × {selected.Count} / sum of MsBetweenDisplayChange over the latest one-second event window. NA/zero intervals and panel frames excluded. Windows ETW via portable PresentMon 2.6.0; busiest displayed swap chain selected for this PID. {selectionDetail}";
            return ([new($"presentmon/{pid}/{newest.SwapChain}/fps", newest.Application, fps, "FPS", SourceName, "ok", detail, SampledAt: sampledAt)], new(SourceName, "available", detail));
        }
    }

    private void Capture()
    {
        string? executable = FindExecutable();
        if (executable is null) { SetDiagnostic("Portable PresentMon 2.6.0 executable is missing."); return; }
        while (!cancellation.IsCancellationRequested)
        {
            try
            {
                var start = new ProcessStartInfo(executable) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
                foreach (string argument in new[] { "--session_name", sessionName, "--output_stdout", "--no_console_stats", "--qpc_time_ms", "--no_track_input", "--no_track_gpu", "--exclude", "SensorPanel.exe" }) start.ArgumentList.Add(argument);
                using var process = Process.Start(start) ?? throw new InvalidOperationException("PresentMon did not start.");
                lock (gate) helper = process;
                if (cancellation.IsCancellationRequested) RequestStop(process);
                // One session begins before game launch. Only the selected game's
                // records reach our rolling buffers; desktop records are discarded.
                var errors = process.StandardError.ReadToEndAsync();
                Dictionary<string, int>? columns = null;
                string? line;
                while ((line = process.StandardOutput.ReadLine()) is not null)
                {
                    if (cancellation.IsCancellationRequested) continue;
                    if (line.StartsWith("Application,", StringComparison.Ordinal))
                    {
                        columns = Csv(line).Select((name, index) => (name, index)).ToDictionary(x => x.name, x => x.index);
                        SetDiagnostic("PresentMon CSV columns: " + line);
                        continue;
                    }
                    if (columns is null) continue;
                    var values = Csv(line);
                    string Field(string key) => columns.TryGetValue(key, out int index) && index < values.Count ? values[index] : "";
                    int pid; lock (gate) pid = activePid;
                    if (pid <= 0 || !int.TryParse(Field("ProcessID"), out int framePid) || framePid != pid || framePid == Environment.ProcessId) continue;
                    string qpc = Field("QPCTimeInMs");
                    if (qpc.Length == 0) qpc = Field("CPUStartQPCTimeInMs");
                    if (qpc.Length == 0) qpc = Field("CPUStartQPCTime");
                    if (!double.TryParse(qpc, NumberStyles.Float, CultureInfo.InvariantCulture, out double stamp) ||
                        !double.TryParse(Field("MsBetweenDisplayChange"), NumberStyles.Float, CultureInfo.InvariantCulture, out double interval) ||
                        !double.IsFinite(stamp) || !double.IsFinite(interval) || interval <= 0) continue;
                    Add(new(Field("Application"), pid, Field("SwapChainAddress"), stamp, interval));
                }
                process.WaitForExit();
                string error = errors.GetAwaiter().GetResult().Trim();
                if (process.ExitCode != 0)
                {
                    lock (gate) failureState = error.Contains("access denied", StringComparison.OrdinalIgnoreCase) ? "access-denied" : "unavailable";
                    SetDiagnostic($"PresentMon exit {process.ExitCode}: {error}");
                }
            }
            catch (Exception ex) { SetDiagnostic(ex.Message); }
            finally { lock (gate) helper = null; }
            cancellation.Token.WaitHandle.WaitOne(5000);
        }
    }

    private void Add(Frame frame)
    {
        double qpcMs = Stopwatch.GetTimestamp() * 1000.0 / Stopwatch.Frequency;
        if (qpcMs - frame.QpcMs is < 0 or > 3000) return;
        lock (gate)
        {
            if (frame.Pid != activePid) return;
            if (!frames.TryGetValue(frame.SwapChain, out var queue)) frames[frame.SwapChain] = queue = new();
            if (queue.Count > 0 && frame.QpcMs <= queue.Last().QpcMs) return;
            queue.Enqueue(frame);
            failureState = "no-data";
            while (queue.Count > 0 && (frame.QpcMs - queue.Peek().QpcMs > 1000 || queue.Count > 4096)) queue.Dequeue();
            diagnostic = $"Receiving {frame.Application} Windows display events.";
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
    private static string? FindExecutable()
    {
        string local = Path.Combine(AppContext.BaseDirectory, "PresentMon-2.6.0-x64.exe");
        if (File.Exists(local)) return local;
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            string candidate = Path.Combine(directory.FullName, "tools", "PresentMon", "PresentMon-2.6.0-x64.exe");
            if (File.Exists(candidate)) return candidate;
        }
        return null;
    }
    public void Dispose()
    {
        if (disposed) return; disposed = true; cancellation.Cancel();
        Process? running; lock (gate) running = helper;
        if (running is not null) RequestStop(running);
        worker.Join(TimeSpan.FromSeconds(6));
        if (!worker.IsAlive) cancellation.Dispose();
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
