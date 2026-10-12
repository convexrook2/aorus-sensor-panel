using System.Runtime.InteropServices;

namespace SensorCollector;

internal sealed class GigabyteSource : IDisposable
{
    private WindowsWmi? wmi;
    private object? definition;
    private string? path;
    private SourceStatus status = new("Gigabyte ACPI/WMI", "unavailable", "Not initialized.");

    public GigabyteSource(string? manufacturer)
    {
        if (manufacturer?.Contains("Gigabyte", StringComparison.OrdinalIgnoreCase) != true)
        {
            status = status with { Detail = "This collector only probes the verified Gigabyte interface." };
            return;
        }
        string stage = "connect to root/WMI";
        try
        {
            wmi = new WindowsWmi(@"root\WMI");
            stage = "load Gigabyte class definition";
            definition = wmi.Services.Get("GSA1_ACPIMethod");
            dynamic cls = definition;
            // Confirm the actual firmware contract before invoking anything.
            stage = "verify Gigabyte GUID and method ID";
            string guid = Convert.ToString(cls.Qualifiers_.Item("guid").Value) ?? "";
            int methodId = Convert.ToInt32(cls.Methods_.Item("ZFCGetCurrentTemp")
                .Qualifiers_.Item("WmiMethodId").Value);
            if (!guid.Trim('{', '}').Equals("DEADBEEF-2001-0000-00A0-C90629100000",
                StringComparison.OrdinalIgnoreCase) || methodId != 0x125)
                throw new InvalidOperationException("Gigabyte firmware contract does not match GUID/method 0x125.");
            // Use the real returned instance path. Never invent an ACPI instance.
            stage = "enumerate the active firmware instance and its system path";
            var instances = wmi.Query("SELECT __PATH, Active FROM GSA1_ACPIMethod", "__PATH", "Active");
            var active = instances.Where(x => x["Active"] is true).ToArray();
            if (active.Length != 1)
                throw new InvalidOperationException($"Expected one active Gigabyte interface, found {active.Length}.");
            path = Convert.ToString(active[0]["__PATH"]);
            status = new("Gigabyte ACPI/WMI", "available", "Verified GUID and ZFCGetCurrentTemp method 0x125; six raw channels. Channel labels require mapping.");
        }
        catch (Exception ex) { status = Failure(ex, stage); }
    }

    public (List<Reading> Readings, SourceStatus Status) Read()
    {
        var readings = new List<Reading>();
        if (path is null || wmi is null || definition is null)
            return (Missing(status), status);
        for (int id = 0; id < 6; id++)
        {
            object? input = null, output = null;
            string stage = "create query parameters";
            try
            {
                // Exact allowlisted query. No arbitrary register/memory/PCI methods.
                input = ((dynamic)definition).Methods_.Item("ZFCGetCurrentTemp").InParameters.SpawnInstance_();
                stage = "assign channel ID";
                ((dynamic)input).Properties_.Item("id").Value = (byte)id;
                stage = "invoke ZFCGetCurrentTemp";
                output = wmi.Services.ExecMethod(path, "ZFCGetCurrentTemp", input);
                stage = "read returned temperature byte";
                object? value = WindowsWmi.Property(output, "value");
                if (value is null) throw new InvalidOperationException("Firmware returned no temperature value.");
                byte raw = Convert.ToByte(value);
                readings.Add(GigabyteTemperature.Decode(id, raw, DateTimeOffset.UtcNow));
            }
            catch (Exception ex)
            {
                var failure = Failure(ex, $"channel {id}: {stage}");
                readings.Add(new($"gigabyte/temperature/{id}", $"Gigabyte temperature channel {id}",
                    null, "°C", failure.Source, failure.Status, failure.Detail));
            }
            finally { WindowsWmi.Release(output); WindowsWmi.Release(input); }
        }
        var summary = readings.Any(r => r.Value.HasValue)
            ? status : new SourceStatus(status.Source, "unavailable", "No usable firmware temperature channels; see per-channel diagnostics.");
        return (readings, summary);
    }

    private static List<Reading> Missing(SourceStatus error) => Enumerable.Range(0, 6)
        .Select(id => new Reading($"gigabyte/temperature/{id}", $"Gigabyte temperature channel {id}",
            null, "°C", error.Source, error.Status, error.Detail)).ToList();

    private static SourceStatus Failure(Exception ex, string stage)
    {
        bool denied = ex is UnauthorizedAccessException || ex.HResult is unchecked((int)0x80041003)
            or unchecked((int)0x80070005);
        return new("Gigabyte ACPI/WMI", denied ? "access-denied" : "unavailable",
            $"{stage}: {ex.Message} (0x{ex.HResult:X8})." + (denied
                ? " Retry the same collector from an administrator terminal; the collector never self-elevates." : ""));
    }

    public void Dispose() { WindowsWmi.Release(definition); wmi?.Dispose(); }
}
