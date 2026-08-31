using System.Drawing;
using System.Windows.Forms;

namespace I3wsb;

internal static class Program
{
    static Vda _vda = null!;
    static DockForm _dock = null!;
    static Config _config = null!;
    static KeyboardHook _hook = null!;
    static readonly Dictionary<int, IntPtr> _lastActive = new();

    // action ids (produced by KeyboardHook.Translate)
    const int GoBase = 1;      // 1..4  -> desktop 0..3
    const int MoveBase = 11;   // 11..14
    const int ToggleDock = 20;
    const int ToggleTop = 21;
    const int ToggleAutoHide = 22;
    const int ToggleView = 23;
    const int DesktopSlots = 4;

    static ToolStripMenuItem _miTop = null!;
    static ToolStripMenuItem _miAutoHide = null!;
    static ToolStripMenuItem _miViewIcons = null!;
    static ToolStripMenuItem _miViewBar = null!;
    static ToolStripMenuItem _miAnchor = null!;
    static ToolStripMenuItem _miSuperWin = null!;
    static ToolStripMenuItem _miSuperAlt = null!;

    [STAThread]
    static void Main()
    {
        ApplicationConfiguration.Initialize();

        _config = Config.Load();
        _vda = new Vda();
        _dock = new DockForm(_vda)
        {
            AutoHideOnFullscreen = _config.AutoHide,
            AnchorTop = _config.AnchorTop,
            View = _config.View,
        };
        _dock.SetAlwaysOnTop(_config.AlwaysOnTop);
        _dock.DesktopClicked += GoToDesktop;

        using var sink = new HotkeyWindow();
        sink.Pressed += OnHotkey;
        _hook = new KeyboardHook(sink, _config.Super);

        using var tray = new NotifyIcon
        {
            Icon = SystemIcons.Application,
            Visible = true,
            Text = _vda.Loaded ? "i3wsb" : "i3wsb (VirtualDesktopAccessor.dll not loaded)",
        };
        tray.ContextMenuStrip = BuildMenu();
        tray.DoubleClick += (_, _) => _dock.ToggleUserVisible();

        var refresh = new System.Windows.Forms.Timer { Interval = 1200 };
        refresh.Tick += (_, _) => _dock.RefreshNow();
        refresh.Start();

        var guard = new System.Windows.Forms.Timer { Interval = 800 };
        guard.Tick += (_, _) => _dock.FullscreenGuardTick();
        guard.Start();

        if (_vda.Loaded)
        {
            _dock.Show();
            _dock.RefreshNow(true);
        }
        else
        {
            MessageBox.Show(
                "VirtualDesktopAccessor.dll could not be loaded. It must sit next to i3wsb.exe " +
                "and match your Windows 11 build. Desktop switching and the bar are disabled.",
                "i3wsb", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

        Application.ApplicationExit += (_, _) => { tray.Visible = false; _hook.Dispose(); _dock.ReleaseAppBar(); };
        Application.Run();
    }

    static ContextMenuStrip BuildMenu()
    {
        var menu = new ContextMenuStrip();
        menu.Items.Add("Show / hide\tCtrl+Alt+D", null, (_, _) => _dock.ToggleUserVisible());

        var view = new ToolStripMenuItem("View");
        _miViewIcons = new ToolStripMenuItem("Icons (mac dock)", null, (_, _) => SetView(DockView.Icons))
        { Checked = _config.View == DockView.Icons };
        _miViewBar = new ToolStripMenuItem("Workspace bar (i3)\tCtrl+Alt+S", null, (_, _) => SetView(DockView.Bar))
        { Checked = _config.View == DockView.Bar };
        view.DropDownItems.Add(_miViewIcons);
        view.DropDownItems.Add(_miViewBar);
        menu.Items.Add(view);

        _miAnchor = new ToolStripMenuItem("Anchor to top edge", null, (_, _) => ToggleAnchor())
        { Checked = _config.AnchorTop };
        menu.Items.Add(_miAnchor);

        var super = new ToolStripMenuItem("Super key");
        _miSuperWin = new ToolStripMenuItem("Win", null, (_, _) => SetSuper(SuperKey.Win))
        { Checked = _config.Super == SuperKey.Win };
        _miSuperAlt = new ToolStripMenuItem("Alt", null, (_, _) => SetSuper(SuperKey.Alt))
        { Checked = _config.Super == SuperKey.Alt };
        super.DropDownItems.Add(_miSuperWin);
        super.DropDownItems.Add(_miSuperAlt);
        menu.Items.Add(super);

        _miTop = new ToolStripMenuItem("Always-on-top\tCtrl+Alt+T", null, (_, _) => ToggleAlwaysOnTop())
        { Checked = _config.AlwaysOnTop };
        menu.Items.Add(_miTop);

        _miAutoHide = new ToolStripMenuItem("Auto-hide over fullscreen apps\tCtrl+Alt+F", null, (_, _) => ToggleFsAutoHide())
        { Checked = _config.AutoHide };
        menu.Items.Add(_miAutoHide);

        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Exit", null, (_, _) => Application.Exit());
        return menu;
    }

    static void OnHotkey(int id)
    {
        if (id is >= GoBase and < GoBase + DesktopSlots) { GoToDesktop(id - GoBase); return; }
        if (id is >= MoveBase and < MoveBase + DesktopSlots) { MoveActiveWindow(id - MoveBase); return; }
        switch (id)
        {
            case ToggleDock: _dock.ToggleUserVisible(); break;
            case ToggleTop: ToggleAlwaysOnTop(); break;
            case ToggleAutoHide: ToggleFsAutoHide(); break;
            case ToggleView: SetView(_dock.View == DockView.Icons ? DockView.Bar : DockView.Icons); break;
        }
    }

    static void SetView(DockView v)
    {
        _dock.View = v;
        _miViewIcons.Checked = v == DockView.Icons;
        _miViewBar.Checked = v == DockView.Bar;
        _config.View = v;
        _config.Save();
    }

    static void ToggleAnchor()
    {
        _miAnchor.Checked = !_miAnchor.Checked;
        _dock.AnchorTop = _miAnchor.Checked;
        _config.AnchorTop = _miAnchor.Checked;
        _config.Save();
    }

    static void SetSuper(SuperKey s)
    {
        _hook.Super = s;
        _miSuperWin.Checked = s == SuperKey.Win;
        _miSuperAlt.Checked = s == SuperKey.Alt;
        _config.Super = s;
        _config.Save();
    }

    static void ToggleAlwaysOnTop()
    {
        _miTop.Checked = !_miTop.Checked;
        _dock.SetAlwaysOnTop(_miTop.Checked);
        _config.AlwaysOnTop = _miTop.Checked;
        _config.Save();
    }

    static void ToggleFsAutoHide()
    {
        _miAutoHide.Checked = !_miAutoHide.Checked;
        _dock.AutoHideOnFullscreen = _miAutoHide.Checked;
        _config.AutoHide = _miAutoHide.Checked;
        _config.Save();
    }

    static void GoToDesktop(int index)
    {
        if (!_vda.Loaded) return;
        int current = _vda.CurrentDesktop;
        if (current == index) { _dock.RefreshNow(true); return; }

        IntPtr fg = Native.GetForegroundWindow();
        if (fg != IntPtr.Zero && _vda.IsOnCurrentDesktop(fg))
            _lastActive[current] = fg;

        _vda.GoTo(index);
        System.Threading.Thread.Sleep(60);

        if (_lastActive.TryGetValue(index, out IntPtr restore) && Native.IsWindow(restore))
            Native.SetForegroundWindow(restore);

        _dock.RefreshNow(true);
    }

    static void MoveActiveWindow(int index)
    {
        if (!_vda.Loaded) return;
        IntPtr fg = Native.GetForegroundWindow();
        if (fg == IntPtr.Zero || fg == _dock.Handle) return;

        _vda.MoveWindow(fg, index);
        _lastActive[index] = fg;
        _vda.GoTo(index);
        System.Threading.Thread.Sleep(60);
        if (Native.IsWindow(fg)) Native.SetForegroundWindow(fg);

        _dock.RefreshNow(true);
    }
}
