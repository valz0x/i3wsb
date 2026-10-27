namespace I3wsb;

/// <summary>Short, lowercase app identifiers for the workspace-bar view
/// ("edge", "code", "term") — clean and readable.</summary>
internal static class AppNames
{
    const int MaxLen = 14;

    static readonly Dictionary<string, string> Overrides = new(StringComparer.OrdinalIgnoreCase)
    {
        ["msedge"] = "edge",
        ["Code"] = "code",
        ["WindowsTerminal"] = "term",
        ["wt"] = "term",
        ["powershell"] = "pwsh",
        ["pwsh"] = "pwsh",
        ["cmd"] = "cmd",
        ["explorer"] = "files",
        ["devenv"] = "vs",
        ["idea64"] = "idea",
        ["pycharm64"] = "pycharm",
        ["clion64"] = "clion",
        ["rider64"] = "rider",
        ["webstorm64"] = "webstorm",
        ["totalcmd64"] = "tc",
        ["totalcmd"] = "tc",
        ["chrome"] = "chrome",
        ["firefox"] = "firefox",
        ["brave"] = "brave",
        ["slack"] = "slack",
        ["discord"] = "discord",
        ["spotify"] = "spotify",
        ["notepad"] = "notes",
        ["sublime_text"] = "sublime",
        ["obsidian"] = "obsidian",
        ["telegram"] = "tg",
    };

    public static string Short(string exePath, string title)
    {
        string name = "";
        if (!string.IsNullOrEmpty(exePath))
        {
            string bas = Path.GetFileNameWithoutExtension(exePath);
            if (bas.Equals("ApplicationFrameHost", StringComparison.OrdinalIgnoreCase))
                name = FirstWord(title);                       // UWP host: real name is the title
            else if (Overrides.TryGetValue(bas, out var o))
                name = o;
            else
                name = bas;
        }
        if (name.Length == 0) name = FirstWord(title);
        if (name.Length == 0) name = "?";

        name = name.ToLowerInvariant();
        return name.Length > MaxLen ? name[..MaxLen] : name;
    }

    /// <summary>Deduplicated, space-bullet joined names for a desktop's windows.</summary>
    public static string Label(IEnumerable<AppWindow> windows)
    {
        var seen = new HashSet<string>();
        var parts = new List<string>();
        foreach (var w in windows)
        {
            string n = Short(w.ExePath, w.Title);
            if (seen.Add(n)) parts.Add(n);
        }
        return string.Join(" ", parts);
    }

    static string FirstWord(string s)
    {
        s = s.Trim();
        int sp = s.IndexOf(' ');
        return sp < 0 ? s : s[..sp];
    }
}
