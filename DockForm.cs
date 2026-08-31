using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace I3wsb;

/// <summary>
/// Borderless always-on-top overlay. Two looks, switchable at runtime:
///   <see cref="DockView.Icons"/> — mac-style icon strip of the current desktop's windows.
///   <see cref="DockView.Bar"/>   — i3-style workspace bar: "1 [brave-code] | 2 [term] | 3 []".
/// </summary>
internal sealed class DockForm : Form
{
    const int IconSize = 40;
    const int Gap = 8;
    const int Pad = 10;
    const int BarPad = 8;

    const string ClockFormat = "yyyy-MM-dd  HH:mm:ss";

    readonly Vda _vda;
    readonly ToolTip _tip = new() { ShowAlways = true, InitialDelay = 250, ReshowDelay = 100 };
    readonly Font _barFont = new("Consolas", 10f);
    readonly System.Windows.Forms.Timer _clockTimer = new() { Interval = 1000 };
    AppBar? _appBar;
    Rectangle _clockRect;
    int _clockWidth;

    // clickable regions for the active view; index-aligned with _apps (Icons) or _segs (Bar)
    readonly List<Rectangle> _rects = new();
    List<AppWindow> _apps = new();
    readonly List<BarSeg> _segs = new();
    readonly List<(Rectangle rect, string text)> _seps = new();

    int _hover = -1;
    string _lastSig = "";

    DockView _view = DockView.Icons;

    /// <summary>Icons vs. workspace bar. Assigning forces a rebuild.</summary>
    [System.ComponentModel.Browsable(false)]
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public DockView View
    {
        get => _view;
        set { if (_view != value) { _view = value; RefreshNow(true); } }
    }

    bool _anchorTop = true;

    /// <summary>Pin as a full-width strip to the top edge of the work area (i3-style).</summary>
    [System.ComponentModel.Browsable(false)]
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public bool AnchorTop
    {
        get => _anchorTop;
        set { if (_anchorTop != value) { _anchorTop = value; RefreshNow(true); } }
    }

    /// <summary>Raised when the user clicks a workspace segment in bar view.</summary>
    public event Action<int>? DesktopClicked;

    [System.ComponentModel.Browsable(false)]
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public bool AutoHideOnFullscreen { get; set; } = true;
    bool _hiddenByFullscreen;
    bool _userHidden;

    readonly record struct BarSeg(int Desktop, string Text, bool Focused, bool HasWindows, string Tooltip);

    public DockForm(Vda vda)
    {
        _vda = vda;
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = true;
        StartPosition = FormStartPosition.Manual;
        BackColor = Color.FromArgb(30, 30, 30);
        DoubleBuffered = true;
        Text = "i3wsb";
        var wa = Screen.PrimaryScreen!.WorkingArea;
        Location = new Point(wa.Left + 12, wa.Top + 12);
        Size = new Size(Pad * 2 + IconSize, Pad * 2 + IconSize);

        _clockTimer.Tick += (_, _) =>
        {
            if (_view != DockView.Bar || !Visible) return;
            if (_clockRect.IsEmpty) Invalidate(); else Invalidate(_clockRect);
        };
        _clockTimer.Start();
    }

    protected override CreateParams CreateParams
    {
        get
        {
            const int WS_EX_NOACTIVATE = 0x08000000;
            const int WS_EX_TOOLWINDOW = 0x00000080;
            var cp = base.CreateParams;
            cp.ExStyle |= WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW;
            return cp;
        }
    }

    protected override bool ShowWithoutActivation => true;

    // ---- data ----------------------------------------------------------

    public void RefreshNow(bool force = false)
    {
        if (!_vda.Loaded) return;
        if (_view == DockView.Bar) RefreshBar(force);
        else RefreshIcons(force);
    }

