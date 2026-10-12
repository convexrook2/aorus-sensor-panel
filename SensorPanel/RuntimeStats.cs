using System.Diagnostics;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;

namespace SensorPanel;

internal sealed record RuntimeStats(double? MemoryUsage, double? UploadBytes, double? DownloadBytes, string NetworkName);

internal sealed class RuntimeReader
{
    private NetworkInterface? network;
    private long sent, received, lastTick;

    public RuntimeStats Read()
    {
        var memory = new MemoryStatus { Length = (uint)Marshal.SizeOf<MemoryStatus>() };
        double? memoryUsage = GlobalMemoryStatusEx(ref memory) ? memory.MemoryLoad : null;
        double? upload = null, download = null;
        string name = "No active adapter";
        try
        {
            if (network is null || network.OperationalStatus != OperationalStatus.Up)
            {
                network = NetworkInterface.GetAllNetworkInterfaces()
                    .Where(n => n.OperationalStatus == OperationalStatus.Up && n.NetworkInterfaceType is NetworkInterfaceType.Ethernet or NetworkInterfaceType.Wireless80211)
                    .OrderByDescending(n => n.GetIPv4Statistics().BytesReceived + n.GetIPv4Statistics().BytesSent).FirstOrDefault();
                lastTick = 0;
            }
            if (network is not null)
            {
                name = network.Name;
                var current = network.GetIPv4Statistics();
                long tick = Stopwatch.GetTimestamp();
                double seconds = (tick - lastTick) / (double)Stopwatch.Frequency;
                if (lastTick > 0 && seconds > 0 && current.BytesSent >= sent && current.BytesReceived >= received)
                {
                    upload = (current.BytesSent - sent) / seconds;
                    download = (current.BytesReceived - received) / seconds;
                }
                sent = current.BytesSent;
                received = current.BytesReceived;
                lastTick = tick;
            }
        }
        catch (NetworkInformationException) { network = null; }
        return new(memoryUsage, upload, download, name);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MemoryStatus
    {
        public uint Length, MemoryLoad;
        public ulong TotalPhysical, AvailablePhysical, TotalPageFile, AvailablePageFile, TotalVirtual, AvailableVirtual, AvailableExtendedVirtual;
    }
    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalMemoryStatusEx(ref MemoryStatus memory);
}
