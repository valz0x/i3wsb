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
        while (count > 0 && count <= index) { _createDesktop(); count++; }
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
}
