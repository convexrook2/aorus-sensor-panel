using System.Runtime.InteropServices;

namespace SensorCollector;

// Windows' built-in Automation interface: no NuGet or external sensor library.
internal sealed class WindowsWmi : IDisposable
{
    private readonly object locator;
    internal readonly dynamic Services;

    public WindowsWmi(string space)
    {
        locator = Activator.CreateInstance(Type.GetTypeFromProgID("WbemScripting.SWbemLocator", true)!)!;
        try
        {
            Services = ((dynamic)locator).ConnectServer(".", space);
            Services.Security_.ImpersonationLevel = 3;
        }
        catch { Release(locator); throw; }
    }

    public List<Dictionary<string, object?>> Query(string query, params string[] fields)
    {
        var result = new List<Dictionary<string, object?>>();
        // These are small, bounded identity/provider queries. A bidirectional
        // result allows ItemIndex and avoids the .NET IDispatch _NewEnum bridge.
        object items = Services.ExecQuery(query, "WQL", 0);
        try
        {
            int count = Convert.ToInt32(((dynamic)items).Count);
            if (count > 64) throw new InvalidOperationException("Unexpected WMI instance count.");
            for (int index = 0; index < count; index++)
            {
                object item = ((dynamic)items).ItemIndex(index);
                try
                {
                    var row = new Dictionary<string, object?>();
                    foreach (string field in fields) row[field] = Property(item, field);
                    result.Add(row);
                }
                finally { Release(item); }
            }
        }
        finally { Release(items); }
        return result;
    }

    public static object? Property(dynamic item, string name)
    {
        // __PATH is WMI metadata, not an ordinary class property. Automation
        // exposes it through SystemProperties_; Properties_.Item throws NOT_FOUND.
        object properties = name.StartsWith("__", StringComparison.Ordinal)
            ? item.SystemProperties_ : item.Properties_;
        object? property = null;
        try { property = ((dynamic)properties).Item(name); return ((dynamic)property).Value; }
        finally { Release(property); Release(properties); }
    }

    public static void Release(object? value)
    {
        if (value is not null && Marshal.IsComObject(value)) Marshal.ReleaseComObject(value);
    }

    public void Dispose() { Release((object)Services); Release(locator); }
}
