namespace I3wsb;

internal enum DockView { Icons, Bar }
internal enum SuperKey { Win, Alt }

/// <summary>Tiny ini-style settings file at %LOCALAPPDATA%\i3wsb\config.ini.</summary>
internal sealed class Config
{
    public DockView View = DockView.Bar;
    public bool AlwaysOnTop = true;
    public bool AutoHide = true;
    public bool AnchorEdge = true;                // docked as an AppBar strip
    public BarEdge Edge = BarEdge.Top;            // Top or Bottom
    public SuperKey Super = SuperKey.Win;         // modifier for desktop-switch / move hotkeys
    public string Theme = "dark";

    public bool ShowCpu = true;
    public bool ShowRam = true;
    public bool ShowVolume = true;
    public bool ShowBattery = true;
    public bool ShowOpenApps = true;
    public bool ShowClock = true;
    public bool AutoPrune = true;
    public bool DisableTaskbarFlashing = false;

    static string Path => System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "i3wsb", "config.ini");

    public static Config Load()
    {
        var c = new Config();
        try
        {
            if (!File.Exists(Path)) return c;
            foreach (var raw in File.ReadAllLines(Path))
            {
                var line = raw.Trim();
                int eq = line.IndexOf('=');
                if (eq <= 0) continue;
                string k = line[..eq].Trim().ToLowerInvariant();
                string v = line[(eq + 1)..].Trim();
                switch (k)
                {
                    case "view": c.View = v.Equals("icons", StringComparison.OrdinalIgnoreCase) ? DockView.Icons : DockView.Bar; break;
                    case "alwaysontop": c.AlwaysOnTop = ParseBool(v); break;
                    case "autohide": c.AutoHide = ParseBool(v); break;
                    case "anchor":
                        c.AnchorEdge = !v.Equals("float", StringComparison.OrdinalIgnoreCase);
                        if (v.Equals("bottom", StringComparison.OrdinalIgnoreCase)) c.Edge = BarEdge.Bottom;
                        else if (v.Equals("top", StringComparison.OrdinalIgnoreCase)) c.Edge = BarEdge.Top;
                        break;
                    case "edge": c.Edge = v.Equals("bottom", StringComparison.OrdinalIgnoreCase) ? BarEdge.Bottom : BarEdge.Top; break;
                    case "super": c.Super = v.Equals("alt", StringComparison.OrdinalIgnoreCase) ? SuperKey.Alt : SuperKey.Win; break;
                    case "theme": c.Theme = v.ToLowerInvariant(); break;
                    case "showcpu": c.ShowCpu = ParseBool(v); break;
                    case "showram": c.ShowRam = ParseBool(v); break;
                    case "showvolume": c.ShowVolume = ParseBool(v); break;
                    case "showbattery": c.ShowBattery = ParseBool(v); break;
                    case "showopenapps":
                    case "showactivewindow": c.ShowOpenApps = ParseBool(v); break;
                    case "showclock": c.ShowClock = ParseBool(v); break;
                    case "autoprune": c.AutoPrune = ParseBool(v); break;
                    case "disabletaskbarflashing": c.DisableTaskbarFlashing = ParseBool(v); break;
                }
            }
        }
        catch { /* first run / unreadable: defaults */ }
        return c;
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path)!);
            File.WriteAllLines(Path, new[]
            {
                $"view={(View == DockView.Bar ? "bar" : "icons")}",
                $"anchor={(AnchorEdge ? (Edge == BarEdge.Bottom ? "bottom" : "top") : "float")}",
                $"edge={(Edge == BarEdge.Bottom ? "bottom" : "top")}",
                $"super={(Super == SuperKey.Alt ? "alt" : "win")}",
                $"theme={Theme}",
                $"alwaysOnTop={AlwaysOnTop}",
                $"autoHide={AutoHide}",
                $"autoPrune={AutoPrune}",
                $"disableTaskbarFlashing={DisableTaskbarFlashing}",
                $"showCpu={ShowCpu}",
                $"showRam={ShowRam}",
                $"showVolume={ShowVolume}",
                $"showBattery={ShowBattery}",
                $"showOpenApps={ShowOpenApps}",
                $"showClock={ShowClock}",
            });
        }
        catch { /* best effort */ }
    }

    static bool ParseBool(string v) =>
        v is "1" or "true" or "True" or "yes" or "on";
}
