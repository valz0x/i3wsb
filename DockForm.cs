using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace I3wsb;

/// <summary>
/// Borderless always-on-top overlay. Two looks, switchable at runtime:
///   <see cref="DockView.Icons"/> — clean statusbar with workspace numbers + compact app icons in center.
///   <see cref="DockView.Bar"/>   — clean statusbar with workspace numbers + app title pills in center.
/// </summary>
internal sealed class DockForm : Form
{
    const int IconSize = 20;
    const int Gap = 6;
    const int Pad = 8;
    const int BarHeight = 32;
    const int PillMarginY = 4;
    const int PillRadius = 5;

    readonly Vda _vda;
    readonly ToolTip _tip = new() { ShowAlways = true, InitialDelay = 250, ReshowDelay = 100 };
    readonly Font _barFont;
    readonly Font _barFontBold;
    readonly System.Windows.Forms.Timer _clockTimer = new() { Interval = 1000 };
    AppBar? _appBar;

    [System.ComponentModel.Browsable(false)]
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public Theme Theme { get; set; } = Theme.Dark;

    [System.ComponentModel.Browsable(false)]
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public bool ShowCpu { get; set; } = true;

    [System.ComponentModel.Browsable(false)]
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public bool ShowRam { get; set; } = true;

    [System.ComponentModel.Browsable(false)]
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public bool ShowVolume { get; set; } = true;

    [System.ComponentModel.Browsable(false)]
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public bool ShowBattery { get; set; } = true;

    [System.ComponentModel.Browsable(false)]
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public bool ShowOpenApps { get; set; } = true;

    [System.ComponentModel.Browsable(false)]
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public bool ShowClock { get; set; } = true;

    [System.ComponentModel.Browsable(false)]
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public bool AutoPrune { get; set; } = true;

    // Interactive regions
    enum HitKind { Workspace, AddDesktop, CenterApp, AppIcon, Cpu, Ram, Volume, Battery, Clock }
    readonly record struct HitTarget(Rectangle Rect, HitKind Kind, int Index, string Text, string Tooltip, IntPtr Hwnd = default, bool HasWindows = false);

    readonly List<HitTarget> _hitTargets = new();
    int _hoverIndex = -1;
    string _lastSig = "";

    DockView _view = DockView.Bar;
    bool _anchorEdge = true;
    BarEdge _edge = BarEdge.Top;

    [System.ComponentModel.Browsable(false)]
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public DockView View
    {
        get => _view;
        set { if (_view != value) { _view = value; RefreshNow(true); } }
    }

    [System.ComponentModel.Browsable(false)]
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public bool AnchorEdge
    {
        get => _anchorEdge;
        set { if (_anchorEdge != value) { _anchorEdge = value; RefreshNow(true); } }
    }

    [System.ComponentModel.Browsable(false)]
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public BarEdge Edge
    {
        get => _edge;
        set
        {
            if (_edge != value)
            {
                _edge = value;
                if (_appBar != null) _appBar.Edge = value;
                RefreshNow(true);
            }
        }
    }

    public event Action<int>? DesktopClicked;

    [System.ComponentModel.Browsable(false)]
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public bool AutoHideOnFullscreen { get; set; } = true;
    bool _hiddenByFullscreen;
    bool _userHidden;

    public DockForm(Vda vda)
    {
        _vda = vda;
        _barFont = ResolveFont("Segoe UI Variable Text", "Segoe UI", 9.5f, FontStyle.Regular);
        _barFontBold = ResolveFont("Segoe UI Variable Text", "Segoe UI", 9.5f, FontStyle.Bold);

        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = true;
        StartPosition = FormStartPosition.Manual;
        DoubleBuffered = true;
        Text = "i3wsb";

        var wa = Screen.PrimaryScreen!.WorkingArea;
        Location = new Point(wa.Left + 12, wa.Top + 12);
        Size = new Size(Pad * 2 + IconSize, Pad * 2 + IconSize);

        _clockTimer.Tick += (_, _) =>
        {
            if (!Visible) return;
            Invalidate();
        };
        _clockTimer.Start();
    }

