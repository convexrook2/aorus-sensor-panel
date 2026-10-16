using System.Runtime.InteropServices;
using System.Security.Principal;

namespace SensorCollector;

public sealed class Collector : IDisposable
{
    private readonly GigabyteSource? gigabyte;
    private readonly LibreSource? libre;
    private readonly NvidiaSource nvidia;
    private readonly FrameViewSource? frameView;
    private readonly PresentMonSource? presentMon;
    private readonly Svi3Source? svi3;
    public int FpsProcessId { get => presentMon?.ProcessId ?? 0; set { if (presentMon is not null) presentMon.ProcessId = value; } }
    public FpsApplication[] FpsApplications => presentMon?.Applications ?? [];
    private readonly string? motherboard, cpu;
    private readonly SourceStatus identityStatus;
    private ulong previousIdle, previousTotal;

    public Collector(bool useLibre = false, bool useFrameView = false, int fpsProcessId = 0, bool usePresentMon = false)
    {
        string? maker = null;
        try
        {
            using var wmi = new WindowsWmi(@"root\CIMV2");
            var board = wmi.Query("SELECT Manufacturer, Product FROM Win32_BaseBoard", "Manufacturer", "Product").FirstOrDefault();
            maker = Convert.ToString(board?["Manufacturer"]);
            motherboard = Convert.ToString(board?["Product"]);
            cpu = Convert.ToString(wmi.Query("SELECT Name FROM Win32_Processor", "Name").FirstOrDefault()?["Name"])?.Trim();
            identityStatus = new("Windows SMBIOS/WMI", "available", "Hardware identity only; SMBIOS is not a live temperature source.");
        }
        catch (Exception ex) { identityStatus = new("Windows SMBIOS/WMI", "unavailable", ex.ToString()); }
        if (useLibre) { libre = new LibreSource(cpu); svi3 = new Svi3Source(cpu); }
        else gigabyte = new GigabyteSource(maker);
        nvidia = new NvidiaSource();
        if (useFrameView) frameView = new FrameViewSource(fpsProcessId);
        if (usePresentMon) presentMon = new PresentMonSource(fpsProcessId);
        if (GetSystemTimes(out var idle, out var kernel, out var user))
        {
            previousIdle = idle.Value;
            previousTotal = kernel.Value + user.Value;
        }
    }

    public Snapshot Read()
    {
        var board = libre is not null ? libre.Read() : gigabyte!.Read();
        var gpu = nvidia.Read();
        var readings = board.Readings.Concat(gpu.Readings).ToList();
        var voltage = svi3?.Read();
        if (voltage.HasValue) readings.AddRange(voltage.Value.Readings);
        var fps = presentMon?.Read() ?? frameView?.Read();
        if (fps.HasValue) readings.AddRange(fps.Value.Readings);
        if (libre is null)
        {
            readings.Add(new("cpu/package-temperature", "CPU package temperature", null, "°C",
                "Gigabyte ACPI/WMI", "unavailable", "Raw board channels do not prove Ryzen Tctl/Tdie. No verified CPU package mapping is available."));
            readings.Add(new("cpu/package-power", "CPU package power", null, "W",
                "Gigabyte ACPI/WMI", "unavailable", "The temperature-only firmware method does not expose CPU package power."));
        }
        var now = DateTimeOffset.UtcNow;
        double? usage = null;
        if (GetSystemTimes(out var idle, out var kernel, out var user))
        {
            ulong total = kernel.Value + user.Value;
            ulong delta = total >= previousTotal ? total - previousTotal : 0;
            ulong idleDelta = idle.Value >= previousIdle ? idle.Value - previousIdle : 0;
            if (delta > 0 && idleDelta <= delta)
                usage = 100.0 * (delta - idleDelta) / delta;
            previousIdle = idle.Value;
            previousTotal = total;
        }
        readings.Add(new("cpu/utilization", "CPU utilization", usage, "%", "Windows GetSystemTimes",
            usage.HasValue ? "ok" : "unavailable", SampledAt: now));
        using var identity = WindowsIdentity.GetCurrent();
        bool elevated = new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
        var sources = new List<SourceStatus> { identityStatus, board.Status, gpu.Status,
            new("Windows GetSystemTimes", usage.HasValue ? "available" : "unavailable", "CPU utilization from Windows processor time counters.") };
        if (fps.HasValue) sources.Add(fps.Value.Status);
        if (voltage.HasValue) sources.Add(voltage.Value.Status);
        return new(now, elevated, motherboard, cpu, readings, sources);
    }

    public void Dispose() { presentMon?.Dispose(); frameView?.Dispose(); svi3?.Dispose(); gigabyte?.Dispose(); libre?.Dispose(); nvidia.Dispose(); }

    [StructLayout(LayoutKind.Sequential)] private struct FileTime
    {
        public uint Low, High;
        public readonly ulong Value => ((ulong)High << 32) | Low;
    }
    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetSystemTimes(out FileTime idle, out FileTime kernel, out FileTime user);
}
