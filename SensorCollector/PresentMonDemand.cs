namespace SensorCollector;

// One capture can span foreground changes and multiple games. With no eligible
// games, cancel its pipe read and release the ETW helper instead of collecting
// desktop events only to discard them. All cancellation/disposal is serialized.
internal sealed class PresentMonDemand : IDisposable
{
    private readonly object gate = new();
    private readonly ManualResetEventSlim available = new(false);
    private CancellationTokenSource? active;
    private bool wanted;

    internal void Update(bool hasGames)
    {
        lock (gate)
        {
            wanted = hasGames;
            if (wanted) available.Set();
            else { available.Reset(); active?.Cancel(); }
        }
    }

    internal CancellationToken Acquire(CancellationToken shutdown)
    {
        while (true)
        {
            available.Wait(shutdown);
            lock (gate)
            {
                shutdown.ThrowIfCancellationRequested();
                if (!wanted) continue;
                if (active is not null) throw new InvalidOperationException("A capture is already active.");
                active = CancellationTokenSource.CreateLinkedTokenSource(shutdown);
                return active.Token;
            }
        }
    }

    internal void Release()
    {
        lock (gate) { active?.Dispose(); active = null; }
    }

    public void Dispose() { Release(); available.Dispose(); }
}
