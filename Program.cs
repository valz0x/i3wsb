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

    const int GoBase = KeyboardHook.GoBase;          // 1..9  -> desktop 0..8
    const int MoveBase = KeyboardHook.MoveBase;      // 11..19
    const int ToggleDock = KeyboardHook.ToggleDock;
    const int ToggleTop = KeyboardHook.ToggleTop;
    const int ToggleAutoHide = KeyboardHook.ToggleAutoHide;
    const int ToggleView = KeyboardHook.ToggleView;
    const int DesktopSlots = KeyboardHook.DesktopSlots;

    static ToolStripMenuItem _miTop = null!;
    static ToolStripMenuItem _miAutoHide = null!;
    static ToolStripMenuItem _miViewIcons = null!;
    static ToolStripMenuItem _miViewBar = null!;
    static ToolStripMenuItem _miEdgeTop = null!;
    static ToolStripMenuItem _miEdgeBottom = null!;
    static ToolStripMenuItem _miEdgeFloat = null!;
    static ToolStripMenuItem _miSuperWin = null!;
    static ToolStripMenuItem _miSuperAlt = null!;

    [STAThread]
    static void Main()
    {
        using var mutex = new System.Threading.Mutex(true, "Local\\i3wsb_single_instance_mutex", out bool createdNew);
        if (!createdNew) return;

        ApplicationConfiguration.Initialize();

        _config = Config.Load();
        _vda = new Vda();
        _dock = new DockForm(_vda)
        {
            AutoHideOnFullscreen = _config.AutoHide,
            AnchorEdge = _config.AnchorEdge,
            Edge = _config.Edge,
            View = _config.View,
            Theme = Theme.FromName(_config.Theme),
            ShowCpu = _config.ShowCpu,
            ShowRam = _config.ShowRam,
            ShowVolume = _config.ShowVolume,
            ShowBattery = _config.ShowBattery,
            ShowOpenApps = _config.ShowOpenApps,
            ShowClock = _config.ShowClock,
            AutoPrune = _config.AutoPrune,
        };
        _dock.SetAlwaysOnTop(_config.AlwaysOnTop);
        _dock.DesktopClicked += GoToDesktop;

        if (_config.DisableTaskbarFlashing)
            Native.SetTaskbarFlashing(false);

        Native.SetDisabledHotkeys(true);

        using var sink = new HotkeyWindow();
        sink.Pressed += OnHotkey;
        _hook = new KeyboardHook(sink, _config.Super);

        Icon appIcon = TrayIconGenerator.CreateAppIcon();
        using var tray = new NotifyIcon
        {
            Icon = appIcon,
            Visible = true,
            Text = _vda.Loaded ? "i3wsb — Workspace Bar" : "i3wsb (VirtualDesktopAccessor.dll not loaded)",
        };
        var menu = BuildMenu();
        tray.ContextMenuStrip = menu;
        _dock.ContextMenuStrip = menu;
        tray.DoubleClick += (_, _) => _dock.ToggleUserVisible();

        var refresh = new System.Windows.Forms.Timer { Interval = 1000 };
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

        Application.ApplicationExit += (_, _) =>
        {
            tray.Visible = false;
            appIcon.Dispose();
            _hook.Dispose();
            _dock.ReleaseAppBar();
        };

        Application.Run();
    }

    static ContextMenuStrip BuildMenu()
    {
        var menu = new ContextMenuStrip();
        menu.Items.Add("Show / hide\tCtrl+Alt+D", null, (_, _) => _dock.ToggleUserVisible());

        // View menu
        var view = new ToolStripMenuItem("View");
        _miViewBar = new ToolStripMenuItem("Workspace bar (i3)\tCtrl+Alt+S", null, (_, _) => SetView(DockView.Bar))
        { Checked = _config.View == DockView.Bar };
        _miViewIcons = new ToolStripMenuItem("Icons (mac dock)", null, (_, _) => SetView(DockView.Icons))
        { Checked = _config.View == DockView.Icons };
        view.DropDownItems.Add(_miViewBar);
        view.DropDownItems.Add(_miViewIcons);
        menu.Items.Add(view);

        // Position / Edge menu
        var pos = new ToolStripMenuItem("Position");
        _miEdgeTop = new ToolStripMenuItem("Top edge (docked)", null, (_, _) => SetPosition(true, BarEdge.Top))
        { Checked = _config.AnchorEdge && _config.Edge == BarEdge.Top };
        _miEdgeBottom = new ToolStripMenuItem("Bottom edge (docked)", null, (_, _) => SetPosition(true, BarEdge.Bottom))
        { Checked = _config.AnchorEdge && _config.Edge == BarEdge.Bottom };
        _miEdgeFloat = new ToolStripMenuItem("Floating window", null, (_, _) => SetPosition(false, BarEdge.Top))
        { Checked = !_config.AnchorEdge };
        pos.DropDownItems.Add(_miEdgeTop);
        pos.DropDownItems.Add(_miEdgeBottom);
        pos.DropDownItems.Add(_miEdgeFloat);
        menu.Items.Add(pos);

        // Themes
        var themes = new ToolStripMenuItem("Theme");
        AddThemeItem(themes, "Modern Dark", "dark");
        AddThemeItem(themes, "Catppuccin Mocha", "catppuccin");
        AddThemeItem(themes, "Nord", "nord");
        AddThemeItem(themes, "Tokyo Night", "tokyonight");
        menu.Items.Add(themes);

        // Widgets
        var widgets = new ToolStripMenuItem("Widgets");
        AddWidgetToggle(widgets, "CPU Monitor", () => _dock.ShowCpu, v => { _dock.ShowCpu = _config.ShowCpu = v; });
        AddWidgetToggle(widgets, "RAM Monitor", () => _dock.ShowRam, v => { _dock.ShowRam = _config.ShowRam = v; });
        AddWidgetToggle(widgets, "Volume Control", () => _dock.ShowVolume, v => { _dock.ShowVolume = _config.ShowVolume = v; });
        AddWidgetToggle(widgets, "Battery Indicator", () => _dock.ShowBattery, v => { _dock.ShowBattery = _config.ShowBattery = v; });
        AddWidgetToggle(widgets, "Open Apps in Center", () => _dock.ShowOpenApps, v => { _dock.ShowOpenApps = _config.ShowOpenApps = v; });
        AddWidgetToggle(widgets, "Date & Clock", () => _dock.ShowClock, v => { _dock.ShowClock = _config.ShowClock = v; });
        menu.Items.Add(widgets);

        // Super key menu
        var super = new ToolStripMenuItem("Super key");
        _miSuperWin = new ToolStripMenuItem("Win (Win+1..9)", null, (_, _) => SetSuper(SuperKey.Win))
        { Checked = _config.Super == SuperKey.Win };
        _miSuperAlt = new ToolStripMenuItem("Alt (Alt+1..9)", null, (_, _) => SetSuper(SuperKey.Alt))
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

        var miAutoPrune = new ToolStripMenuItem("Auto-remove empty desktops", null, (s, _) =>
        {
            if (s is ToolStripMenuItem mi)
            {
                mi.Checked = !mi.Checked;
                _config.AutoPrune = mi.Checked;
                _dock.AutoPrune = mi.Checked;
                _config.Save();
                _dock.RefreshNow(true);
            }
        })
        { Checked = _config.AutoPrune };
        menu.Items.Add(miAutoPrune);

        var miFlashing = new ToolStripMenuItem("Disable taskbar flashing", null, (s, _) =>
        {
            if (s is ToolStripMenuItem mi)
            {
                mi.Checked = !mi.Checked;
                _config.DisableTaskbarFlashing = mi.Checked;
                Native.SetTaskbarFlashing(!mi.Checked);
                _config.Save();
            }
        })
        { Checked = _config.DisableTaskbarFlashing };
        menu.Items.Add(miFlashing);

        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Exit", null, (_, _) => Application.Exit());
        return menu;
    }

    static void AddThemeItem(ToolStripMenuItem parent, string title, string themeName)
    {
        var item = new ToolStripMenuItem(title, null, (_, _) =>
        {
            _config.Theme = themeName;
            _dock.Theme = Theme.FromName(themeName);
            _config.Save();
            foreach (ToolStripMenuItem mi in parent.DropDownItems) mi.Checked = false;
            if (parent.DropDownItems.Cast<ToolStripMenuItem>().FirstOrDefault(m => (string?)m.Tag == themeName) is { } curr)
                curr.Checked = true;
            _dock.Invalidate();
        })
        {
            Tag = themeName,
            Checked = string.Equals(_config.Theme, themeName, StringComparison.OrdinalIgnoreCase)
        };
        parent.DropDownItems.Add(item);
    }

    static void AddWidgetToggle(ToolStripMenuItem parent, string title, Func<bool> getter, Action<bool> setter)
    {
        var item = new ToolStripMenuItem(title, null, (s, _) =>
        {
            if (s is ToolStripMenuItem mi)
            {
                mi.Checked = !mi.Checked;
                setter(mi.Checked);
                _config.Save();
                _dock.RefreshNow(true);
            }
        })
        {
            Checked = getter()
        };
        parent.DropDownItems.Add(item);
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

    static void SetPosition(bool anchor, BarEdge edge)
    {
        _config.AnchorEdge = anchor;
        _config.Edge = edge;
        _dock.AnchorEdge = anchor;
        _dock.Edge = edge;
        _miEdgeTop.Checked = anchor && edge == BarEdge.Top;
        _miEdgeBottom.Checked = anchor && edge == BarEdge.Bottom;
        _miEdgeFloat.Checked = !anchor;
        _config.Save();
        _dock.RefreshNow(true);
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
        IntPtr root = fg != IntPtr.Zero ? Native.GetAncestor(fg, Native.GA_ROOT) : IntPtr.Zero;
        if (root != IntPtr.Zero) fg = root;

        if (fg != IntPtr.Zero && fg != _dock.Handle && _vda.WindowDesktop(fg) == current)
            _lastActive[current] = fg;

        // Drop foreground from any app window before switching virtual desktops.
        // If an app remains foreground while Windows switches to a desktop that has
        // no active window, Windows 11 drags that foreground app to the new desktop!
        IntPtr shell = Native.GetShellWindow();
        if (shell != IntPtr.Zero) Native.SetForegroundWindow(shell);

        _vda.GoTo(index);
        System.Threading.Thread.Sleep(80);

        bool restored = false;
        if (_lastActive.TryGetValue(index, out IntPtr restore) && Native.IsWindow(restore))
        {
            if (_vda.WindowDesktop(restore) == index)
            {
                Native.SetForegroundWindow(restore);
                restored = true;
            }
            else
            {
                _lastActive.Remove(index);
            }
        }

        if (!restored)
        {
            var win = TaskbarWindows.Enumerate(true).FirstOrDefault(w => _vda.WindowDesktop(w.Hwnd) == index);
            if (win.Hwnd != IntPtr.Zero && Native.IsWindow(win.Hwnd))
            {
                Native.SetForegroundWindow(win.Hwnd);
                _lastActive[index] = win.Hwnd;
            }
        }

        _dock.RefreshNow(true);
    }

    static void MoveActiveWindow(int index)
    {
        if (!_vda.Loaded) return;
        IntPtr fg = Native.GetForegroundWindow();
        if (fg == IntPtr.Zero || fg == _dock.Handle) return;

        IntPtr root = Native.GetAncestor(fg, Native.GA_ROOT);
        if (root != IntPtr.Zero) fg = root;

        // Clear this window from all desktop restore caches so switching back never pulls it
        foreach (var kvp in _lastActive.Where(kvp => kvp.Value == fg).ToList())
            _lastActive.Remove(kvp.Key);

        _vda.MoveWindow(fg, index);
        System.Threading.Thread.Sleep(50);

        _lastActive[index] = fg;

        IntPtr shell = Native.GetShellWindow();
        if (shell != IntPtr.Zero) Native.SetForegroundWindow(shell);

        _vda.GoTo(index);
        System.Threading.Thread.Sleep(80);

        if (Native.IsWindow(fg)) Native.SetForegroundWindow(fg);

        _dock.RefreshNow(true);
    }
}
