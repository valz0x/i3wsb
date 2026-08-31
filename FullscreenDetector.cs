namespace I3wsb;

internal static class FullscreenDetector
{
    static readonly HashSet<string> ShellClasses = new(StringComparer.Ordinal)
    {
        "Progman", "WorkerW", "Shell_TrayWnd", "Shell_SecondaryTrayWnd",
        "Windows.UI.Core.CoreWindow", "XamlExplorerHostIslandWindow",
        "ForegroundStaging", "MultitaskingViewFrame",
    };

    /// <summary>True when the foreground window fills a whole monitor and isn't the shell.</summary>
    public static bool ForegroundIsFullscreen(IntPtr dockHwnd)
    {
        IntPtr fg = Native.GetForegroundWindow();
        if (fg == IntPtr.Zero || fg == dockHwnd) return false;

        string cls = Native.GetClass(fg);
        if (ShellClasses.Contains(cls)) return false;

        if (!Native.GetWindowRect(fg, out var w)) return false;

        IntPtr mon = Native.MonitorFromWindow(fg, Native.MONITOR_DEFAULTTONEAREST);
        var mi = new Native.MONITORINFO { cbSize = System.Runtime.InteropServices.Marshal.SizeOf<Native.MONITORINFO>() };
        if (!Native.GetMonitorInfoW(mon, ref mi)) return false;
        var m = mi.rcMonitor;

        return w.Left <= m.Left + 1 && w.Top <= m.Top + 1 &&
               w.Right >= m.Right - 1 && w.Bottom >= m.Bottom - 1;
    }
}
