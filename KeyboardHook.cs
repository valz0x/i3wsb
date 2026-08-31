using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace I3wsb;

/// <summary>
/// Low-level keyboard hook. Global hotkeys are done this way rather than with
/// <c>RegisterHotKey</c> because <c>Win+1..9</c> are reserved by the shell and
/// <c>RegisterHotKey</c> silently fails for them. The hook sees the keystroke
/// first, so it can both act on it and swallow it.
///
/// Translation is intentionally trivial and returns fast; the actual desktop
/// switch is posted to <see cref="HotkeyWindow"/> and runs off the hook.
/// </summary>
internal sealed class KeyboardHook : IDisposable
{
    const int WH_KEYBOARD_LL = 13;
    const int WM_KEYDOWN = 0x0100;
    const int WM_SYSKEYDOWN = 0x0104;

    const int VK_SHIFT = 0x10, VK_CONTROL = 0x11, VK_MENU = 0x12;
    const int VK_LWIN = 0x5B, VK_RWIN = 0x5C;

    [StructLayout(LayoutKind.Sequential)]
    struct KBDLLHOOKSTRUCT { public uint vkCode; public uint scanCode; public uint flags; public uint time; public IntPtr extra; }

    delegate IntPtr HookProc(int code, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true)]
    static extern IntPtr SetWindowsHookExW(int idHook, HookProc fn, IntPtr hmod, uint threadId);
    [DllImport("user32.dll")]
    static extern bool UnhookWindowsHookEx(IntPtr hook);
    [DllImport("user32.dll")]
    static extern IntPtr CallNextHookEx(IntPtr hook, int code, IntPtr wParam, IntPtr lParam);
    [DllImport("user32.dll")]
    static extern short GetAsyncKeyState(int vk);
    [DllImport("user32.dll")]
    static extern void keybd_event(byte vk, byte scan, uint flags, IntPtr extra);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    static extern IntPtr GetModuleHandleW(string? name);

    readonly HookProc _proc;   // kept alive for the lifetime of the hook
    IntPtr _hook;

    readonly HotkeyWindow _sink;
    public SuperKey Super { get; set; }

    // action ids (match Program's OnHotkey)
    const int GoBase = 1, MoveBase = 11, ToggleDock = 20, ToggleTop = 21, ToggleAutoHide = 22, ToggleView = 23, Slots = 4;

    public KeyboardHook(HotkeyWindow sink, SuperKey super)
    {
        _sink = sink;
        Super = super;
        _proc = Callback;
        _hook = SetWindowsHookExW(WH_KEYBOARD_LL, _proc, GetModuleHandleW(null), 0);
    }

    IntPtr Callback(int code, IntPtr wParam, IntPtr lParam)
    {
        if (code >= 0)
        {
            int msg = wParam.ToInt32();
            if (msg == WM_KEYDOWN || msg == WM_SYSKEYDOWN)
            {
                var kb = Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(lParam);
                int id = Translate((int)kb.vkCode, out bool superUsed);
                if (id != 0)
                {
                    _sink.Post(id);
                    if (superUsed && Super == SuperKey.Win) NeutralizeWinKey();
                    return (IntPtr)1;   // swallow
                }
            }
        }
        return CallNextHookEx(_hook, code, wParam, lParam);
    }

    static bool Down(int vk) => (GetAsyncKeyState(vk) & 0x8000) != 0;

    int Translate(int vk, out bool superUsed)
    {
        superUsed = false;
        bool shift = Down(VK_SHIFT);
        bool ctrl = Down(VK_CONTROL);
        bool alt = Down(VK_MENU);
        bool win = Down(VK_LWIN) || Down(VK_RWIN);
        bool super = Super == SuperKey.Alt ? alt : win;

        int digit = vk is >= 0x31 and <= 0x34 ? vk - 0x31
                  : vk is >= 0x61 and <= 0x64 ? vk - 0x61   // numpad 1..4
                  : -1;

        if (super && digit >= 0 && !ctrl)
        {
            superUsed = true;
            return (shift ? MoveBase : GoBase) + digit;
        }

        if (ctrl && alt && !shift && !win)
        {
            return vk switch
            {
                0x44 => ToggleDock,      // D
                0x53 => ToggleView,      // S
                0x54 => ToggleTop,       // T
                0x46 => ToggleAutoHide,  // F
                _ => 0,
            };
        }
        return 0;
    }

    // After swallowing Win+<key>, tap Ctrl so Windows doesn't treat the Win
    // release as a lone press and open the Start menu.
    static void NeutralizeWinKey()
    {
        const byte VK_CONTROL_B = 0x11;
        const uint KEYEVENTF_KEYUP = 0x0002;
        keybd_event(VK_CONTROL_B, 0, 0, IntPtr.Zero);
        keybd_event(VK_CONTROL_B, 0, KEYEVENTF_KEYUP, IntPtr.Zero);
    }

    public void Dispose()
    {
        if (_hook != IntPtr.Zero) { UnhookWindowsHookEx(_hook); _hook = IntPtr.Zero; }
    }
}