    static Font ResolveFont(string primary, string fallback, float size, FontStyle style)
    {
        try
        {
            using var test = new FontFamily(primary);
            return new Font(primary, size, style);
        }
        catch
        {
            return new Font(fallback, size, style);
        }
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

    // ---- Data Refresh ----------------------------------------------------

    public void RefreshNow(bool force = false)
    {
        if (!_vda.Loaded) return;
        if (_userHidden || _hiddenByFullscreen) return;
        RefreshBar(force);
    }

    void RefreshBar(bool force)
    {
        int current = _vda.CurrentDesktop;

        var buckets = new Dictionary<int, List<AppWindow>>();
        foreach (var w in TaskbarWindows.Enumerate(true))
        {
            int d = _vda.WindowDesktop(w.Hwnd);
            if (d < 0) continue;
            if (!buckets.TryGetValue(d, out var list)) buckets[d] = list = new();
            list.Add(w);
        }

        int vdaCount = _vda.DesktopCount;
        if (vdaCount < 1)
        {
            vdaCount = current + 1;
            foreach (int k in buckets.Keys) if (k >= vdaCount) vdaCount = k + 1;
        }

        if (AutoPrune && _vda.CanRemove)
        {
            var occupied = new HashSet<int>(buckets.Where(kv => kv.Value.Count > 0).Select(kv => kv.Key));
            _vda.PruneTrailingEmptyDesktops(occupied);
            vdaCount = _vda.DesktopCount;
        }

        // Always show at least 5 desktop numbers, up to 9
        int displayCount = Math.Clamp(Math.Max(vdaCount, 5), 5, 9);

        IntPtr fg = Native.GetForegroundWindow();
        IntPtr root = fg != IntPtr.Zero ? Native.GetAncestor(fg, Native.GA_ROOT) : IntPtr.Zero;
        if (root != IntPtr.Zero) fg = root;
        string fgTitle = (fg != IntPtr.Zero && fg != Handle) ? Native.GetTitle(fg) : "";

        string sig = $"B|{current}|{displayCount}|{_view}|{fg}|" + string.Join(",",
            buckets.OrderBy(kv => kv.Key).Select(kv => kv.Key + ":" + string.Join(".", kv.Value.Select(w => w.Hwnd))));

        if (!force && sig == _lastSig)
        {
            Invalidate();
            return;
        }
        _lastSig = sig;

        _hitTargets.Clear();

        using var g = CreateGraphics();
        
        int pillH = BarHeight - (PillMarginY * 2);
        int curX = Pad;
        // 1. Workspace number pills (Left side) - always visible in all views
        for (int d = 0; d < displayCount; d++)
        {
            var wins = buckets.TryGetValue(d, out var l) ? l : new List<AppWindow>();
            bool hasWindows = wins.Count > 0;
            string tip = hasWindows
                ? $"Desktop {d + 1} ({wins.Count} windows):\n" + string.Join("\n", wins.Select(w => "• " + w.Title))
                : $"Desktop {d + 1} (empty)";

            int pillW = 28; // Uniform compact pill
            var r = new Rectangle(curX, PillMarginY, pillW, pillH);
            _hitTargets.Add(new HitTarget(r, HitKind.Workspace, d, $"{d + 1}", tip, default, hasWindows));
            curX += pillW + Gap;
        }

        // Add desktop button '+' if count < 9
        if (_vda.CanCreate && displayCount < 9)
        {
            int plusW = 22;
            var plusRect = new Rectangle(curX, PillMarginY, plusW, pillH);
            _hitTargets.Add(new HitTarget(plusRect, HitKind.AddDesktop, displayCount, "+", "Create new virtual desktop"));
            curX += plusW + Gap;
        }

        // 2. Center: Open apps on the current desktop (either compact icons or text pills)
        if (ShowOpenApps)
        {
            var curDesktopApps = buckets.TryGetValue(current, out var curList) ? curList : new List<AppWindow>();
            if (curDesktopApps.Count > 0)
            {
                if (_view == DockView.Icons)
                {
                    // Render as compact icon pills
                    int iconPillW = 28;
                    int totalCenterW = curDesktopApps.Count * iconPillW + (curDesktopApps.Count - 1) * Gap;
                    int barWidth = ClientRectangle.Width > 0 ? ClientRectangle.Width : Screen.PrimaryScreen!.Bounds.Width;
                    int startCenterX = Math.Max((barWidth - totalCenterW) / 2, curX + Gap + 8);

                    int appX = startCenterX;
                    foreach (var app in curDesktopApps)
                    {
                        var appRect = new Rectangle(appX, PillMarginY, iconPillW, pillH);
                        _hitTargets.Add(new HitTarget(appRect, HitKind.AppIcon, 0, app.ExePath, app.Title, app.Hwnd, app.Hwnd == fg));
                        appX += iconPillW + Gap;
                    }
                }
                else
                {
                    // Render as text title pills
                    var centerPills = new List<(string text, string fullTitle, IntPtr hwnd, int width, bool isFg)>();
                    foreach (var app in curDesktopApps)
                    {
                        string shortApp = AppNames.Short(app.ExePath, app.Title);
                        string cleanTitle = app.Title;
                        string displayText = string.IsNullOrEmpty(cleanTitle) ? shortApp
                            : $"{shortApp}: {(cleanTitle.Length > 28 ? cleanTitle[..26] + "…" : cleanTitle)}";

                        int textW = TextRenderer.MeasureText(g, displayText, _barFont, Size.Empty, TextFormatFlags.NoPadding).Width;
                        int pillW = textW + 16;
                        centerPills.Add((displayText, app.Title, app.Hwnd, pillW, app.Hwnd == fg));
                    }

                    int totalCenterW = centerPills.Sum(p => p.width) + (centerPills.Count - 1) * Gap;
                    int barWidth = ClientRectangle.Width > 0 ? ClientRectangle.Width : Screen.PrimaryScreen!.Bounds.Width;
                    int startCenterX = Math.Max((barWidth - totalCenterW) / 2, curX + Gap + 8);

                    int appX = startCenterX;
                    foreach (var cp in centerPills)
                    {
                        var appRect = new Rectangle(appX, PillMarginY, cp.width, pillH);
                        _hitTargets.Add(new HitTarget(appRect, HitKind.CenterApp, 0, cp.text, cp.fullTitle, cp.hwnd, cp.isFg));
                        appX += cp.width + Gap;
                    }
                }
            }
        }

        // 3. Right side modules width calculation for floating mode
        int rightModulesWidth = 0;
        if (ShowClock) rightModulesWidth += 160 + Gap;
        if (ShowBattery) rightModulesWidth += 80 + Gap;
        if (ShowVolume) rightModulesWidth += 76 + Gap;
        if (ShowRam) rightModulesWidth += 76 + Gap;
        if (ShowCpu) rightModulesWidth += 72 + Gap;

        int totalContentW = Math.Max(curX + rightModulesWidth + 30, 600);
        ApplyGeometry(totalContentW, BarHeight, rounded: false);
    }

    void ApplyGeometry(int contentW, int contentH, bool rounded)
    {
        bool shouldShow = !_userHidden && !_hiddenByFullscreen;
        if (_anchorEdge)
        {
            Region = null;
            if (shouldShow)
            {
                _appBar ??= new AppBar(Handle);
                _appBar.EnsureRegistered(contentH, _edge);
            }
            else
            {
                _appBar?.Unregister();
            }
        }
        else
        {
            _appBar?.Unregister();
            Region = null;
            Size = new Size(contentW, contentH);
            if (rounded)
            {
                using var path = RoundedRect(new Rectangle(0, 0, contentW, contentH), 12);
                Region = new Region(path);
            }
        }
        Invalidate();
    }

    static GraphicsPath RoundedRect(Rectangle r, int radius)
    {
        int d = radius * 2;
        var p = new GraphicsPath();
        if (r.Width <= d || r.Height <= d)
        {
            p.AddRectangle(r);
            return p;
        }
        p.AddArc(r.X, r.Y, d, d, 180, 90);
        p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        p.CloseFigure();
        return p;
    }

    // ---- Painting --------------------------------------------------------

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

        using (var bgBrush = new SolidBrush(Theme.BarBg))
            g.FillRectangle(bgBrush, ClientRectangle);

        PaintBar(g);
    }

