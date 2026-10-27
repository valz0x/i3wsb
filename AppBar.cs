using System.Runtime.InteropServices;

namespace I3wsb;

public enum BarEdge { Top, Bottom }

/// <summary>
/// Registers the overlay window as a Windows AppBar docked to top or bottom edge, so
/// the shell shrinks everyone else's work area — maximized / snapped windows
/// get pushed down instead of sliding under the bar (i3 strut behaviour).
///
/// The reservation MUST be released on exit or the work area stays shrunk until
/// explorer restarts.
/// </summary>
internal sealed class AppBar
{
    public const int CallbackMsg = 0x0400 + 3; // WM_APP + 3

    readonly IntPtr _hwnd;
    bool _registered;
    int _height;
    BarEdge _edge = BarEdge.Top;

    public AppBar(IntPtr hwnd) => _hwnd = hwnd;

    public BarEdge Edge
    {
        get => _edge;
        set
        {
            if (_edge != value)
            {
                _edge = value;
                Reposition();
            }
        }
    }

    public void EnsureRegistered(int height, BarEdge edge = BarEdge.Top)
    {
        _height = Math.Max(height, 1);
        _edge = edge;
        if (!_registered)
        {
            var d = New();
            d.uCallbackMessage = CallbackMsg;
            Native.SHAppBarMessage(Native.ABM_NEW, ref d);
            _registered = true;
        }
        Reposition();
    }

    public void Unregister()
    {
        if (!_registered) return;
        var d = New();
        Native.SHAppBarMessage(Native.ABM_REMOVE, ref d);
        _registered = false;
    }

    public void SetHeight(int height)
    {
        height = Math.Max(height, 1);
        if (!_registered || height == _height) return;
        _height = height;
        Reposition();
    }

    /// <summary>Returns true if the message was an AppBar notification.</summary>
    public bool HandleMessage(int msg, IntPtr wParam)
    {
        if (msg != CallbackMsg) return false;
        if (wParam.ToInt32() == Native.ABN_POSCHANGED) Reposition();
        return true;
    }

    public void OnWindowPosChanged()
    {
        if (!_registered) return;
        var d = New();
        Native.SHAppBarMessage(Native.ABM_WINDOWPOSCHANGED, ref d);
    }

    void Reposition()
    {
        if (!_registered) return;
        Native.RECT mon = MonitorRect();

        var d = New();
        d.uEdge = _edge == BarEdge.Bottom ? Native.ABE_BOTTOM : Native.ABE_TOP;

        if (_edge == BarEdge.Bottom)
        {
            d.rc = new Native.RECT { Left = mon.Left, Top = mon.Bottom - _height, Right = mon.Right, Bottom = mon.Bottom };
            Native.SHAppBarMessage(Native.ABM_QUERYPOS, ref d);
            d.rc.Top = d.rc.Bottom - _height;
        }
        else
        {
            d.rc = new Native.RECT { Left = mon.Left, Top = mon.Top, Right = mon.Right, Bottom = mon.Top + _height };
            Native.SHAppBarMessage(Native.ABM_QUERYPOS, ref d);
            d.rc.Bottom = d.rc.Top + _height;
        }

        Native.SHAppBarMessage(Native.ABM_SETPOS, ref d);
        Native.MoveWindow(_hwnd, d.rc.Left, d.rc.Top, d.rc.Right - d.rc.Left, d.rc.Bottom - d.rc.Top, true);
    }

    Native.APPBARDATA New() => new() { cbSize = Marshal.SizeOf<Native.APPBARDATA>(), hWnd = _hwnd };

    Native.RECT MonitorRect()
    {
        IntPtr m = Native.MonitorFromWindow(_hwnd, Native.MONITOR_DEFAULTTONEAREST);
        var mi = new Native.MONITORINFO { cbSize = Marshal.SizeOf<Native.MONITORINFO>() };
        Native.GetMonitorInfoW(m, ref mi);
        return mi.rcMonitor;
    }
}
