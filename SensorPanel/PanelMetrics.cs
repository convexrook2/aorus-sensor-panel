using System.IO;
using SensorCollector;

namespace SensorPanel;

internal sealed class PanelMetrics(Snapshot snapshot, DateTimeOffset now, bool savedPreview = false, int pollingIntervalMs = 1000)
{
    private readonly string? gpuPrefix = snapshot.Readings.FirstOrDefault(r =>
        r.Source == "NVIDIA NVML" && r.Id.EndsWith("/temperature", StringComparison.Ordinal))?.Id[..^"temperature".Length];

    public string GpuName => snapshot.Readings.FirstOrDefault(r => r.Source == "NVIDIA NVML")?.Name.Split(':')[0]
        .Replace("NVIDIA ", "", StringComparison.Ordinal) ?? "NVIDIA GPU";

    public double? CpuTemperature => Read("libre/amdcpu/0/temperature/2");
    public double? CpuCcdMax => Read("libre/amdcpu/0/temperature/5");
    public double? CpuPower => Read("libre/amdcpu/0/power/0");
    public double? CpuVoltage => Read("cpu/core-voltage");
    public double? CpuClock => Read("libre/amdcpu/0/clock/1");
    public double? CpuUsage => Read("cpu/utilization");
    public double? MemoryClock => Read("ram/mclk");
    public double? MemoryDataRate => Read("ram/data-rate");
    private Reading? FpsReading => snapshot.Readings.FirstOrDefault(r => r.Source == PresentMonSource.SourceName)
        ?? snapshot.Readings.FirstOrDefault(r => r.Source == FrameViewSource.SourceName);
    public string? FpsId => FpsReading?.Id;
    public string? FpsApplication => FpsReading is { Name.Length: > 0 } reading
        ? reading.Name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? Path.GetFileNameWithoutExtension(reading.Name) : reading.Name : null;
    public double? Fps => FpsId is null ? null : Read(FpsId);
    public string FpsDetail => FpsReading?.Detail ?? "Windows ETW/PresentMon game FPS; select a game from the right-click menu.";
    public double? Gpu(string suffix) => gpuPrefix is null ? null : Read(gpuPrefix + suffix);
    public double? BoardSystem => Read("libre/lpc/it8696e/0/temperature/0");
    public double? BoardPch => Read("libre/lpc/it8696e/0/temperature/1");
    public double? BoardVrm => Read("libre/lpc/it8696e/0/temperature/4");
    public double? CpuFan => Read("libre/lpc/it8696e/0/fan/0");
    public double? CpuOptionalFan => Read("libre/lpc/it8696e/0/fan/4");
    public double? BoardPump => Read("libre/lpc/it87952e/0/fan/0");

    private double? Read(string id)
    {
        var maxAge = TimeSpan.FromMilliseconds(Math.Max(5000, pollingIntervalMs * 2));
        if (!savedPreview && (now - snapshot.CapturedAt > maxAge || snapshot.CapturedAt > now.AddSeconds(1))) return null;
        var reading = snapshot.Readings.FirstOrDefault(r => r.Id == id);
        if (reading is not { Status: "ok", Value: not null, SampledAt: not null } || !double.IsFinite(reading.Value.Value)) return null;
        var readingMaxAge = reading.Unit == "FPS" ? TimeSpan.FromSeconds(5) : maxAge;
        if (!savedPreview && (now - reading.SampledAt.Value > readingMaxAge || reading.SampledAt.Value > now.AddSeconds(1))) return null;
        return reading.Value;
    }

