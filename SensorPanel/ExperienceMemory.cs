namespace SensorPanel;

internal static class ExperienceMemory
{
    internal static void Apply(bool expanse)
    {
        System.Windows.Media.RenderOptions.ProcessRenderMode=expanse?System.Windows.Interop.RenderMode.Default:System.Windows.Interop.RenderMode.SoftwareOnly;
    }
}

internal sealed partial class PanelWindow
{
    private long aorusAllocated=GC.GetTotalAllocatedBytes(false);
    private bool aorusCollectionQueued,aorusFullCollection;
    private int aorusCollections;
    private double aorusCollectionMaxMilliseconds;
    private void CollectAorusGarbage(bool full=false)
    {
        if(closing||theme.ShipConsole)return;
        aorusFullCollection|=full;
        if(aorusCollectionQueued||!full&&GC.GetTotalAllocatedBytes(false)-aorusAllocated<2_000_000)return;
        aorusCollectionQueued=true;
        // Run after the UI event's temporary controls have left the stack.
        // No hard heap limit and no eviction of still-live resident pages.
        Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.ContextIdle,new Action(()=>
        {
            aorusCollectionQueued=false;
            bool complete=aorusFullCollection||GC.GetGCMemoryInfo().TotalCommittedBytes>12_000_000;aorusFullCollection=false;
            if(closing||theme.ShipConsole)return;
            long started=System.Diagnostics.Stopwatch.GetTimestamp();
            GC.Collect(complete?GC.MaxGeneration:1,complete?GCCollectionMode.Aggressive:GCCollectionMode.Forced,true,true);
            if(complete)
            {
                var info=new HeapOptimization { Version=1 };
                HeapSetInformation(IntPtr.Zero,3,ref info,(nuint)System.Runtime.InteropServices.Marshal.SizeOf<HeapOptimization>());
            }
            aorusCollectionMaxMilliseconds=Math.Max(aorusCollectionMaxMilliseconds,System.Diagnostics.Stopwatch.GetElapsedTime(started).TotalMilliseconds);
            aorusAllocated=GC.GetTotalAllocatedBytes(false);aorusCollections++;
        }));
    }
}