    void PaintBar(Graphics g)
    {
        int currentDesktop = _vda.CurrentDesktop;
        const TextFormatFlags textFmtCenter = TextFormatFlags.NoPadding | TextFormatFlags.VerticalCenter | TextFormatFlags.HorizontalCenter;
        const TextFormatFlags textFmtLeft = TextFormatFlags.NoPadding | TextFormatFlags.VerticalCenter | TextFormatFlags.Left;

        // 1. Paint left-side targets & center app targets
        for (int i = 0; i < _hitTargets.Count; i++)
        {
            var target = _hitTargets[i];
            var r = target.Rect;
            bool isHovered = (i == _hoverIndex);

            switch (target.Kind)
            {
                case HitKind.Workspace:
                    {
                        bool isFocused = (target.Index == currentDesktop);
                        Color bg = isFocused ? Theme.FocusedPillBg : isHovered ? Theme.PillHoverBg : Theme.PillBg;
                        Color border = isFocused ? Theme.FocusedPillBorder : Theme.PillBorder;
                        Color fg = isFocused ? Theme.FocusedPillFg : target.HasWindows ? Theme.OccupiedPillFg : Theme.EmptyPillFg;

                        DrawPill(g, r, bg, border, PillRadius);

                        var font = isFocused ? _barFontBold : _barFont;
                        TextRenderer.DrawText(g, target.Text, font, r, fg, textFmtCenter);

                        // If occupied (has windows) and not focused, draw a subtle indicator dot at the bottom
                        if (target.HasWindows && !isFocused)
                        {
                            using var dotBrush = new SolidBrush(Theme.OccupiedPillFg);
                            int dotSize = 3;
                            int dotX = (r.Left + r.Right - dotSize) / 2;
                            int dotY = r.Bottom - 4;
                            g.FillEllipse(dotBrush, dotX, dotY, dotSize, dotSize);
                        }
                        break;
                    }

                case HitKind.AddDesktop:
                    {
                        Color bg = isHovered ? Theme.PillHoverBg : Theme.PillBg;
                        DrawPill(g, r, bg, Theme.PillBorder, PillRadius);
                        TextRenderer.DrawText(g, "+", _barFontBold, r, Theme.EmptyPillFg, textFmtCenter);
                        break;
                    }

                case HitKind.CenterApp:
                    {
                        bool isFg = target.HasWindows; // flag used for isForeground
                        Color bg = isFg ? Theme.ActiveWindowBg : isHovered ? Theme.PillHoverBg : Theme.PillBg;
                        Color border = isFg ? Theme.FocusedPillBorder : Theme.PillBorder;
                        Color fg = isFg ? Theme.ActiveWindowFg : Theme.OccupiedPillFg;

                        DrawPill(g, r, bg, border, PillRadius);

                        Rectangle textRect = Rectangle.Inflate(r, -8, 0);
                        var font = isFg ? _barFontBold : _barFont;
                        TextRenderer.DrawText(g, target.Text, font, textRect, fg, textFmtLeft);
                        break;
                    }

                case HitKind.AppIcon:
                    {
                        bool isFg = target.HasWindows; // flag used for isForeground
                        Color bg = isFg ? Theme.ActiveWindowBg : isHovered ? Theme.PillHoverBg : Theme.PillBg;
                        Color border = isFg ? Theme.FocusedPillBorder : Theme.PillBorder;

                        DrawPill(g, r, bg, border, PillRadius);

                        var icon = TaskbarWindows.IconFor(target.Text, IconSize);
                        if (icon != null)
                        {
                            int iconX = r.X + (r.Width - IconSize) / 2;
                            int iconY = r.Y + (r.Height - IconSize) / 2;
                            g.DrawImage(icon, iconX, iconY, IconSize, IconSize);
                        }
                        else
                        {
                            string letter = target.Tooltip.Length > 0 ? target.Tooltip[..1] : "?";
                            TextRenderer.DrawText(g, letter, _barFontBold, r, isFg ? Theme.ActiveWindowFg : Theme.OccupiedPillFg, textFmtCenter);
                        }
                        break;
                    }
            }
        }

        // 2. Right-side modules
        int pillH = BarHeight - (PillMarginY * 2);
        int curRight = ClientRectangle.Right - Pad;

        // Clock Widget
        if (ShowClock)
        {
            string timeStr = DateTime.Now.ToString("ddd d MMM  HH:mm:ss");
            int clockW = TextRenderer.MeasureText(g, timeStr, _barFont, Size.Empty, TextFormatFlags.NoPadding).Width + 16;
            curRight -= clockW;
            var clockRect = new Rectangle(curRight, PillMarginY, clockW, pillH);
            UpdateOrAddHitTarget(clockRect, HitKind.Clock, 0, timeStr, DateTime.Now.ToLongDateString() + " " + DateTime.Now.ToLongTimeString());

            bool isHovered = IsTargetHovered(clockRect);
            DrawPill(g, clockRect, isHovered ? Theme.PillHoverBg : Theme.WidgetBg, Theme.WidgetBorder, PillRadius);
            TextRenderer.DrawText(g, timeStr, _barFont, clockRect, Theme.ClockFg, textFmtCenter);
            curRight -= Gap;
        }

        // Battery Widget
        if (ShowBattery)
        {
            var (hasBattery, batteryPct, isCharging) = SystemMetrics.GetBatteryStatus();
            if (hasBattery)
            {
                string batText = isCharging ? $"BAT {batteryPct}% ⚡" : $"BAT {batteryPct}%";
                int batW = TextRenderer.MeasureText(g, batText, _barFont, Size.Empty, TextFormatFlags.NoPadding).Width + 14;
                curRight -= batW;
                var batRect = new Rectangle(curRight, PillMarginY, batW, pillH);
                string tip = $"Battery: {batteryPct}% " + (isCharging ? "(Charging)" : "(On Battery)");
                UpdateOrAddHitTarget(batRect, HitKind.Battery, 0, batText, tip);

                bool isHovered = IsTargetHovered(batRect);
                Color fg = (batteryPct < 20 && !isCharging) ? Theme.WidgetCriticalFg
                         : (batteryPct < 35 && !isCharging) ? Theme.WidgetWarningFg
                         : Theme.WidgetFg;

                DrawPill(g, batRect, isHovered ? Theme.PillHoverBg : Theme.WidgetBg, Theme.WidgetBorder, PillRadius);
                TextRenderer.DrawText(g, batText, _barFont, batRect, fg, textFmtCenter);
                curRight -= Gap;
            }
            else
            {
                RemoveHitTarget(HitKind.Battery);
            }
        }
        else
        {
            RemoveHitTarget(HitKind.Battery);
        }

        // Volume Widget
        if (ShowVolume)
        {
            bool volOk = AudioService.TryGetMasterVolume(out int volPct, out bool isMuted);
            string volText = !volOk ? "VOL --" : isMuted ? "VOL MUTE" : $"VOL {volPct}%";
            int volW = 74;
            curRight -= volW;
            var volRect = new Rectangle(curRight, PillMarginY, volW, pillH);
            string tip = !volOk ? "Audio device unavailable"
                : $"Master Volume: {(isMuted ? "Muted" : $"{volPct}%")}\n• Click to toggle Mute\n• Scroll wheel to adjust volume";
            UpdateOrAddHitTarget(volRect, HitKind.Volume, 0, volText, tip);

            bool isHovered = IsTargetHovered(volRect);
            Color fg = isMuted ? Theme.WidgetCriticalFg : Theme.WidgetFg;
            DrawPill(g, volRect, isHovered ? Theme.PillHoverBg : Theme.WidgetBg, Theme.WidgetBorder, PillRadius);
            TextRenderer.DrawText(g, volText, _barFont, volRect, fg, textFmtCenter);
            curRight -= Gap;
        }
        else
        {
            RemoveHitTarget(HitKind.Volume);
        }

        // RAM Widget
        if (ShowRam)
        {
            var (ramPct, ramSummary) = SystemMetrics.GetMemoryUsage();
            string ramText = $"RAM {ramPct}%";
            int ramW = 74;
            curRight -= ramW;
            var ramRect = new Rectangle(curRight, PillMarginY, ramW, pillH);
            string tip = $"Memory Usage: {ramSummary}";
            UpdateOrAddHitTarget(ramRect, HitKind.Ram, 0, ramText, tip);

            bool isHovered = IsTargetHovered(ramRect);
            Color fg = (ramPct >= 90) ? Theme.WidgetCriticalFg
                     : (ramPct >= 80) ? Theme.WidgetWarningFg
                     : Theme.WidgetFg;

            DrawPill(g, ramRect, isHovered ? Theme.PillHoverBg : Theme.WidgetBg, Theme.WidgetBorder, PillRadius);
            TextRenderer.DrawText(g, ramText, _barFont, ramRect, fg, textFmtCenter);
            curRight -= Gap;
        }
        else
        {
            RemoveHitTarget(HitKind.Ram);
        }

        // CPU Widget
        if (ShowCpu)
        {
            int cpuPct = SystemMetrics.GetCpuUsagePercent();
            string cpuText = $"CPU {cpuPct}%";
            int cpuW = 70;
            curRight -= cpuW;
            var cpuRect = new Rectangle(curRight, PillMarginY, cpuW, pillH);
            string tip = $"Processor Usage: {cpuPct}%";
            UpdateOrAddHitTarget(cpuRect, HitKind.Cpu, 0, cpuText, tip);

            bool isHovered = IsTargetHovered(cpuRect);
            Color fg = (cpuPct >= 90) ? Theme.WidgetCriticalFg
                     : (cpuPct >= 75) ? Theme.WidgetWarningFg
                     : Theme.WidgetFg;

            DrawPill(g, cpuRect, isHovered ? Theme.PillHoverBg : Theme.WidgetBg, Theme.WidgetBorder, PillRadius);
            TextRenderer.DrawText(g, cpuText, _barFont, cpuRect, fg, textFmtCenter);
        }
        else
        {
            RemoveHitTarget(HitKind.Cpu);
        }
    }

