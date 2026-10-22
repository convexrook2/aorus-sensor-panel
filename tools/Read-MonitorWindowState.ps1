# Read-only Win32 status, not a window-control helper. No SendInput/ShowWindow.
if(-not ('MonitorResourceWindowState' -as [type])) {
    Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
public static class MonitorResourceWindowState {
    [StructLayout(LayoutKind.Sequential)] public struct Rect {public int Left,Top,Right,Bottom;}
    [StructLayout(LayoutKind.Sequential)] public struct Point {public int X,Y; public Point(int x,int y){X=x;Y=y;}}
    [DllImport("user32.dll")] public static extern bool IsIconic(IntPtr window);
    [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr window);
    [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr window,out Rect rect);
    [DllImport("user32.dll")] public static extern IntPtr WindowFromPoint(Point point);
    [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr window,out uint process);
}
'@
}
function Read-MonitorWindowState([int]$MonitorProcessId) {
    $p=Get-Process -Id $MonitorProcessId -ErrorAction Stop
    $handle=$p.MainWindowHandle
    if($handle -eq [IntPtr]::Zero){throw 'The selected monitor has no native window.'}
    [uint32]$owner=0
    [void][MonitorResourceWindowState]::GetWindowThreadProcessId($handle,[ref]$owner)
    if($owner -ne $MonitorProcessId){throw 'Native window ownership no longer matches the monitor PID.'}
    $minimized=[MonitorResourceWindowState]::IsIconic($handle)
    $visible=[MonitorResourceWindowState]::IsWindowVisible($handle)
    $bounds=[MonitorResourceWindowState+Rect]::new()
    if(![MonitorResourceWindowState]::GetWindowRect($handle,[ref]$bounds)){throw 'Cannot read monitor window bounds.'}
    $covered=0;$checked=0;$coveringProcesses=[Collections.Generic.HashSet[uint32]]::new()
    if($visible -and !$minimized){
        # Read-only sample across the interior. Focus may belong to a different
        # monitor; actual screen coverage is the relevant measurement guard.
        foreach($row in 0..4){foreach($column in 0..8){
            $point=[MonitorResourceWindowState+Point]::new([int]($bounds.Left+20+($bounds.Right-$bounds.Left-40)*($column+.5)/9),[int]($bounds.Top+40+($bounds.Bottom-$bounds.Top-60)*($row+.5)/5))
            [uint32]$pointOwner=0
            [void][MonitorResourceWindowState]::GetWindowThreadProcessId([MonitorResourceWindowState]::WindowFromPoint($point),[ref]$pointOwner)
            $checked++;if($pointOwner -ne $MonitorProcessId){$covered++;[void]$coveringProcesses.Add($pointOwner)}
        }}
    }
    [pscustomobject]@{ProcessId=$MonitorProcessId;Handle=$handle.ToInt64();Minimized=$minimized;Visible=$visible;PresentationActive=$visible -and !$minimized;Foreground=[MonitorResourceWindowState]::GetForegroundWindow() -eq $handle;CoveragePoints=$checked;CoveredPoints=$covered;CoveringProcessIds=@($coveringProcesses);Bounds=$bounds;CapturedAt=[datetimeoffset]::Now.ToString('o')}
}
