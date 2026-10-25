using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using System.Windows.Media;
using System.Runtime.InteropServices;

namespace SensorPanel;

internal enum ExpanseGraphicsMode { FullFidelity, Efficient }

internal sealed partial class PanelWindow
{
    private ExpanseGraphicsMode graphicsMode=PanelPreferences.GraphicsMode;
    private string aorusTheme=PanelPreferences.ExperienceTheme(false),expanseTheme=PanelPreferences.ExperienceTheme(true);
    private Task themeChange=Task.CompletedTask,graphicsRelease=Task.CompletedTask;
    private int themeChangeId;

    private MenuItem ExperienceCommands()
    {
        var group=new MenuItem { Header="Experience" };
        foreach(bool expanse in new[]{false,true})
        {
            var item=new MenuItem { Header=expanse?"The Expanse Experience":"AORUS · Lite Mode",IsCheckable=true };
            group.SubmenuOpened+=(_,_)=>item.IsChecked=theme.ShipConsole==expanse;
            item.Click+=(_,_)=>
            {
                if(theme.ShipConsole==expanse&&themeChange.IsCompleted)return;
                if(theme.ShipConsole)expanseTheme=theme.Id;else aorusTheme=theme.Id;
                ChooseTheme(PanelTheme.Resolve(expanse?expanseTheme:aorusTheme));
            };
            group.Items.Add(item);
        }
        return group;
    }
    private MenuItem GraphicsCommands()
    {
        var group=new MenuItem { Header="Rendering mode" };
        foreach(var mode in new[]{ExpanseGraphicsMode.FullFidelity,ExpanseGraphicsMode.Efficient})
        {
            var item=new MenuItem { Header=mode==ExpanseGraphicsMode.FullFidelity?"Full Fidelity":"Efficient",IsCheckable=true };
            group.SubmenuOpened+=(_,_)=>item.IsChecked=graphicsMode==mode;
            item.Click+=(_,_)=>ApplyGraphicsMode(mode,true);
            group.Items.Add(item);
        }
        return group;
    }
    private async void ChooseTheme(PanelTheme choice)
    {
        themeChange=ChangeThemeAsync(choice);
        await themeChange;
        if(!closing)UpdateShowcaseClock();
    }
    private async Task ChangeThemeAsync(PanelTheme choice)
    {
        int request=++themeChangeId;
        try{await graphicsRelease;}catch(TimeoutException){}
        if(request!=themeChangeId||closing)return;
        if(theme.ShipConsole&&!choice.ShipConsole&&shipPanel is not null)
        {
            // Finish retiring the old experience before making Lite active.
            // This is a mode boundary, never a per-frame/polling collection.
            viewport.Child=null;shipPanel?.SuspendShowcase();
            var retirement=shipPanel?.GraphicsRetirement??Task.CompletedTask;
            shipPanel=null;
            RenderOptions.ProcessRenderMode=System.Windows.Interop.RenderMode.SoftwareOnly;
            if(PresentationSource.FromVisual(this) is System.Windows.Interop.HwndSource source)
                source.CompositionTarget.RenderMode=System.Windows.Interop.RenderMode.SoftwareOnly;
            graphicsRelease=ReclaimGraphicsAsync(retirement);
            try{await graphicsRelease;}
            catch(TimeoutException ex)
            {
                graphicsRelease=Task.CompletedTask;
                System.IO.File.WriteAllText(System.IO.Path.Combine(PanelPaths.Artifacts,"experience-retirement-error.txt"),ex.ToString());
                if(request==themeChangeId&&!closing)ApplyTheme();
                return;
            }
        }
        if(request!=themeChangeId||closing)return;
        if(theme.ShipConsole)expanseTheme=theme.Id;else aorusTheme=theme.Id;
        theme=choice;
        if(choice.ShipConsole)expanseTheme=choice.Id;else aorusTheme=choice.Id;
        if(choice.Showcase)
        {
            showcaseCycle=true;
            if(previewPath is null&&!AorusMemorySampler.Active)PanelPreferences.SaveExpanseCycle(true);
        }
        if(previewPath is null&&!AorusMemorySampler.Active)PanelPreferences.SaveTheme(theme.Id);
        ApplyTheme();
        SaveDisplayState();
    }
    private async Task ReclaimGraphicsAsync(Task retirement)
    {
        // Let WPF retire its composition references, then finalize the now
        // unreachable images/geometry and return their committed heap pages.
        await RetireCompositionAsync();
        await Task.Run(() =>
        {
            GC.Collect(GC.MaxGeneration,GCCollectionMode.Aggressive,true,true);
            GC.WaitForPendingFinalizers();
        });
        // Controller disposal initiates shutdown; retirement awaits the runtime
        // processes reported by that controller's environment.
        await retirement.WaitAsync(TimeSpan.FromSeconds(10));
        await RetireCompositionAsync();
        await Task.Run(() =>
        {
            GC.Collect(GC.MaxGeneration,GCCollectionMode.Aggressive,true,true);
            // Decommit freed LFH caches; this releases private commit rather
            // than paging still-live allocations out of the working set.
            var info=new HeapOptimization { Version=1 };
            HeapSetInformation(IntPtr.Zero,3,ref info,(nuint)Marshal.SizeOf<HeapOptimization>());
        });
    }
    private async Task RetireCompositionAsync()
    {
        if(!IsVisible||WindowState==WindowState.Minimized){await Dispatcher.Yield(DispatcherPriority.ContextIdle);return;}
        var completed=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        int frames=0;
        void Frame(object? sender,EventArgs args){if(++frames>=2)completed.TrySetResult();}
        CompositionTarget.Rendering+=Frame;
        try{InvalidateVisual();await completed.Task.WaitAsync(TimeSpan.FromSeconds(1));}
        catch(TimeoutException){await Dispatcher.Yield(DispatcherPriority.ContextIdle);}
        finally{CompositionTarget.Rendering-=Frame;}
    }
    [StructLayout(LayoutKind.Sequential)]
    private struct HeapOptimization { public uint Version,Flags; }
    [DllImport("kernel32.dll",SetLastError=true)]
    [return:MarshalAs(UnmanagedType.Bool)]
    private static extern bool HeapSetInformation(IntPtr heap,int kind,ref HeapOptimization information,nuint length);
    private void ApplyGraphicsMode(ExpanseGraphicsMode mode,bool persist=false)
    {
        graphicsMode=mode;shipPanel?.SetGraphicsMode(mode);
        if(persist&&previewPath is null)PanelPreferences.SaveGraphicsMode(mode);
        settings?.ApplyTheme();if(IsLoaded)SaveDisplayState();
    }
}

internal sealed partial class ShipPanel
{
    private ExpanseGraphicsMode graphicsMode=PanelPreferences.GraphicsMode;
    internal void SetGraphicsMode(ExpanseGraphicsMode mode)
    {graphicsMode=mode;shipViewport?.SetGraphicsMode(mode);projection?.SetGraphicsMode(mode);}
}
