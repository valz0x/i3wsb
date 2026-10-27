using System.Runtime.InteropServices;
using ComTypes = System.Runtime.InteropServices.ComTypes;

namespace I3wsb;

internal static class SystemMetrics
{
    static ulong _prevIdleTime;
    static ulong _prevKernelTime;
    static ulong _prevUserTime;
    static int _lastCpuPercent;
    static DateTime _lastCpuSample = DateTime.MinValue;

    public static int GetCpuUsagePercent()
    {
        // Cache for at least 400ms to avoid sampling noise and unnecessary calculations
        var now = DateTime.UtcNow;
        if ((now - _lastCpuSample).TotalMilliseconds < 400)
            return _lastCpuPercent;

        if (!GetSystemTimes(out var idle, out var kernel, out var user))
            return _lastCpuPercent;

        ulong idleTime = ToUInt64(idle);
        ulong kernelTime = ToUInt64(kernel);
        ulong userTime = ToUInt64(user);

        if (_prevIdleTime != 0)
        {
            ulong diffIdle = idleTime - _prevIdleTime;
            ulong diffKernel = kernelTime - _prevKernelTime;
            ulong diffUser = userTime - _prevUserTime;

            // kernelTime includes idleTime
            ulong total = diffKernel + diffUser;
            if (total > 0 && total >= diffIdle)
            {
                ulong busy = total - diffIdle;
                _lastCpuPercent = (int)Math.Clamp((busy * 100UL) / total, 0, 100);
            }
        }

        _prevIdleTime = idleTime;
        _prevKernelTime = kernelTime;
        _prevUserTime = userTime;
        _lastCpuSample = now;
        return _lastCpuPercent;
    }

    public static (int percent, string summary) GetMemoryUsage()
    {
        var mem = new MEMORYSTATUSEX { dwLength = (uint)Marshal.SizeOf<MEMORYSTATUSEX>() };
        if (GlobalMemoryStatusEx(ref mem))
        {
            double usedGb = (mem.ullTotalPhys - mem.ullAvailPhys) / (1024.0 * 1024 * 1024);
            double totalGb = mem.ullTotalPhys / (1024.0 * 1024 * 1024);
            int percent = (int)mem.dwMemoryLoad;
            return (percent, $"{usedGb:F1} / {totalGb:F1} GB ({percent}%)");
        }
        return (0, "N/A");
    }

    public static (bool hasBattery, int percent, bool isCharging) GetBatteryStatus()
    {
        if (GetSystemPowerStatus(out var status))
        {
            // 128 = No system battery, 255 = Unknown
            bool noBattery = (status.BatteryFlag & 128) != 0 || status.BatteryLifePercent == 255;
            if (noBattery) return (false, 0, false);

            int percent = Math.Clamp((int)status.BatteryLifePercent, 0, 100);
            bool isCharging = (status.BatteryFlag & 8) != 0 || status.ACLineStatus == 1;
            return (true, percent, isCharging);
        }
        return (false, 0, false);
    }

    static ulong ToUInt64(ComTypes.FILETIME ft) => ((ulong)ft.dwHighDateTime << 32) | (uint)ft.dwLowDateTime;

    [DllImport("kernel32.dll", SetLastError = true)]
    static extern bool GetSystemTimes(out ComTypes.FILETIME lpIdleTime, out ComTypes.FILETIME lpKernelTime, out ComTypes.FILETIME lpUserTime);

    [StructLayout(LayoutKind.Sequential)]
    struct MEMORYSTATUSEX
    {
        public uint dwLength;
        public uint dwMemoryLoad;
        public ulong ullTotalPhys;
        public ulong ullAvailPhys;
        public ulong ullTotalPageFile;
        public ulong ullAvailPageFile;
        public ulong ullTotalVirtual;
        public ulong ullAvailVirtual;
        public ulong ullAvailExtendedVirtual;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX lpBuffer);

    [StructLayout(LayoutKind.Sequential)]
    struct SYSTEM_POWER_STATUS
    {
        public byte ACLineStatus;
        public byte BatteryFlag;
        public byte BatteryLifePercent;
        public byte SystemStatusFlag;
        public int BatteryLifeTime;
        public int BatteryFullLifeTime;
    }

    [DllImport("kernel32.dll")]
    static extern bool GetSystemPowerStatus(out SYSTEM_POWER_STATUS lpSystemPowerStatus);
}
