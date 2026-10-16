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
    private readonly DesktopFrameWindow desktopFrames = new();
    private readonly Thread worker;
    private int requestedPid;
    private string diagnostic = "Waiting for application display events.";
    private string selectionDetail = "Automatic application selection.";
    private string selectedName = "";
    private string failureState = "no-data";
    private Process? helper;
    private readonly string sessionName = $"X890Panel-{Environment.ProcessId}-{Guid.NewGuid():N}";
    private int previousApplicationPid;
    private bool disposed;
    private bool runtimeFrames;

    public PresentMonSource(int processId = 0)
    {
        requestedPid = processId;
        worker = new Thread(Capture) { IsBackground = true, Name = "Windows FPS events" };
        worker.Start();
    }

    public int ProcessId { get => Volatile.Read(ref requestedPid); set => Volatile.Write(ref requestedPid, Math.Max(-1, value)); }
    public FpsApplication[] Applications
    {
        get { lock (gate) {
            double now = QpcMs;
            var applications = frames.Applications(now);
            var desktop = desktopFrames.Read(now);
            return desktop is { } sample ? applications.Append(new FpsApplication(-1, "Windows desktop (DWM)", sample.Fps)).ToArray() : applications;
        } }
    }
    private static double QpcMs => Stopwatch.GetTimestamp() * 1000.0 / Stopwatch.Frequency;

    public (List<Reading> Readings, SourceStatus Status) Read()
    {
        GetWindowThreadProcessId(GetForegroundWindow(), out uint foreground);
        lock (gate)
        {
            double qpcMs = QpcMs;
            int pid = ProcessId;
            if (pid == 0)
            {
                pid = frames.Select((int)foreground, previousApplicationPid, qpcMs);
                previousApplicationPid = pid;
                selectionDetail = "Automatic: foreground presenting application; retain the previous active app stream; otherwise busiest app stream, then Windows desktop composition.";
            }
            else selectionDetail = $"Explicit PID override {pid}.";
            if (pid == Environment.ProcessId) pid = 0;
            if (pid == -1 || (ProcessId == 0 && pid == 0))
            {
                var desktop = ReadDesktop(qpcMs);
                if (desktop.HasValue) return desktop.Value;
                if (pid == -1) { selectedName = "Windows desktop (DWM)"; return Missing("no-data", "Waiting for Windows desktop composition frame events."); }
            }
            selectedName = frames.Applications(qpcMs).FirstOrDefault(a => a.ProcessId == pid)?.Name ?? (pid > 0 ? $"PID {pid}" : "");
            if (pid == 0) return Missing("no-data", diagnostic + " No application display stream is available.");
            var selected = frames.Read(pid, qpcMs);
            if (selected.Length == 0) return Missing(failureState, $"Waiting for PID {pid} display events. {diagnostic} {selectionDetail}");
            var newest = selected.Last();
            double fps = AverageFps(selected.Select(f => f.DisplayMs));
            if (!double.IsFinite(fps) || fps <= 0) return Missing("no-data", "No valid frame intervals.");
            var sampledAt = DateTimeOffset.UtcNow.AddMilliseconds(-(qpcMs - newest.QpcMs));
            string measurement = runtimeFrames ? "application-present FPS (successful runtime presents; not displayed FPS)" : "displayed FPS";
            string intervalColumn = runtimeFrames ? "msBetweenPresents" : "MsBetweenDisplayChange";
            string detail = $"{newest.Application}, PID {pid}, swap chain {newest.SwapChain}; {measurement} = 1000 × {selected.Length} / sum of {intervalColumn} over the latest one-second event window. NA/zero intervals and panel frames excluded. Windows ETW via portable PresentMon; busiest swap chain selected for this PID. {selectionDetail}";
            return ([new($"presentmon/{pid}/{newest.SwapChain}/fps", newest.Application, fps, "FPS", SourceName, "ok", detail, SampledAt: sampledAt)], new(SourceName, "available", detail));
        }
    }

    private (List<Reading> Readings, SourceStatus Status)? ReadDesktop(double now)
    {
        if (desktopFrames.Read(now) is not { } sample) return null;
        selectedName = "Windows desktop (DWM)";
        string detail = $"Desktop composition FPS from Microsoft-Windows-Dwm-Core CurrentFrameId events (451), DWM PID {sample.Pid}, current Windows session; {sample.Count} observed unique composition frames over the latest one-second event window. FPS counts received frame intervals, without filling missing frame IDs. This is session-wide desktop composition, not a particular application's FPS or confirmed display scan-out, and is not derived from monitor Hz. External ETW in the existing PresentMon session. {selectionDetail}";
        return ([new("presentmon/desktop/fps", selectedName, sample.Fps, "FPS", SourceName, "ok", detail,
            SampledAt: DateTimeOffset.UtcNow.AddMilliseconds(-(now - sample.Time)))], new(SourceName, "available", detail));
    }

    private void Capture()
    {
        string? executable = FindExecutable("PresentMon-2.6.0-x890-compat-x64.exe");
        runtimeFrames = executable is not null;
        executable ??= FindExecutable("PresentMon-2.6.0-x64.exe");
        if (executable is null) { SetDiagnostic("Portable PresentMon 2.6.0 executable is missing."); return; }
        while (!cancellation.IsCancellationRequested)
        {
            try
            {
                var start = new ProcessStartInfo(executable) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
                foreach (string argument in new[] { "--session_name", sessionName, "--output_stdout", "--no_console_stats", "--qpc_time_ms", "--no_track_input", "--no_track_gpu", "--exclude", "SensorPanel.exe" }) start.ArgumentList.Add(argument);
                if (runtimeFrames) { start.ArgumentList.Add("--v1_metrics"); start.ArgumentList.Add("--no_track_display"); start.ArgumentList.Add("--desktop_frames"); }
                using var process = Process.Start(start) ?? throw new InvalidOperationException("PresentMon did not start.");
                lock (gate) helper = process;
                if (cancellation.IsCancellationRequested) RequestStop(process);
                // One external ETW session observes every presenting application.
                var errors = process.StandardError.ReadToEndAsync();
                Dictionary<string, int>? columns = null;
                uint currentSession = (uint)Process.GetCurrentProcess().SessionId;
                int checkedDesktopPid = 0; bool desktopInSession = false;
                double nextDesktopSessionCheck = 0;
                string? line;
                while ((line = process.StandardOutput.ReadLine()) is not null)
                {
                    if (cancellation.IsCancellationRequested) continue;
                    if (line.StartsWith("#X890DesktopFrame,", StringComparison.Ordinal))
                    {
                        if (DesktopFrameWindow.TryParse(line, out int desktopPid, out ulong frameId, out double desktopStamp))
                        {
                            if (checkedDesktopPid != desktopPid || (!desktopInSession && QpcMs >= nextDesktopSessionCheck)) {
                                checkedDesktopPid = desktopPid;
                                nextDesktopSessionCheck = QpcMs + 1000;
                                try { using var dwm = Process.GetProcessById(desktopPid); desktopInSession = dwm.SessionId == currentSession; }
                                catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception) { desktopInSession = false; }
                            }
                            lock (gate) {
                                double now = QpcMs;
                                if (desktopInSession) desktopFrames.Add(desktopPid, frameId, desktopStamp, now);
                            }
                        }
                        continue;
                    }
                    if (line.StartsWith("Application,", StringComparison.Ordinal))
                    {
                        columns = Csv(line).Select((name, index) => (name, index)).ToDictionary(x => x.name, x => x.index, StringComparer.OrdinalIgnoreCase);
                        SetDiagnostic("PresentMon CSV columns: " + line);
                        continue;
                    }
                    if (columns is null) continue;
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
