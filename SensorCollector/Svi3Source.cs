using LibreHardwareMonitor.PawnIo;
using System.Security.Principal;

namespace SensorCollector;

internal sealed class Svi3Source : IDisposable
{
    internal const string SourceName = "AMD SVI3 PMT / existing PawnIO";
    private RyzenSmu? module;
    private uint version, tableBase;
    private int bytes;
    private SourceStatus status = new(SourceName, "unavailable", "No CPU voltage telemetry session.");

    public Svi3Source(string? cpuName)
    {
        using var identity = WindowsIdentity.GetCurrent();
        if (!new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator) || !PawnIo.IsInstalled)
        { status = new(SourceName, "access-denied", "Existing PawnIO and administrator rights are required for CPU rail telemetry."); return; }
        if (cpuName?.Contains("9950X3D", StringComparison.OrdinalIgnoreCase) != true)
        { status = new(SourceName, "unsupported", "This processor is outside the supported SVI3 voltage mapping."); return; }
        try
        {
            module = new RyzenSmu();
            module.ResolvePmTable(out version, out tableBase);
            bytes = TableSize(version);
            if (bytes == 0 || tableBase == 0) throw new InvalidDataException($"Unmapped/absent PMT: version 0x{version:X8}, base 0x{tableBase:X8}.");
            status = new(SourceName, "available", $"Granite Ridge PMT 0x{version:X8}, {bytes} bytes. VDDCR_VDD measured voltage at 0xC4; requested VID at 0xC0.");
        }
        catch (Exception ex) { status = new(SourceName, "unavailable", ex.Message); Dispose(); }
    }

    public (List<Reading> Readings, SourceStatus Status) Read()
    {
        if (module is null) return Missing(status);
        try
        {
            // These typed library operations resolve/refresh/read firmware
            // telemetry. No voltage, clock, power-limit, or fan setters are called.
            module.UpdatePmTable();
            long[] values = module.ReadPmTable((bytes + 7) / 8);
            if (values.Length * 8 < bytes) throw new InvalidDataException("Truncated CPU PMT.");
            byte[] raw = new byte[bytes]; Buffer.BlockCopy(values, 0, raw, 0, bytes);
            float measured = BitConverter.ToSingle(raw, 0xC4), vid = BitConverter.ToSingle(raw, 0xC0);
            var now = DateTimeOffset.UtcNow;
            string detail = $"PMT 0x{version:X8}, base 0x{tableBase:X8}; refreshed firmware table. SVI3 VDDCR_VDD measured rail at byte 0xC4; requested VID at byte 0xC0 is a separate quantity. Layout: ZenStates-Core PowerTable GraniteRidge/Svi3Rails.VddSocMisc.";
            var readings = new List<Reading>
            {
                Decode("cpu/core-voltage", "CPU Vcore (SVI3 measured)", measured, BitConverter.ToUInt32(raw, 0xC4), now, detail),
                Decode("cpu/core-vid", "CPU requested VID (SVI3)", vid, BitConverter.ToUInt32(raw, 0xC0), now, detail)
            };
            return (readings, readings[0].Value.HasValue ? status : status with { Status = "unavailable", Detail = "CPU PMT rail voltage is absent/nonfinite/out of the supported range." });
        }
        catch (Exception ex) { return Missing(new(SourceName, "unavailable", ex.Message)); }
    }

    // Explicit published Granite Ridge versions only; never apply a generic
    // layout or hunt through physical memory when firmware changes.
    internal static int TableSize(uint version) => version switch
    {
        0x621102 or 0x620105 => 0x724,
        0x621202 or 0x620205 => 0x994,
        _ => 0
    };
    internal static Reading Decode(string id, string name, float voltage, uint bits, DateTimeOffset now, string detail)
    {
        bool valid = float.IsFinite(voltage) && voltage is > 0.1f and <= 1.65f;
        return new(id, name, valid ? voltage : null, "V", SourceName, valid ? "ok" : "unavailable", detail, bits, valid ? now : null);
    }
    private static (List<Reading>, SourceStatus) Missing(SourceStatus status)
        => ([new("cpu/core-voltage", "CPU Vcore (SVI3 measured)", null, "V", SourceName, "unavailable", status.Detail),
             new("cpu/core-vid", "CPU requested VID (SVI3)", null, "V", SourceName, "unavailable", status.Detail)], status);
    public void Dispose() { module?.Close(); module = null; }
}
