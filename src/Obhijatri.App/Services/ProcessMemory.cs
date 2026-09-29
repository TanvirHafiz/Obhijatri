using System.Runtime.InteropServices;

namespace Obhijatri.App.Services;

/// <summary>
/// Memory a process uses that no other process shares: the "private working set" that Task
/// Manager shows as Memory. Plain working set would count shared system libraries once for every
/// engine process, which overstates a browser with many of them.
/// </summary>
internal static class ProcessMemory
{
    private const uint QueryLimitedInformation = 0x1000;
    private const uint VmRead = 0x0010;

    [StructLayout(LayoutKind.Sequential)]
    private struct MemoryCountersEx2
    {
        public uint Cb;
        public uint PageFaultCount;
        public nuint PeakWorkingSetSize;
        public nuint WorkingSetSize;
        public nuint QuotaPeakPagedPoolUsage;
        public nuint QuotaPagedPoolUsage;
        public nuint QuotaPeakNonPagedPoolUsage;
        public nuint QuotaNonPagedPoolUsage;
        public nuint PagefileUsage;
        public nuint PeakPagefileUsage;
        public nuint PrivateUsage;
        public nuint PrivateWorkingSetSize;
        public ulong SharedCommitUsage;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern nint OpenProcess(uint access, [MarshalAs(UnmanagedType.Bool)] bool inherit, uint processId);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseHandle(nint handle);

    [DllImport("kernel32.dll", EntryPoint = "K32GetProcessMemoryInfo", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetProcessMemoryInfo(nint process, ref MemoryCountersEx2 counters, uint size);

    /// <summary>Private working set in bytes, or 0 when the process is gone or cannot be read.</summary>
    public static long PrivateBytes(int processId)
    {
        var handle = OpenProcess(QueryLimitedInformation | VmRead, false, (uint)processId);
        if (handle == 0)
        {
            return 0;
        }

        try
        {
            var counters = new MemoryCountersEx2 { Cb = (uint)Marshal.SizeOf<MemoryCountersEx2>() };
            if (!GetProcessMemoryInfo(handle, ref counters, counters.Cb))
            {
                return 0;
            }
            return (long)(counters.PrivateWorkingSetSize != 0 ? counters.PrivateWorkingSetSize : counters.WorkingSetSize);
        }
        finally
        {
            CloseHandle(handle);
        }
    }
}