    static void DrawPill(Graphics g, Rectangle r, Color bg, Color border, int radius)
    {
        using var path = RoundedRect(r, radius);
        using var brush = new SolidBrush(bg);
        using var pen = new Pen(border, 1f);
        g.FillPath(brush, path);
        g.DrawPath(pen, path);
    }

    void UpdateOrAddHitTarget(Rectangle r, HitKind kind, int index, string text, string tooltip)
    {
        int existing = _hitTargets.FindIndex(t => t.Kind == kind);
        var target = new HitTarget(r, kind, index, text, tooltip);
        if (existing >= 0) _hitTargets[existing] = target;
        else _hitTargets.Add(target);
    }

    void RemoveHitTarget(HitKind kind)
    {
        int existing = _hitTargets.FindIndex(t => t.Kind == kind);
        if (existing >= 0) _hitTargets.RemoveAt(existing);
    }

    bool IsTargetHovered(Rectangle r) => _hoverIndex >= 0 && _hoverIndex < _hitTargets.Count && _hitTargets[_hoverIndex].Rect == r;

    // ---- Mouse Interaction -----------------------------------------------

    [DllImport("user32.dll")] static extern bool ReleaseCapture();
    [DllImport("user32.dll")] static extern IntPtr SendMessageW(IntPtr h, int msg, IntPtr wp, IntPtr lp);
    const int WM_NCLBUTTONDOWN = 0xA1;
    const int HTCAPTION = 2;

