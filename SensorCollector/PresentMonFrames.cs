namespace SensorCollector;

public sealed record FpsApplication(int ProcessId, string Name, double FramesPerSecond);

// Called under PresentMonSource's gate. Frame capture stays independent from
// game eligibility, so switching games never restarts the working ETW helper.
internal sealed class PresentMonFrames
{
    internal sealed record Frame(string Application, int Pid, string SwapChain, double QpcMs, double DisplayMs);
    private readonly Dictionary<(int Pid, string Chain), Queue<Frame>> streams = new();

    internal void Add(Frame frame, double now)
    {
        if (frame.Pid <= 0 || !double.IsFinite(frame.QpcMs) || !double.IsFinite(frame.DisplayMs) ||
            frame.DisplayMs <= 0 || now - frame.QpcMs is < 0 or > 3000) return;
        Prune(now);
        var key = (frame.Pid, frame.SwapChain);
        if (!streams.TryGetValue(key, out var queue))
        {
            if (streams.Count >= 256) return;
            streams[key] = queue = new();
        }
        if (queue.Count > 0 && frame.QpcMs <= queue.Last().QpcMs) return;
        queue.Enqueue(frame);
        while (queue.Count > 0 && (frame.QpcMs - queue.Peek().QpcMs > 1000 || queue.Count > 4096)) queue.Dequeue();
    }

    internal FpsApplication[] Applications(double now)
    {
        Prune(now);
        return streams.Values.Where(q => q.Count >= 2).GroupBy(q => q.Last().Pid)
            .Select(g => {
                var queue = g.OrderByDescending(q => q.Count).First();
                return new FpsApplication(g.Key, queue.Last().Application, PresentMonSource.AverageFps(queue.Select(f => f.DisplayMs)));
            })
            .OrderBy(a => a.Name, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    internal int Select(int foreground, int previous, double now)
    {
        var applications = Applications(now);
        if (applications.Any(a => a.ProcessId == foreground)) return foreground;
        if (applications.Any(a => a.ProcessId == previous)) return previous;
        return streams.Values.Where(q => q.Count >= 2).OrderByDescending(q => q.Count)
            .Select(q => q.Last().Pid).FirstOrDefault();
    }

    internal FpsApplication[] Games(IReadOnlyList<RunningGame> running, double now)
    {
        var names = running.ToDictionary(game => game.ProcessId, game => game.Name);
        return Applications(now).Where(app => names.ContainsKey(app.ProcessId))
            .Select(app => app with { Name = names[app.ProcessId] }).ToArray();
    }

    internal Frame[] Read(int pid, double now)
    {
        Prune(now);
        return streams.Where(s => s.Key.Pid == pid && s.Value.Count >= 2)
            .OrderByDescending(s => s.Value.Count).Select(s => s.Value.ToArray()).FirstOrDefault() ?? [];
    }

    private void Prune(double now)
    {
        foreach (var key in streams.Where(s => now - s.Value.Last().QpcMs is < 0 or > 3000).Select(s => s.Key).ToArray()) streams.Remove(key);
    }
}
