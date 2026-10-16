namespace SensorCollector;

// Actual DWM ETW CurrentFrameId observations, not refresh-rate settings or
// projected timing APIs. Missing IDs are never counted as received frames.
internal sealed class DesktopFrameWindow
{
    private readonly Queue<(ulong Id, double Time)> frames = new();
    private int processId;
    internal void Add(int pid, ulong id, double stamp, double now)
    {
        if (pid <= 0 || !double.IsFinite(stamp) || now - stamp is < 0 or > 3000) return;
        if (processId != pid) { processId = pid; frames.Clear(); }
        if (frames.Count > 0 && stamp - frames.Last().Time > 3000) frames.Clear();
        if (frames.Count > 0 && (id <= frames.Last().Id || stamp <= frames.Last().Time)) return;
        frames.Enqueue((id, stamp));
        while (frames.Count > 1 && (stamp - frames.Peek().Time > 1000 || frames.Count > 4096)) frames.Dequeue();
    }

    internal (double Fps, double Time, int Pid, int Count)? Read(double now)
    {
        if (frames.Count < 2 || now - frames.Last().Time is < 0 or > 3000) return null;
        double duration = frames.Last().Time - frames.Peek().Time;
        if (duration <= 0) return null;
        return (1000.0 * (frames.Count - 1) / duration, frames.Last().Time, processId, frames.Count);
    }

    internal static bool TryParse(string line, out int pid, out ulong frameId, out double stamp)
    {
        pid = 0; frameId = 0; stamp = 0;
        var fields = line.Split(',');
        if (fields.Length != 6 || fields[0] != "#X890DesktopFrame" ||
            !int.TryParse(fields[1], out pid) || pid <= 0 ||
            !uint.TryParse(fields[2], out _) || !ulong.TryParse(fields[3], out frameId) ||
            !ulong.TryParse(fields[4], out ulong ticks) || !ulong.TryParse(fields[5], out ulong frequency) || frequency == 0) return false;
        stamp = ticks * 1000.0 / frequency;
        return double.IsFinite(stamp);
    }
}