    protected override void OnMouseDown(MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Right)
        {
            ContextMenuStrip?.Show(this, e.Location);
            return;
        }

        int hit = HitTest(e.Location);
        if (hit >= 0 && e.Button == MouseButtons.Left)
        {
            var target = _hitTargets[hit];
            switch (target.Kind)
            {
                case HitKind.Workspace:
                    DesktopClicked?.Invoke(target.Index);
                    return;

                case HitKind.AddDesktop:
                    if (_vda.CanCreate)
                    {
                        int newIndex = _vda.DesktopCount;
                        _vda.EnsureDesktop(newIndex);
                        DesktopClicked?.Invoke(newIndex);
                    }
                    return;

                case HitKind.CenterApp:
                case HitKind.AppIcon:
                    if (target.Hwnd != IntPtr.Zero) ActivateWindow(target.Hwnd);
                    return;

                case HitKind.Volume:
                    AudioService.ToggleMute();
                    Invalidate();
                    return;
            }
        }

        // Drag if floating
        if (e.Button == MouseButtons.Left && !_anchorEdge)
        {
            ReleaseCapture();
            SendMessageW(Handle, WM_NCLBUTTONDOWN, HTCAPTION, IntPtr.Zero);
        }
    }

    protected override void OnMouseWheel(MouseEventArgs e)
    {
        base.OnMouseWheel(e);
        int hit = HitTest(e.Location);
        if (hit >= 0 && _hitTargets[hit].Kind == HitKind.Volume)
        {
            int delta = e.Delta > 0 ? 2 : -2;
            AudioService.AdjustVolume(delta);
            Invalidate();
            return;
        }

        // Cycling desktops by scrolling anywhere on the bar
        if (_vda.Loaded)
        {
            int current = _vda.CurrentDesktop;
            int count = Math.Max(_vda.DesktopCount, 1);
            if (count > 1)
            {
                if (e.Delta > 0)
                {
                    int prev = (current - 1 + count) % count;
                    DesktopClicked?.Invoke(prev);
                }
                else if (e.Delta < 0)
                {
                    int next = (current + 1) % count;
                    DesktopClicked?.Invoke(next);
                }
            }
        }
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        int hit = HitTest(e.Location);
        if (hit != _hoverIndex)
        {
            _hoverIndex = hit;
            Invalidate();
            _tip.Hide(this);
            if (hit >= 0 && !string.IsNullOrEmpty(_hitTargets[hit].Tooltip))
            {
                int tipY = _edge == BarEdge.Bottom ? e.Y - 32 : e.Y + 24;
                _tip.Show(_hitTargets[hit].Tooltip, this, e.X + 12, tipY, 4000);
            }
        }
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        _hoverIndex = -1;
        _tip.Hide(this);
        Invalidate();
    }

    int HitTest(Point p)
    {
        for (int i = 0; i < _hitTargets.Count; i++)
            if (_hitTargets[i].Rect.Contains(p)) return i;
        return -1;
    }

    static void ActivateWindow(IntPtr hwnd)
    {
        if (!Native.IsWindow(hwnd)) return;
        if (Native.IsIconic(hwnd)) Native.ShowWindow(hwnd, Native.SW_RESTORE);
        Native.SetForegroundWindow(hwnd);
    }

    // ---- Visibility State ------------------------------------------------

    public void ToggleUserVisible()
    {
        _userHidden = !_userHidden;
        ApplyVisibility();
        if (!_userHidden) RefreshNow(true);
    }

    public void SetAlwaysOnTop(bool on) => TopMost = on;

    public void FullscreenGuardTick()
    {
        if (!AutoHideOnFullscreen)
        {
            if (_hiddenByFullscreen)
            {
                _hiddenByFullscreen = false;
                ApplyVisibility();
                RefreshNow(true);
            }
            return;
        }
        bool fs = FullscreenDetector.ForegroundIsFullscreen(Handle);
        if (fs != _hiddenByFullscreen)
        {
            _hiddenByFullscreen = fs;
            ApplyVisibility();
            if (!_hiddenByFullscreen) RefreshNow(true);
        }
    }

    void ApplyVisibility()
    {
        bool shouldShow = !_userHidden && !_hiddenByFullscreen;
        if (shouldShow)
        {
            if (!Visible) Show();
            if (_anchorEdge)
            {
                _appBar ??= new AppBar(Handle);
                _appBar.EnsureRegistered(BarHeight, _edge);
            }
        }
        else
        {
            if (_anchorEdge && _appBar != null)
            {
                _appBar.Unregister();
            }
            if (Visible) Hide();
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
        _appBar?.Unregister();
        base.OnHandleDestroyed(e);
    }

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
