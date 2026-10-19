# Read-only Win32 status, not a window-control helper. No SendInput/ShowWindow.
if(-not ('MonitorResourceWindowState' -as [type])) {
    Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
public static class MonitorResourceWindowState {
    [DllImport("user32.dll")] public static extern bool IsIconic(IntPtr window);
    [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr window);
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
    [pscustomobject]@{ProcessId=$MonitorProcessId;Handle=$handle.ToInt64();Minimized=$minimized;Visible=$visible;PresentationActive=$visible -and !$minimized;CapturedAt=[datetimeoffset]::Now.ToString('o')}
}
