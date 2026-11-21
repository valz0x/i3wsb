using System.Drawing;

namespace I3wsb;

internal readonly record struct AppWindow(IntPtr Hwnd, string Title, string ExePath);

/// <summary>Enumerates "real" top-level windows, the same set the taskbar shows.</summary>
internal static class TaskbarWindows
{
    static readonly Dictionary<uint, string> _procPathCache = new();
    static DateTime _lastCacheCleanup = DateTime.UtcNow;

    public static string GetCachedProcessPath(IntPtr hwnd)
    {
        Native.GetWindowThreadProcessId(hwnd, out uint pid);
        if (pid == 0) return "";

        lock (_procPathCache)
        {
            if (_procPathCache.TryGetValue(pid, out string? path))
                return path;

            path = Native.GetProcessPath(hwnd);
            _procPathCache[pid] = path;

            if (_procPathCache.Count > 128 && (DateTime.UtcNow - _lastCacheCleanup).TotalSeconds > 60)
            {
                _lastCacheCleanup = DateTime.UtcNow;
                _procPathCache.Clear();
            }

            return path;
        }
    }

    public static List<AppWindow> Enumerate(bool includeOtherDesktops = true)
    {
        var result = new List<AppWindow>();

        Native.EnumWindows((hwnd, _) =>
        {
            // 1. Fast filters first (no allocations):
            if (!Native.IsWindowVisible(hwnd)) return true;

            long ex = Native.GetExStyle(hwnd);
            bool appWindow = (ex & Native.WS_EX_APPWINDOW) != 0;
            bool toolWindow = (ex & Native.WS_EX_TOOLWINDOW) != 0;
            if (toolWindow && !appWindow) return true;

            IntPtr owner = Native.GetWindow(hwnd, Native.GW_OWNER);
            if (owner != IntPtr.Zero && !appWindow) return true;

            int cloaked = Native.GetCloakedState(hwnd);
            if (cloaked != 0)
            {
                // DWM_CLOAKED_SHELL (2) means the window is on another virtual desktop.
                if (!includeOtherDesktops || cloaked != Native.DWM_CLOAKED_SHELL)
                    return true;
            }

            // 2. Class filter
            string cls = Native.GetClass(hwnd);
            if (cls is "Progman" or "WorkerW" or "Shell_TrayWnd" or
                       "Shell_SecondaryTrayWnd" or "Button" or "TopLevelWindowForOverflowXamlIsland")
                return true;

            // 3. Title filter (only queried for real top-level app windows)
            string title = Native.GetTitle(hwnd);
            if (title.Length == 0) return true;

            // 4. Cached process path (avoids repeated OpenProcess/QueryImage calls)
            result.Add(new AppWindow(hwnd, title, GetCachedProcessPath(hwnd)));
            return true;
        }, IntPtr.Zero);

        return result;
    }

    // ---- icon cache -------------------------------------------------------

    static readonly Dictionary<string, Bitmap?> _iconCache = new(StringComparer.OrdinalIgnoreCase);

    public static Bitmap? IconFor(string exePath, int size)
    {
        if (string.IsNullOrEmpty(exePath)) return null;
        string key = exePath + "|" + size;
        if (_iconCache.TryGetValue(key, out var cached)) return cached;

        Bitmap? bmp = null;
        try
        {
            var info = new Native.SHFILEINFO();
            uint flags = Native.SHGFI_ICON | (size <= 24 ? Native.SHGFI_SMALLICON : Native.SHGFI_LARGEICON);
            IntPtr r = Native.SHGetFileInfoW(exePath, 0, ref info, (uint)System.Runtime.InteropServices.Marshal.SizeOf<Native.SHFILEINFO>(), flags);
            if (r != IntPtr.Zero && info.hIcon != IntPtr.Zero)
            {
                using var ico = Icon.FromHandle(info.hIcon);
                using var full = ico.ToBitmap();
                bmp = new Bitmap(full, new Size(size, size));
                Native.DestroyIcon(info.hIcon);
            }
        }
        catch { bmp = null; }

        _iconCache[key] = bmp;
        return bmp;
    }
}