    void RefreshIcons(bool force)
    {
        var apps = new List<AppWindow>();
        foreach (var w in TaskbarWindows.Enumerate())
            if (_vda.IsOnCurrentDesktop(w.Hwnd))
                apps.Add(w);

        string sig = "I|" + _vda.CurrentDesktop + "|" + string.Join(",", apps.Select(a => a.Hwnd));
        if (!force && sig == _lastSig) return;
        _lastSig = sig;
        _apps = apps;

        _seps.Clear();
        _rects.Clear();
        int count = Math.Max(_apps.Count, 1);
        int bw = Pad * 2 + count * IconSize + (count - 1) * Gap;
        int bh = Pad * 2 + IconSize;

        int x = Pad;
        for (int i = 0; i < _apps.Count; i++)
        {
            _rects.Add(new Rectangle(x, Pad, IconSize, IconSize));
            x += IconSize + Gap;
        }
        ApplyGeometry(bw, bh, rounded: true);
    }

    void RefreshBar(bool force)
    {
        int current = _vda.CurrentDesktop;

        var buckets = new Dictionary<int, List<AppWindow>>();
        foreach (var w in TaskbarWindows.Enumerate())
        {
            int d = _vda.WindowDesktop(w.Hwnd);
            if (d < 0) continue;
            if (!buckets.TryGetValue(d, out var list)) buckets[d] = list = new();
            list.Add(w);
        }

        int count = _vda.DesktopCount;
        if (count < 1)
        {
            count = current;
            foreach (int k in buckets.Keys) if (k > count) count = k;
            count += 1;
        }

        string sig = "B|" + current + "|" + string.Join(",",
            buckets.OrderBy(kv => kv.Key).Select(kv => kv.Key + ":" + string.Join(".", kv.Value.Select(w => w.Hwnd))));
        if (!force && sig == _lastSig) return;
        _lastSig = sig;

        _segs.Clear();
        _seps.Clear();
        _rects.Clear();

        for (int d = 0; d < count; d++)
        {
            var wins = buckets.TryGetValue(d, out var l) ? l : new List<AppWindow>();
            string text = $" {d + 1} [{AppNames.Label(wins)}] ";
            string tip = string.Join("\n", wins.Select(w => w.Title));
            _segs.Add(new BarSeg(d, text, d == current, wins.Count > 0, tip));
        }

        int h = _barFont.Height + BarPad * 2;
        int x = BarPad;
        using var g = CreateGraphics();
        for (int i = 0; i < _segs.Count; i++)
        {
            int segW = MeasureW(g, _segs[i].Text);
            _rects.Add(new Rectangle(x, BarPad, segW, _barFont.Height));
            x += segW;
            if (i < _segs.Count - 1)
            {
                int sepW = MeasureW(g, "|");
                _seps.Add((new Rectangle(x, BarPad, sepW, _barFont.Height), "|"));
                x += sepW;
            }
        }

        // reserve room for the right-aligned clock (drawn against the real
        // window edge in PaintBar, so it works anchored or floating)
        _clockWidth = MeasureW(g, DateTime.Now.ToString(ClockFormat)) + 2;
        int floatW = x + BarPad + 24 + _clockWidth + BarPad;

        ApplyGeometry(floatW, h, rounded: false);
    }

    /// <summary>Place the window: full-width at the top edge when anchored,
    /// otherwise a content-sized floating strip at its current location.</summary>
    void ApplyGeometry(int contentW, int contentH, bool rounded)
    {
        if (_anchorTop)
        {
            Region = null;
            _appBar ??= new AppBar(Handle);
            _appBar.EnsureRegistered(contentH);   // reserves work area + positions/sizes the window
        }
        else
        {
            _appBar?.Unregister();
            Region = null;
            Size = new Size(contentW, contentH);
            if (rounded)
            {
                using var path = RoundedRect(new Rectangle(0, 0, contentW, contentH), 14);
                Region = new Region(path);
            }
        }
        Invalidate();
    }

    int MeasureW(Graphics g, string s) =>
        TextRenderer.MeasureText(g, s, _barFont, Size.Empty, TextFormatFlags.NoPadding).Width;

