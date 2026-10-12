using System.Security.Principal;
using LibreHardwareMonitor.Hardware;
using LibreHardwareMonitor.PawnIo;

namespace SensorCollector;

// Uses the library inside this process. Never reads another app's memory or
// persisted fan-control settings, and never invokes IControl setters.
internal sealed class LibreSource : IDisposable
{
    private const string Source = "LibreHardwareMonitor 0.9.6 / existing PawnIO";
    private Computer? computer;
    private AmdFamily17? cpuProbe;
    private SourceStatus status = new(Source, "unavailable", "Not initialized.");
    private readonly Dictionary<string, Reading> known = new();

    public LibreSource(string? cpuName)
    {
        using var identity = WindowsIdentity.GetCurrent();
        if (!new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator))
        {
            status = status with { Status = "access-denied", Detail = "Run --libre in an administrator terminal. No automatic elevation or driver installation." };
            return;
        }
        if (!PawnIo.IsInstalled || PawnIo.Version < new Version(2, 2))
        {
            status = status with { Detail = "Existing PawnIO 2.2 or later is required. No driver is installed by this collector." };
            return;
        }
        // The raw access preflight is intentionally limited to an admitted CPU model.
        // PawnIO can silently return zero when a module cannot be loaded.
        if (cpuName?.Contains("9950X3D", StringComparison.OrdinalIgnoreCase) != true)
        {
            status = status with { Detail = "This processor is outside the supported raw CPU access preflight. CPU/motherboard collection is unavailable." };
            return;
        }
        try
        {
            cpuProbe = new AmdFamily17();
            CheckCpuAccess();
            computer = new Computer { IsCpuEnabled = true, IsMotherboardEnabled = true };
            // Default settings return Undefined control mode; no saved settings
            // from the standalone GUI are loaded. GPU/USB/storage are disabled.
            computer.Open();
            status = new(Source, "available", $"Embedded library; PawnIO {PawnIo.Version}; CPU and motherboard only. Labels are upstream mappings; values await hardware comparison. Sample timestamps mark retrieval, not device acquisition.");
        }
        catch (Exception ex)
        {
            status = new(Source, "unavailable", $"Initialization: {ex.Message} (0x{ex.HResult:X8}).");
            Dispose();
            computer = null;
            cpuProbe = null;
        }
    }

    private void CheckCpuAccess()
    {
        // Same THM_TCON_CUR_TMP register and conversion as upstream Amd17Cpu
        // and Linux k10temp for family 1Ah. Access is through its PawnIO module.
        uint raw = cpuProbe!.ReadSmn(0x00059800);
        double temperature = (raw >> 21) * 0.125 - ((raw & 0x80000) != 0 ? 49 : 0);
        if (raw == 0 || temperature <= 0 || temperature > 125)
            throw new InvalidOperationException("CPU temperature access returned an absent/invalid value; zeroed driver results are rejected.");
    }

    public (List<Reading> Readings, SourceStatus Status) Read()
    {
        if (computer is null) return (Missing(status.Detail, status.Status), status);
        var readings = new List<Reading>();
        try
        {
            CheckCpuAccess();
            foreach (var hardware in computer.Hardware) ReadHardware(hardware, readings);
            AddMemoryClock(readings);
            foreach (var reading in readings) known[reading.Id] = reading;
            var present = readings.Select(r => r.Id).ToHashSet();
            foreach (var previous in known.Values.Where(r => !present.Contains(r.Id)))
                readings.Add(previous with { Value = null, Status = "unavailable", Detail = "Sensor no longer enumerated.", SampledAt = null });
            var summary = readings.Any(r => r.Value.HasValue) ? status
                : status with { Status = "unavailable", Detail = "No usable CPU/motherboard sensor values." };
            return (readings, summary);
        }
        catch (Exception ex)
        {
            string detail = $"Collection failed: {ex.Message} (0x{ex.HResult:X8}).";
            return (Missing(detail, "unavailable"), new(Source, "unavailable", detail));
        }
    }

    private void AddMemoryClock(List<Reading> readings)
    {
        const string source = "AMD DDR5 UMC / PawnIO + measured BCLK";
        try
        {
            // ZenStates-Core Ddr5Timings.ReadRatio: UMC 0x50200[15:0]/100.
            // Read only on the 9950X3D admitted by this source's preflight.
            uint raw = cpuProbe!.ReadSmn(0x50200);
            var bus = readings.FirstOrDefault(r => r.Name.EndsWith(" / Bus Speed", StringComparison.Ordinal)
                && r.Unit == "MHz" && r.Status == "ok");
            double? mclk = DecodeMemoryClock(raw, bus?.Value);
            string detail = $"UMC0 register 0x50200=0x{raw:X8}; clock ratio {(raw & 0xffff) / 100.0:0.##} × current BCLK {bus?.Value:0.###} MHz. DDR data rate is twice MCLK; SMBIOS speed is not used as a live sample.";
            var now = DateTimeOffset.UtcNow;
            readings.Add(new("ram/mclk", "RAM memory clock (MCLK)", mclk, "MHz", source,
                mclk.HasValue ? "ok" : "unavailable", detail, raw, now));
            readings.Add(new("ram/data-rate", "RAM DDR data rate", mclk * 2, "MT/s", source,
                mclk.HasValue ? "ok" : "unavailable", detail, raw, now));
        }
        catch (Exception ex)
        {
            readings.Add(new("ram/mclk", "RAM memory clock (MCLK)", null, "MHz", source, "unavailable", ex.Message));
            readings.Add(new("ram/data-rate", "RAM DDR data rate", null, "MT/s", source, "unavailable", ex.Message));
        }
    }

    internal static double? DecodeMemoryClock(uint raw, double? busClock)
    {
        double ratio = (raw & 0xffff) / 100.0;
        if (raw is 0 or uint.MaxValue || ratio is < 10 or > 60 || busClock is null ||
            !double.IsFinite(busClock.Value) || busClock is < 80 or > 120) return null;
        return ratio * busClock;
    }

    private static void ReadHardware(IHardware hardware, List<Reading> readings)
    {
        hardware.Update();
        foreach (var sensor in hardware.Sensors)
        {
            string? unit = sensor.SensorType switch
            {
                SensorType.Temperature => "°C", SensorType.Voltage => "V",
                SensorType.Fan => "RPM", SensorType.Power => "W",
                SensorType.Clock => "MHz", SensorType.Load => "%",
                _ => null
            };
            if (unit is null) continue;
            readings.Add(Decode(sensor.Identifier.ToString(), $"{hardware.Name} / {sensor.Name}", sensor.Value, unit, DateTimeOffset.UtcNow));
        }
        foreach (var child in hardware.SubHardware) ReadHardware(child, readings);
    }

    internal static Reading Decode(string id, string name, float? value, string unit, DateTimeOffset retrievedAt)
    {
        bool available = value.HasValue && float.IsFinite(value.Value);
        // Actual board rail labels/scaling and Ryzen VID decoding need tracing:
        // live samples showed +0.82V -> 1.86V and core VID -> 0.14V.
        bool voltage = unit == "V";
        return new($"libre{id}", name, available ? value : null, unit, Source,
            !available ? "unavailable" : voltage ? "unmapped" : "ok",
            voltage ? "Voltage label/scaling/VID decoding is unverified on this board/CPU. Preserve for diagnostics; do not display as a confirmed voltage."
                : "Upstream sensor label; retrieval time. CPU Tctl/Tdie differs from the motherboard CPU sensor.",
            SampledAt: retrievedAt);
    }

    private List<Reading> Missing(string detail, string failure)
    {
        var readings = known.Values.Select(r => r with { Value = null, Status = failure, Detail = detail, SampledAt = null }).ToList();
        if (readings.Count == 0)
            readings.Add(new("libre/status", "CPU/motherboard sensors", null, "", Source, failure, detail));
        return readings;
    }

    public void Dispose()
    {
        try { computer?.Close(); }
        finally { cpuProbe?.Close(); }
    }
}
