using System.Windows.Forms;

namespace I3wsb;

/// <summary>Invisible message sink. The keyboard hook posts action ids here so
/// the work runs on the UI thread, off the hook callback.</summary>
internal sealed class HotkeyWindow : NativeWindow, IDisposable
{
    const int WM_APP_ACTION = 0x8000 + 1; // WM_APP + 1

    public event Action<int>? Pressed;

    public HotkeyWindow() => CreateHandle(new CreateParams());

    public void Post(int id) => Native.PostMessageW(Handle, WM_APP_ACTION, (IntPtr)id, IntPtr.Zero);

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == WM_APP_ACTION)
            Pressed?.Invoke(m.WParam.ToInt32());
        base.WndProc(ref m);
    }

    public void Dispose()
    {
        if (Handle != IntPtr.Zero) DestroyHandle();
    }
}
