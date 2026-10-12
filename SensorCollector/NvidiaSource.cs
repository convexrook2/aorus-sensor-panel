using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32;

namespace SensorCollector;

internal sealed class NvidiaSource : IDisposable
{
    private nint library;
    private bool initialized;
    private readonly List<(nint Handle, string Id, string Name)> devices = [];
    private SourceStatus status = new("NVIDIA NVML", "unavailable", "Not initialized.");

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int NoArgs();
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int GetCount(out uint count);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int GetHandle(uint index, out nint device);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int GetText(nint device, [Out] byte[] buffer, uint length);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int GetUint(nint device, out uint value);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int GetUintWithKind(nint device, uint kind, out uint value);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int GetUtilization(nint device, out Utilization value);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int GetMemory(nint device, out Memory value);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate nint ErrorString(int code);
    [StructLayout(LayoutKind.Sequential)] private struct Utilization { public uint Gpu, Memory; }
    [StructLayout(LayoutKind.Sequential)] private struct Memory { public ulong Total, Free, Used; }

    public NvidiaSource()
    {
        try
        {
            string dll = ResolveLibrary();
            library = NativeLibrary.Load(dll);
            Check(Function<NoArgs>("nvmlInit_v2")());
            initialized = true;
            Check(Function<GetCount>("nvmlDeviceGetCount_v2")(out uint count));
            if (count > 64) throw new InvalidOperationException("Unexpected NVIDIA device count.");
            for (uint index = 0; index < count; index++)
            {
                int result = Function<GetHandle>("nvmlDeviceGetHandleByIndex_v2")(index, out nint handle);
                if (result != 0) continue;
                string id = Text(handle, "nvmlDeviceGetUUID");
                devices.Add((handle, id, Text(handle, "nvmlDeviceGetName")));
            }
            status = new("NVIDIA NVML", devices.Count > 0 ? "available" : "unavailable",
                $"{devices.Count} accessible GPU(s). Loaded the installed driver library: {dll}");
        }
        catch (Exception ex) { status = new("NVIDIA NVML", "unavailable", ex.Message); }
    }

    private static string ResolveLibrary()
    {
        // The active driver service selects the right DriverStore version; do not
        // pick an arbitrary old DLL from a recursive directory search.
        var paths = new List<string> { Path.Combine(Environment.SystemDirectory, "nvml.dll"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "NVIDIA Corporation", "NVSMI", "nvml.dll") };
        using var key = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Services\nvlddmkm");
        string? image = key?.GetValue("ImagePath") as string;
        if (image is not null)
        {
            image = Environment.ExpandEnvironmentVariables(image.Trim('"'));
            if (image.StartsWith(@"\SystemRoot\", StringComparison.OrdinalIgnoreCase))
                image = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), image[12..]);
            if (image.StartsWith(@"\??\", StringComparison.Ordinal)) image = image[4..];
            string? directory = Path.GetDirectoryName(image);
            if (directory is not null && Path.IsPathFullyQualified(directory))
                paths.Insert(0, Path.Combine(directory, "nvml.dll"));
        }
        return paths.FirstOrDefault(File.Exists) ?? throw new FileNotFoundException("NVML was not found beside the active NVIDIA driver or in its standard install paths.");
    }

    private T Function<T>(string name) where T : Delegate
        => Marshal.GetDelegateForFunctionPointer<T>(NativeLibrary.GetExport(library, name));

    private void Check(int result)
    {
        if (result != 0) throw new InvalidOperationException(Error(result));
    }

    private string Error(int result)
    {
        string? text = null;
        if (library != 0 && NativeLibrary.TryGetExport(library, "nvmlErrorString", out nint pointer))
            text = Marshal.PtrToStringAnsi(Marshal.GetDelegateForFunctionPointer<ErrorString>(pointer)(result));
        return $"NVML {result}: {text ?? "unknown error"}";
    }

    private string Text(nint handle, string name)
    {
        byte[] buffer = new byte[128];
        Check(Function<GetText>(name)(handle, buffer, (uint)buffer.Length));
        int end = Array.IndexOf(buffer, (byte)0);
        return Encoding.UTF8.GetString(buffer, 0, end < 0 ? buffer.Length : end);
    }

    public (List<Reading> Readings, SourceStatus Status) Read()
    {
        var readings = new List<Reading>();
        foreach (var device in devices)
        {
            void Add(string id, string label, string unit, Func<(int Code, double Value)> query)
            {
                try
                {
                    var result = query();
                    readings.Add(new($"nvidia/{device.Id}/{id}", $"{device.Name}: {label}",
                        result.Code == 0 ? result.Value : null, unit, "NVIDIA NVML",
                        result.Code == 0 ? "ok" : result.Code == 3 ? "unsupported" : "unavailable",
                        result.Code == 0 ? null : Error(result.Code), SampledAt: DateTimeOffset.UtcNow));
                }
                catch (Exception ex)
                {
                    readings.Add(new($"nvidia/{device.Id}/{id}", $"{device.Name}: {label}", null,
                        unit, "NVIDIA NVML", "unavailable", ex.Message));
                }
            }
            Add("temperature", "GPU temperature", "°C", () =>
            {
                int code = Function<GetUintWithKind>("nvmlDeviceGetTemperature")(device.Handle, 0, out uint value);
                return (code, value);
            });
            Add("power", "Reported GPU power", "W", () =>
            {
                int code = Function<GetUint>("nvmlDeviceGetPowerUsage")(device.Handle, out uint value);
                return (code, value / 1000.0);
            });
            Add("utilization", "GPU utilization", "%", () =>
            {
                int code = Function<GetUtilization>("nvmlDeviceGetUtilizationRates")(device.Handle, out var value);
                return (code, value.Gpu);
            });
            Add("graphics-clock", "Graphics clock", "MHz", () =>
            {
                int code = Function<GetUintWithKind>("nvmlDeviceGetClockInfo")(device.Handle, 0, out uint value);
                return (code, value);
            });
            Add("memory-clock", "Memory clock", "MHz", () =>
            {
                int code = Function<GetUintWithKind>("nvmlDeviceGetClockInfo")(device.Handle, 2, out uint value);
                return (code, value);
            });
            Add("memory-used", "VRAM used", "MiB", () =>
            {
                int code = Function<GetMemory>("nvmlDeviceGetMemoryInfo")(device.Handle, out var value);
                return (code, value.Used / 1048576.0);
            });
            Add("fan", "GPU fan duty", "%", () =>
            {
                int code = Function<GetUint>("nvmlDeviceGetFanSpeed")(device.Handle, out uint value);
                return (code, value);
            });
        }
        return (readings, status);
    }

    public void Dispose()
    {
        if (initialized) { Function<NoArgs>("nvmlShutdown")(); initialized = false; }
        if (library != 0) { NativeLibrary.Free(library); library = 0; }
    }
}