    static GraphicsPath RoundedRect(Rectangle r, int radius)
    {
        int d = radius * 2;
        var p = new GraphicsPath();
        p.AddArc(r.X, r.Y, d, d, 180, 90);
        p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        p.CloseFigure();
        return p;
    }

    // ---- paint ---------------------------------------------------------

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        using (var bg = new SolidBrush(Color.FromArgb(30, 30, 30)))
            g.FillRectangle(bg, ClientRectangle);

        if (_view == DockView.Bar) PaintBar(g);
        else PaintIcons(g);
    }

    void PaintIcons(Graphics g)
    {
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        if (_apps.Count == 0)
        {
            TextRenderer.DrawText(g, "empty", Font, ClientRectangle,
                Color.Gray, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            return;
        }

        for (int i = 0; i < _apps.Count && i < _rects.Count; i++)
        {
            var r = _rects[i];
            if (i == _hover)
                using (var hl = new SolidBrush(Color.FromArgb(60, 255, 255, 255)))
                using (var hp = RoundedRect(Rectangle.Inflate(r, 4, 4), 8))
                    g.FillPath(hl, hp);

            var icon = TaskbarWindows.IconFor(_apps[i].ExePath, IconSize);
            if (icon != null)
            {
                g.DrawImage(icon, r);
            }
            else
            {
                using var tile = new SolidBrush(Color.FromArgb(48, 48, 48));
                g.FillRectangle(tile, r);
                string letter = _apps[i].Title.Length > 0 ? _apps[i].Title[..1] : "?";
                TextRenderer.DrawText(g, letter, new Font("Segoe UI", 14), r, Color.White,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            }
        }
    }

    static readonly Color FocusBg = Color.FromArgb(0x28, 0x55, 0x77);
    static readonly Color HoverBg = Color.FromArgb(0x3C, 0x3C, 0x3C);
    static readonly Color UsedFg = Color.FromArgb(0xCC, 0xCC, 0xCC);
    static readonly Color EmptyFg = Color.FromArgb(0x80, 0x80, 0x80);
    static readonly Color SepFg = Color.FromArgb(0x55, 0x55, 0x55);
    static readonly Color ClockFg = Color.FromArgb(0xD0, 0xD0, 0xD0);

    void PaintBar(Graphics g)
    {
        const TextFormatFlags fmt = TextFormatFlags.NoPadding | TextFormatFlags.VerticalCenter | TextFormatFlags.Left;

        for (int i = 0; i < _segs.Count && i < _rects.Count; i++)
        {
            var s = _segs[i];
            var r = _rects[i];
            if (s.Focused)
                using (var b = new SolidBrush(FocusBg)) g.FillRectangle(b, r);
            else if (i == _hover)
                using (var b = new SolidBrush(HoverBg)) g.FillRectangle(b, r);

            Color fg = s.Focused ? Color.White : s.HasWindows ? UsedFg : EmptyFg;
            TextRenderer.DrawText(g, s.Text, _barFont, r, fg, fmt);
        }

        foreach (var (rect, text) in _seps)
            TextRenderer.DrawText(g, text, _barFont, rect, SepFg, fmt);

        // right section: date + time
        int w = Math.Max(_clockWidth, 1);
        _clockRect = new Rectangle(ClientRectangle.Right - w - BarPad, BarPad, w, _barFont.Height);
        TextRenderer.DrawText(g, DateTime.Now.ToString(ClockFormat), _barFont, _clockRect, ClockFg,
            TextFormatFlags.NoPadding | TextFormatFlags.VerticalCenter | TextFormatFlags.Right);
    }

    // ---- interaction --------------------------------------------------

    [DllImport("user32.dll")] static extern bool ReleaseCapture();
    [DllImport("user32.dll")] static extern IntPtr SendMessageW(IntPtr h, int msg, IntPtr wp, IntPtr lp);
    const int WM_NCLBUTTONDOWN = 0xA1;
    const int HTCAPTION = 2;

    protected override void OnMouseDown(MouseEventArgs e)
    {
        int hit = HitTest(e.Location);
        if (hit >= 0 && e.Button == MouseButtons.Left)
        {
            if (_view == DockView.Bar) DesktopClicked?.Invoke(_segs[hit].Desktop);
            else ActivateWindow(_apps[hit].Hwnd);
            return;
        }
        if (e.Button == MouseButtons.Left && !_anchorTop)   // anchored strip isn't draggable
        {
            ReleaseCapture();
            SendMessageW(Handle, WM_NCLBUTTONDOWN, HTCAPTION, IntPtr.Zero);
        }
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        int hit = HitTest(e.Location);
        if (hit != _hover)
        {
            _hover = hit;
            Invalidate();
            _tip.Hide(this);
            string? tip = hit < 0 ? null
                : _view == DockView.Bar ? (_segs[hit].Tooltip.Length > 0 ? _segs[hit].Tooltip : null)
                : _apps[hit].Title;
            if (tip != null)
                _tip.Show(tip, this, e.X + 12, e.Y + 24, 4000);
        }
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        _hover = -1;
        _tip.Hide(this);
        Invalidate();
    }

    int HitTest(Point p)
    {
        for (int i = 0; i < _rects.Count; i++)
            if (_rects[i].Contains(p)) return i;
        return -1;
    }

    static void ActivateWindow(IntPtr hwnd)
    {
        if (!Native.IsWindow(hwnd)) return;
        if (Native.IsIconic(hwnd)) Native.ShowWindow(hwnd, Native.SW_RESTORE);
        Native.SetForegroundWindow(hwnd);
    }

    // ---- visibility state --------------------------------------------

    public void ToggleUserVisible()
    {
        _userHidden = !_userHidden;
        ApplyVisibility();
        if (!_userHidden) RefreshNow(true);
    }

    public void SetAlwaysOnTop(bool on) => TopMost = on;

    public void FullscreenGuardTick()
    {
        if (!AutoHideOnFullscreen) { if (_hiddenByFullscreen) { _hiddenByFullscreen = false; ApplyVisibility(); } return; }
        bool fs = FullscreenDetector.ForegroundIsFullscreen(Handle);
        if (fs != _hiddenByFullscreen)
        {
            _hiddenByFullscreen = fs;
            ApplyVisibility();
        }
    }

    void ApplyVisibility()
    {
        bool shouldShow = !_userHidden && !_hiddenByFullscreen;
        if (shouldShow && !Visible) Show();
        else if (!shouldShow && Visible) Hide();

        // Give the reserved work area back while hidden (so a fullscreen app or a
        // manual hide really gets the whole screen), reclaim it when shown.
        if (_anchorTop && _appBar != null)
        {
            if (shouldShow) _appBar.EnsureRegistered(Math.Max(Height, _barFont.Height));
            else _appBar.Unregister();
        }
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (e.CloseReason == CloseReason.UserClosing)
        {
            e.Cancel = true;
            _userHidden = true;
            ApplyVisibility();
        }
        else base.OnFormClosing(e);
    }

    protected override void OnHandleDestroyed(EventArgs e)
    {
        _clockTimer.Stop();
        _appBar?.Unregister();   // release the reserved work area
        base.OnHandleDestroyed(e);
    }

    /// <summary>Belt-and-suspenders: called from Program on ApplicationExit.</summary>
    public void ReleaseAppBar() => _appBar?.Unregister();

    protected override void WndProc(ref Message m)
    {
        const int WM_WINDOWPOSCHANGED = 0x0047;
        if (_appBar != null)
        {
            if (_appBar.HandleMessage(m.Msg, m.WParam)) { base.WndProc(ref m); return; }
            if (m.Msg == WM_WINDOWPOSCHANGED) _appBar.OnWindowPosChanged();
        }
        base.WndProc(ref m);
    }
}
