using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.ComponentModel;

namespace SensorPanel;

internal sealed record DisplayInfo(string Name, int Left, int Top, int Width, int Height, bool Primary, string MonitorId = "");

internal static class Displays
{
    public static IReadOnlyList<DisplayInfo> List()
    {
        var result = new List<DisplayInfo>();
        MonitorCallback callback = (nint monitor, nint dc, ref NativeRect rectangle, nint data) =>
        {
            var information = new MonitorInfo { Size = (uint)Marshal.SizeOf<MonitorInfo>(), Device = "" };
            if (GetMonitorInfo(monitor, ref information))
            {
                var device = new DisplayDevice { Size = (uint)Marshal.SizeOf<DisplayDevice>() };
                string id = EnumDisplayDevices(information.Device, 0, ref device, 1) ? device.Id : "";
                result.Add(new(information.Device, information.Monitor.Left, information.Monitor.Top,
                    information.Monitor.Right - information.Monitor.Left, information.Monitor.Bottom - information.Monitor.Top,
                    (information.Flags & 1) != 0, id));
            }
            return true;
        };
        EnumDisplayMonitors(0, 0, callback, 0);
        return result.OrderByDescending(d => d.Primary).ToArray();
    }

    public static void Place(Window window, DisplayInfo display, bool fit, Size? previewSize = null)
    {
        var bounds = Placement(display, fit);
        if (previewSize is { } size)
        {
            double scale = Math.Min(1,Math.Min(display.Width*0.9/size.Width,display.Height*0.9/size.Height));
            int width=(int)Math.Round(size.Width*scale),height=(int)Math.Round(size.Height*scale);
            bounds=new(display.Left+(display.Width-width)/2,display.Top+(display.Height-height)/2,width,height);
        }
        nint handle = new WindowInteropHelper(window).Handle;
        if (handle == 0) return;
        double dpi = Math.Max(96, GetDpiForWindow(handle)) / 96.0;
        window.MinWidth = fit ? 0 : Math.Min(320, bounds.Width / dpi);
        window.MinHeight = fit ? 0 : Math.Min(180, bounds.Height / dpi);
        window.ResizeMode = fit ? ResizeMode.NoResize : ResizeMode.CanResizeWithGrip;
        if (!SetWindowPos(handle, 0, bounds.Left, bounds.Top, bounds.Width, bounds.Height, 0x0014))
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Unable to position the panel on the selected monitor.");
    }
    internal sealed record Bounds(int Left, int Top, int Width, int Height);
    internal static Bounds CurrentBounds(Window window)
    {
        if (!GetWindowRect(new WindowInteropHelper(window).Handle,out var rectangle)) throw new Win32Exception(Marshal.GetLastWin32Error());
        return new(rectangle.Left,rectangle.Top,rectangle.Right-rectangle.Left,rectangle.Bottom-rectangle.Top);
    }
    internal static uint Dpi(Window window) => GetDpiForWindow(new WindowInteropHelper(window).Handle);
    internal static Bounds Placement(DisplayInfo display, bool fit)
    {
        double scale = Math.Min(64, Math.Min(display.Width / 16.0, display.Height / 9.0));
        int width = fit ? display.Width : (int)Math.Round(scale * 16);
        int height = fit ? display.Height : (int)Math.Round(scale * 9);
        return new(display.Left + (display.Width - width) / 2, display.Top + (display.Height - height) / 2, width, height);
    }

    [StructLayout(LayoutKind.Sequential)] private struct NativeRect { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)] private struct MonitorInfo
    {
        public uint Size;
        public NativeRect Monitor, Work;
        public uint Flags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string Device;
    }
    private delegate bool MonitorCallback(nint monitor, nint dc, ref NativeRect rectangle, nint data);
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)] private struct DisplayDevice
    {
        public uint Size;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string Name;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string Description;
        public uint Flags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string Id;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string Key;
    }
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EnumDisplayDevices(string device, uint index, ref DisplayDevice information, uint flags);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EnumDisplayMonitors(nint dc, nint clip, MonitorCallback callback, nint data);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetMonitorInfo(nint monitor, ref MonitorInfo information);
    [DllImport("user32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPos(nint window, nint after, int x, int y, int width, int height, uint flags);
    [DllImport("user32.dll")] private static extern uint GetDpiForWindow(nint window);
    [DllImport("user32.dll",SetLastError=true)] [return:MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetWindowRect(nint window,out NativeRect rectangle);
}
