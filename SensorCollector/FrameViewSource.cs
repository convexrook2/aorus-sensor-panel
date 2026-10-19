using System.Diagnostics;
using System.Runtime.InteropServices;

namespace SensorCollector;

public sealed class FrameViewSource(int processId = 0) : IDisposable
{
    public const string SourceName = "NVIDIA FrameView SDK";
    private nint library, session;
    private DateTimeOffset nextAttempt;
    private SourceStatus status = new(SourceName, "unavailable", "No FPS session.");
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate uint Open(out nint session, out uint stage);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate uint ReadFrame(nint session, uint pid, out double fps, out ulong swap, out ulong timestamp);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate void Close(nint session);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate uint Layout();

    public (List<Reading> Readings, SourceStatus Status) Read()
    {
        try
        {
            if (session == 0 && !EnsureSession()) return Missing(status.Status, status.Detail);
            uint pid = (uint)processId;
            if (pid == 0) GetWindowThreadProcessId(GetForegroundWindow(), out pid);
            if (pid == 0 || pid == Environment.ProcessId)
                return Missing("no-data", "Focus a rendering application. The panel's own frames are excluded.");
            uint result = Function<ReadFrame>("FrameViewRead")(session, pid, out double fps, out ulong swap, out ulong timestamp);
            if (result != 0)
            {
                if (result is 4 or 7) { Dispose(); nextAttempt = DateTimeOffset.UtcNow.AddSeconds(5); }
                return Missing(result == 3 ? "no-data" : "unavailable", $"FrameViewRead returned 0x{result:X8} for foreground PID {pid}.");
            }
            var now = DateTimeOffset.UtcNow;
            var reading = Decode(pid, swap, fps, timestamp, (ulong)Stopwatch.GetTimestamp(), Stopwatch.Frequency, now);
            return ([reading], new(SourceName, reading.Status == "ok" ? "available" : reading.Status, reading.Detail ?? ""));
        }
        catch (Exception ex)
        {
            Dispose(); nextAttempt = DateTimeOffset.UtcNow.AddSeconds(5);
            status = new(SourceName, "unavailable", ex.Message);
            return Missing(status.Status, status.Detail);
        }
    }

    private bool EnsureSession()
    {
        if (DateTimeOffset.UtcNow < nextAttempt) return false;
        nextAttempt = DateTimeOffset.UtcNow.AddSeconds(5);
        using var wmi = new WindowsWmi(@"root\CIMV2");
        var service = wmi.Query("SELECT State FROM Win32_Service WHERE Name='FvSvc'", "State").FirstOrDefault();
        if (!string.Equals(Convert.ToString(service?["State"]), "Running", StringComparison.OrdinalIgnoreCase))
        {
            status = new(SourceName, "service-stopped", "Existing NVIDIA FrameView service is stopped. This collector never starts it or changes its startup mode.");
            return false;
        }
        library = NativeLibrary.Load(Path.Combine(AppContext.BaseDirectory, "NvidiaFrameViewBridge.dll"));
        if (Function<Layout>("FrameViewLayout")() != 0x00382018) throw new InvalidOperationException("Unexpected FrameView native layout.");
        uint result = Function<Open>("FrameViewOpen")(out session, out uint stage);
        if (result != 0) throw new InvalidOperationException($"FrameView stage {stage} (1 initialize / 2 create / 3 start / 4 enable) returned 0x{result:X8}.");
        status = new(SourceName, "available", "Foreground application's average rendered FPS, per swap chain. Installed NVIDIA SDK, not NVML.");
        return true;
    }

    internal static Reading Decode(uint pid, ulong swap, double fps, ulong timestamp, ulong currentQpc, long frequency, DateTimeOffset now)
    {
        double age = timestamp <= currentQpc && frequency > 0 ? (currentQpc - timestamp) / (double)frequency : double.PositiveInfinity;
        bool valid = pid > 0 && pid != Environment.ProcessId && double.IsFinite(fps) && fps > 0 && age <= 3;
        string name;
        try { using var process = Process.GetProcessById((int)pid); name = process.ProcessName; }
        catch (ArgumentException) { name = $"PID {pid}"; }
        return new($"frameview/{pid}/{swap:X}/fps", $"{name}: average rendered FPS", valid ? fps : null, "FPS", SourceName,
            valid ? "ok" : "unavailable", $"NVIDIA eAvgFPS / AvgFPS_v1.mFPS.AvgFPS; application {name}, PID {pid}, swap chain 0x{swap:X}. Average rendered FPS; includes neither a guessed refresh rate nor an FPS estimate from utilization.",
            SampledAt: valid ? now.AddSeconds(-age) : null);
    }

    private (List<Reading> Readings, SourceStatus Status) Missing(string state, string detail)
        => ([new("frameview/status", "NVIDIA FPS", null, "FPS", SourceName, "unavailable", detail)], new(SourceName, state, detail));
    private T Function<T>(string export) where T : Delegate => Marshal.GetDelegateForFunctionPointer<T>(NativeLibrary.GetExport(library, export));
    public void Dispose()
    {
        if (library == 0) return;
        try { Function<Close>("FrameViewClose")(session); }
        finally { session = 0; NativeLibrary.Free(library); library = 0; }
    }

    internal static void CheckAbi()
    {
        nint native = NativeLibrary.Load(Path.Combine(AppContext.BaseDirectory, "NvidiaFrameViewBridge.dll"));
        try
        {
            var query = Marshal.GetDelegateForFunctionPointer<Layout>(NativeLibrary.GetExport(native, "FrameViewLayout"));
            if (query() != 0x00382018) throw new Exception("FrameView SDK's compiled x64 layout must match 56/32/24 byte structures.");
        }
        finally { NativeLibrary.Free(native); }
        CheckSamples();
    }

    internal static void CheckSamples()
    {
        var now = DateTimeOffset.UtcNow;
        if (Decode(1, 1, 100, 100, 500, 100, now).Value is not null ||
            Decode(1, 1, double.NaN, 100, 100, 100, now).Value is not null ||
            Decode((uint)Environment.ProcessId, 1, 60, 100, 100, 100, now).Value is not null)
            throw new Exception("Stale, nonfinite, and panel-owned FPS must never display as valid game FPS.");
    }
    [DllImport("user32.dll")] private static extern nint GetForegroundWindow();
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(nint window, out uint processId);
}
