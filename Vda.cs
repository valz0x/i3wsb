using System.Runtime.InteropServices;

namespace I3wsb;

/// <summary>
/// Dynamic binding to Ciantic's VirtualDesktopAccessor.dll. Every export is
/// resolved individually so an older DLL that is missing some functions still
/// gives us the ones it has (switching + per-window desktop queries) instead
/// of failing to start.
/// </summary>
internal sealed class Vda
{
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate int IntFn();
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate int IntFromIntFn(int a);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate int IntFromPtrFn(IntPtr h);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate void VoidFromIntFn(int a);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate void VoidFromPtrIntFn(IntPtr h, int a);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate int IntFromIntIntFn(int a, int b);

    readonly IntFn? _getCurrent;
    readonly IntFn? _getCount;
    readonly VoidFromIntFn? _goto;
    readonly VoidFromPtrIntFn? _moveWindow;
    readonly IntFromPtrFn? _windowDesktop;
    readonly IntFromPtrFn? _isOnCurrent;
    readonly IntFn? _createDesktop;
    readonly IntFromIntIntFn? _removeDesktop;

    public bool Loaded { get; }
    public bool CanCreate => _createDesktop != null;
    public bool CanRemove => _removeDesktop != null;

    public Vda()
    {
        string dll = Path.Combine(AppContext.BaseDirectory, "VirtualDesktopAccessor.dll");
        if (!File.Exists(dll) || !NativeLibrary.TryLoad(dll, out IntPtr lib))
        {
            Loaded = false;
            return;
        }

        T? Bind<T>(string name) where T : Delegate =>
            NativeLibrary.TryGetExport(lib, name, out IntPtr p)
                ? Marshal.GetDelegateForFunctionPointer<T>(p) : null;

        _getCurrent    = Bind<IntFn>("GetCurrentDesktopNumber");
        _getCount      = Bind<IntFn>("GetDesktopCount");
        _goto          = Bind<VoidFromIntFn>("GoToDesktopNumber");
        _moveWindow    = Bind<VoidFromPtrIntFn>("MoveWindowToDesktopNumber");
        _windowDesktop = Bind<IntFromPtrFn>("GetWindowDesktopNumber");
        _isOnCurrent   = Bind<IntFromPtrFn>("IsWindowOnCurrentVirtualDesktop");
        _createDesktop = Bind<IntFn>("CreateDesktop");
        _removeDesktop = Bind<IntFromIntIntFn>("RemoveDesktop");

        Loaded = _getCurrent != null && _goto != null;
    }

    public int CurrentDesktop => _getCurrent?.Invoke() ?? 0;

    public int DesktopCount => _getCount?.Invoke() ?? -1;

    public void EnsureDesktop(int index)
    {
        if (_createDesktop == null || _getCount == null) return;
        int count = _getCount();
        int attempts = 0;
        while (count > 0 && count <= index && attempts < 10)
        {
            _createDesktop();
            attempts++;
            System.Threading.Thread.Sleep(20);
            int newCount = _getCount();
            if (newCount > count) count = newCount;
            else count++;
        }
    }

    public void GoTo(int index)
    {
        EnsureDesktop(index);
        _goto?.Invoke(index);
    }

    public void MoveWindow(IntPtr hWnd, int index)
    {
        EnsureDesktop(index);
        _moveWindow?.Invoke(hWnd, index);
    }

    public bool IsOnCurrentDesktop(IntPtr hWnd)
    {
        if (_isOnCurrent != null) return _isOnCurrent(hWnd) != 0;
        if (_windowDesktop != null) return _windowDesktop(hWnd) == CurrentDesktop;
        return true;
    }

    /// <summary>0-based desktop a window lives on, or -1 if unknown / pinned.</summary>
    public int WindowDesktop(IntPtr hWnd) => _windowDesktop?.Invoke(hWnd) ?? -1;

    public bool RemoveDesktop(int index, int fallback = 0)
    {
        if (_removeDesktop == null) return false;
        return _removeDesktop(index, fallback) != 0;
    }

    /// <summary>
    /// Removes trailing empty virtual desktops beyond desktop 0 that are not currently active.
    /// </summary>
    public void PruneTrailingEmptyDesktops(HashSet<int> occupied)
    {
        if (_removeDesktop == null || _getCount == null) return;
        int count = _getCount();
        int current = CurrentDesktop;

        for (int d = count - 1; d > 0; d--)
        {
            if (d == current) break;
            if (!occupied.Contains(d))
            {
                _removeDesktop(d, Math.Min(current, d - 1));
            }
            else
            {
                break;
            }
        }
    }
}
