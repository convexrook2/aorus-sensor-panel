namespace SensorCollector;

public sealed record Reading(string Id, string Name, double? Value, string Unit,
    string Source, string Status, string? Detail = null, long? RawValue = null,
    DateTimeOffset? SampledAt = null);

public sealed record SourceStatus(string Source, string Status, string Detail);

public sealed record Snapshot(DateTimeOffset CapturedAt, bool Elevated,
    string? Motherboard, string? Cpu, IReadOnlyList<Reading> Readings,
    IReadOnlyList<SourceStatus> Sources);

public static class GigabyteTemperature
{
    // GSA1 method 0x125: the upstream Gigabyte WMI driver documents zero as
    // absent and the low byte as a signed integer Celsius temperature.
    public static Reading Decode(int channel, byte raw, DateTimeOffset time)
        => new($"gigabyte/temperature/{channel}", $"Gigabyte temperature channel {channel}",
            raw == 0 ? null : unchecked((sbyte)raw), "°C", "Gigabyte ACPI/WMI",
            raw == 0 ? "unavailable" : "unmapped",
            raw == 0 ? "Firmware returned the absent-sensor sentinel."
                     : "Channel identity is unverified; this is not labelled CPU/package temperature.",
            raw, time);
}