    // Hardware-independent checks protect against displaying old/failed data,
    // mixing the AMD integrated GPU with NVIDIA, or using board CPU as Tctl/Tdie.
    public static int Check()
    {
        PanelPreferences.CheckDisplaySelection();
        DisplayRecovery.Check();
        PanelLayout.Check();
        ShipPanel.Check();
        Holotable.CheckResources();
        PanelTheme.Check();
        ExpanseMedia.Check();
        if (PanelPreferences.ValidPollingInterval(500) != 500 || PanelPreferences.ValidPollingInterval(5000) != 5000 ||
            PanelPreferences.ValidPollingInterval(499) != 1000 || PanelPreferences.ValidPollingInterval(5001) != 1000 ||
            PanelWindow.PollingWaitMilliseconds(1000, 30) != 970 || PanelWindow.PollingWaitMilliseconds(500, 700) != 0)
            throw new Exception("Polling bounds and non-overlapping start-to-start scheduling failed.");
        var now = DateTimeOffset.UtcNow;
        var readings = new Reading[]
        {
            new("libre/amdcpu/0/temperature/2", "CPU", 60, "°C", "libre", "ok", SampledAt: now),
            new("libre/lpc/it8696e/0/temperature/2", "Board CPU", 42, "°C", "libre", "ok", SampledAt: now),
            new("nvidia/GPU-test/temperature", "NVIDIA example GPU: Temperature", 35, "°C", "NVIDIA NVML", "ok", SampledAt: now)
        };
        var snapshot = new Snapshot(now, true, "Example motherboard", "Example CPU", readings, []);
        if (new PanelMetrics(snapshot, now).CpuTemperature != 60 || new PanelMetrics(snapshot, now).Gpu("temperature") != 35)
            throw new Exception("CPU package and selected NVIDIA sensor mapping failed.");
        if (new PanelMetrics(snapshot, now.AddSeconds(6)).CpuTemperature is not null)
            throw new Exception("Stale snapshots must clear the display.");
        if (new PanelMetrics(snapshot, now.AddSeconds(6), pollingIntervalMs: 5000).CpuTemperature != 60 ||
            new PanelMetrics(snapshot, now.AddSeconds(11), pollingIntervalMs: 5000).CpuTemperature is not null)
            throw new Exception("Slow polling must allow its configured cadence and still expire old readings.");
        var failed = readings.Select(r => r with { Status = "unavailable" }).ToArray();
        if (new PanelMetrics(snapshot with { Readings = failed }, now).CpuTemperature is not null)
            throw new Exception("Unavailable readings must never display old values.");
        if (new PanelMetrics(snapshot with { Readings = [readings[1]] }, now).CpuTemperature is not null)
            throw new Exception("The motherboard CPU sensor must not be substituted for CPU Tctl/Tdie.");
        var staleReading = readings[0] with { SampledAt = now.AddSeconds(-6) };
        if (new PanelMetrics(snapshot with { Readings = [staleReading] }, now).CpuTemperature is not null)
            throw new Exception("Fresh snapshots must not hide stale individual readings.");
        var cooling = new Reading[]
        {
            new("libre/amdcpu/0/temperature/5", "CCDs Max (Tdie)", 52, "°C", "libre", "ok", SampledAt: now),
            new("libre/lpc/it8696e/0/fan/0", "CPU Fan", 660, "RPM", "libre", "ok", SampledAt: now),
            new("libre/lpc/it8696e/0/fan/4", "CPU Optional Fan", 1180, "RPM", "libre", "ok", SampledAt: now),
            new("libre/lpc/it87952e/0/fan/0", "System Fan #5 / Pump", 3070, "RPM", "libre", "ok", SampledAt: now)
        };
        var coolingMetrics = new PanelMetrics(snapshot with { Readings = cooling }, now);
        if (coolingMetrics.CpuCcdMax != 52 || coolingMetrics.CpuFan != 660 || coolingMetrics.CpuOptionalFan != 1180 || coolingMetrics.BoardPump != 3070)
            throw new Exception("CCD hotspot and motherboard cooling header mappings failed.");
        foreach (var unavailable in new[] { Array.Empty<Reading>(), cooling.Select(r => r with { Status = "unavailable" }).ToArray(), cooling.Select(r => r with { SampledAt = now.AddSeconds(-6) }).ToArray() })
        {
            var missing = new PanelMetrics(snapshot with { Readings = unavailable }, now);
            if (missing.CpuCcdMax is not null || missing.CpuFan is not null || missing.CpuOptionalFan is not null || missing.BoardPump is not null)
                throw new Exception("Missing, failed or stale cooling readings must clear instead of being inferred.");
        }
        var memory = new Reading("ram/mclk", "MCLK", 2994, "MHz", "UMC", "ok", SampledAt: now);
        var rate = memory with { Id = "ram/data-rate", Value = 5988, Unit = "MT/s" };
        var fps = new Reading("frameview/1/2/fps", "Application FPS", 123, "FPS", FrameViewSource.SourceName, "ok", SampledAt: now);
        var expanded = new PanelMetrics(snapshot with { Readings = [memory, rate, fps] }, now);
        if (expanded.MemoryClock != 2994 || expanded.MemoryDataRate != 5988 || expanded.Fps != 123 ||
            new PanelMetrics(snapshot with { Readings = [fps with { SampledAt = now.AddSeconds(-6) }] }, now).Fps is not null)
            throw new Exception("RAM clock/data-rate mapping or FPS freshness failed.");
        var windowsFps = fps with { Id = "presentmon/1/2/fps", Name = "Game.exe", Value = 159, Source = PresentMonSource.SourceName };
        if (new PanelMetrics(snapshot with { Readings = [fps, windowsFps] }, now).Fps != 159 ||
            new PanelMetrics(snapshot with { Readings = [windowsFps with { SampledAt = now.AddSeconds(-6) }] }, now).Fps is not null)
            throw new Exception("Windows display FPS must retain its source and reject stale data.");
        if (new PanelMetrics(snapshot with { Readings = [windowsFps] }, now).FpsApplication != "Game" ||
            new PanelMetrics(snapshot with { Readings = [windowsFps with { Name = "Game.Name 2" }] }, now).FpsApplication != "Game.Name 2")
            throw new Exception("FPS source must preserve catalog game names and trim only executable extensions.");
        var measuredVoltage = new Reading("cpu/core-voltage", "Measured Vcore", 1.323, "V", "AMD SVI3", "ok", SampledAt: now);
        var requestedVid = measuredVoltage with { Id = "cpu/core-vid", Value = 1.346 };
        var boardVoltage = measuredVoltage with { Id = "libre/lpc/it87952e/0/voltage/0", Value = 1.837, Status = "unmapped" };
        if (new PanelMetrics(snapshot with { Readings = [boardVoltage, requestedVid, measuredVoltage] }, now).CpuVoltage != 1.323 ||
            new PanelMetrics(snapshot with { Readings = [boardVoltage, requestedVid] }, now).CpuVoltage is not null ||
            new PanelMetrics(snapshot with { Readings = [measuredVoltage with { Status = "unavailable" }] }, now).CpuVoltage is not null)
            throw new Exception("Vcore must use measured CPU rail telemetry, never requested VID or an unverified motherboard rail.");
        File.WriteAllText(Path.Combine(PanelPaths.Artifacts, "panel-mapping-check.txt"),
            "PASS: CPU package/CCD hotspot, NVIDIA identity, motherboard cooling headers, RAM MHz/MT/s mapping, FPS mapping, stale snapshots/readings, and reported failures.\n");
        return 0;
    }
}
