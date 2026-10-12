namespace SensorPanel;

// Retain the selected monitor while it is absent. A fallback monitor must not
// become the new target, and a deliberate tray hide must survive reconnection.
internal sealed class DisplayRecovery(DisplayInfo target)
{
    public DisplayInfo Target { get; private set; } = target;
    public bool Waiting { get; private set; }
    private bool resume;

    public void Select(DisplayInfo target) { Target = target; Waiting = false; resume = false; }
    public void HideByUser() => resume = false;
    public DisplayInfo? Connected(IReadOnlyList<DisplayInfo> displays) =>
        displays.FirstOrDefault(d => !string.IsNullOrWhiteSpace(Target.MonitorId)
            ? string.Equals(d.MonitorId, Target.MonitorId, StringComparison.OrdinalIgnoreCase)
            : string.Equals(d.Name, Target.Name, StringComparison.OrdinalIgnoreCase));

    public (DisplayInfo? Display, bool Show) Refresh(IReadOnlyList<DisplayInfo> displays, bool visible)
    {
        var connected = Connected(displays);
        if (connected is null || (!Target.Primary && connected.Primary))
        {
            Waiting = true;
            resume |= visible && !Target.Primary;
            return (null, false);
        }
        bool show = resume && !connected.Primary;
        Select(connected);
        return (connected, show);
    }

    internal static void Check()
    {
        var primary = new DisplayInfo("primary", 0, 0, 3840, 2160, true, "primary-id");
        var sensor = new DisplayInfo("sensor", -1920, 1080, 1920, 1080, false, "sensor-id");
        var other = sensor with { Name = "other", MonitorId = "other-id" };
        var recovery = new DisplayRecovery(sensor);
        if (recovery.Refresh([primary, other], true).Display is not null || !recovery.Waiting || recovery.Target != sensor)
            throw new InvalidOperationException("Disconnect must retain the target and hide rather than fall back.");
        var returned = sensor with { Name = "renumbered", Left = 3840, Top = 0 };
        var restored = recovery.Refresh([primary, returned], false);
        if (restored.Display != returned || !restored.Show || recovery.Waiting)
            throw new InvalidOperationException("Reconnect must restore the visible secondary panel at its current bounds.");
        recovery.Refresh([primary], true);
        recovery.HideByUser();
        if (recovery.Refresh([primary, returned], false).Show)
            throw new InvalidOperationException("A deliberate tray hide must cancel automatic restoration.");
        recovery.Refresh([primary], false);
        if (recovery.Refresh([primary, returned], false).Show)
            throw new InvalidOperationException("An already hidden panel must stay hidden after reconnection.");
        recovery.Refresh([primary], true);
        if (recovery.Refresh([returned with { Primary = true }], false).Display is not null ||
            !recovery.Refresh([primary, returned], false).Show)
            throw new InvalidOperationException("Automatic recovery must wait while the sensor becomes the primary workspace.");
        recovery.Refresh([primary], true);
        recovery.Select(other);
        if (recovery.Refresh([primary, other], false).Show || recovery.Target != other)
            throw new InvalidOperationException("Explicit display selection must clear pending recovery.");
    }
}
