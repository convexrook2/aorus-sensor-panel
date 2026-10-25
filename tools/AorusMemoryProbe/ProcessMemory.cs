using System.Runtime.InteropServices;

namespace AorusMemoryDiagnostics;

internal static class ProcessMemory
{
    internal readonly record struct Snapshot(ulong PrivateResident,ulong Resident,ulong PrivateCommit);
    internal static bool TryRead(out Snapshot snapshot)
        =>TryRead(new IntPtr(-1),out snapshot);
    internal static bool TryRead(IntPtr process,out Snapshot snapshot)
    {
        var counters=new Counters { Size=(uint)Marshal.SizeOf<Counters>() };
        bool supported=GetProcessMemoryInfo(process,ref counters,counters.Size)&&counters.PrivateWorkingSet>0;
        snapshot=new(counters.PrivateWorkingSet,counters.WorkingSet,counters.PrivateUsage);
        return supported;
    }
    internal sealed record Helper(int ProcessId,string Executable,ulong PrivateResident,ulong Resident,ulong PrivateCommit);
    internal static Helper? RefreshHelper(Helper helper)
    {
        IntPtr process=OpenProcess(0x410,false,(uint)helper.ProcessId);
        if(process==IntPtr.Zero)return null;
        try{return TryRead(process,out var sample)?new(helper.ProcessId,helper.Executable,sample.PrivateResident,sample.Resident,sample.PrivateCommit):null;}
        finally{CloseHandle(process);}
    }
    // Diagnostic only; enumerate processes, never the system-wide thread list.
    internal static Helper[] ReadHelpers(int rootProcessId)
    {
        var entries=new List<ProcessEntry>();
        IntPtr list=CreateToolhelp32Snapshot(2,0);
        if(list==new IntPtr(-1))throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
        try
        {
            var entry=new ProcessEntry { Size=(uint)Marshal.SizeOf<ProcessEntry>(),Executable="" };
            if(Process32First(list,ref entry))do{entries.Add(entry);}while(Process32Next(list,ref entry));
        }
        finally{CloseHandle(list);}
        var pids=new HashSet<uint>{(uint)rootProcessId};
        bool added;
        do{added=false;foreach(var entry in entries)if(pids.Contains(entry.ParentId))added|=pids.Add(entry.Id);}while(added);
        var result=new List<Helper>();
        foreach(var entry in entries.Where(e=>e.Id!=rootProcessId&&pids.Contains(e.Id)))
        {
            IntPtr process=OpenProcess(0x410,false,entry.Id);
            if(process==IntPtr.Zero)continue;
            try{if(TryRead(process,out var sample))result.Add(new((int)entry.Id,entry.Executable,sample.PrivateResident,sample.Resident,sample.PrivateCommit));}
            finally{CloseHandle(process);}
        }
        return result.ToArray();
    }
    [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)]
    private struct ProcessEntry
    {
        public uint Size,Usage,Id;public nuint DefaultHeap;public uint ModuleId,Threads,ParentId;public int Priority;public uint Flags;
        [MarshalAs(UnmanagedType.ByValTStr,SizeConst=260)]public string Executable;
    }
    [DllImport("kernel32.dll",SetLastError=true)]private static extern IntPtr CreateToolhelp32Snapshot(uint flags,uint pid);
    [DllImport("kernel32.dll",EntryPoint="Process32FirstW",CharSet=CharSet.Unicode)]
    [return:MarshalAs(UnmanagedType.Bool)]private static extern bool Process32First(IntPtr list,ref ProcessEntry entry);
    [DllImport("kernel32.dll",EntryPoint="Process32NextW",CharSet=CharSet.Unicode)]
    [return:MarshalAs(UnmanagedType.Bool)]private static extern bool Process32Next(IntPtr list,ref ProcessEntry entry);
    [DllImport("kernel32.dll",SetLastError=true)]private static extern IntPtr OpenProcess(uint access,[MarshalAs(UnmanagedType.Bool)]bool inherit,uint pid);
    [DllImport("kernel32.dll")][return:MarshalAs(UnmanagedType.Bool)]private static extern bool CloseHandle(IntPtr handle);
    [StructLayout(LayoutKind.Sequential)]
    private struct Counters
    {
        public uint Size,PageFaultCount;
        public ulong PeakWorkingSet,WorkingSet,PeakPagedPool,PagedPool,PeakNonPagedPool,NonPagedPool,
            Pagefile,PeakPagefile,PrivateUsage,PrivateWorkingSet,SharedCommit;
    }
    [DllImport("psapi.dll",SetLastError=true)]
    [return:MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetProcessMemoryInfo(IntPtr process,ref Counters counters,uint size);
}
