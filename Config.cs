namespace I3wsb;

internal enum DockView { Icons, Bar }
internal enum SuperKey { Win, Alt }

/// <summary>Tiny ini-style settings file at %LOCALAPPDATA%\i3wsb\config.ini.</summary>
internal sealed class Config
{
    public DockView View = DockView.Bar;
    public bool AlwaysOnTop = true;
    public bool AutoHide = true;
    public bool AnchorTop = true;                 // full-width strip pinned to the top edge, i3-style
    public SuperKey Super = SuperKey.Win;         // modifier for desktop-switch / move hotkeys

    static string Path => System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "i3wsb", "config.ini");

    public static Config Load()
    {
        var c = new Config();
        try
        {
            foreach (var raw in File.ReadAllLines(Path))
            {
                var line = raw.Trim();
                int eq = line.IndexOf('=');
                if (eq <= 0) continue;
                string k = line[..eq].Trim().ToLowerInvariant();
                string v = line[(eq + 1)..].Trim();
                switch (k)
                {
                    case "view": c.View = v.Equals("bar", StringComparison.OrdinalIgnoreCase) ? DockView.Bar : DockView.Icons; break;
                    case "alwaysontop": c.AlwaysOnTop = ParseBool(v); break;
                    case "autohide": c.AutoHide = ParseBool(v); break;
                    case "anchor": c.AnchorTop = !v.Equals("float", StringComparison.OrdinalIgnoreCase); break;
                    case "super": c.Super = v.Equals("alt", StringComparison.OrdinalIgnoreCase) ? SuperKey.Alt : SuperKey.Win; break;
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
                $"anchor={(AnchorTop ? "top" : "float")}",
                $"super={(Super == SuperKey.Alt ? "alt" : "win")}",
                $"alwaysOnTop={AlwaysOnTop}",
                $"autoHide={AutoHide}",
            });
        }
        catch { /* best effort */ }
    }

    static bool ParseBool(string v) =>
        v is "1" or "true" or "True" or "yes" or "on";
}
