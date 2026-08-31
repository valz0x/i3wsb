namespace I3wsb;

/// <summary>Short, lowercase app identifiers for the workspace-bar view
/// ("brave", "code", "term") — matches the AutoHotkey script's scheme.</summary>
internal static class AppNames
{
    const int MaxLen = 16;

    static readonly Dictionary<string, string> Overrides = new(StringComparer.OrdinalIgnoreCase)
    {
        ["msedge"] = "edge",
        ["Code"] = "code",
        ["WindowsTerminal"] = "term",
        ["wt"] = "term",
        ["explorer"] = "files",
        ["devenv"] = "vs",
        ["idea64"] = "idea",
        ["pycharm64"] = "pycharm",
        ["totalcmd64"] = "tc",
        ["totalcmd"] = "tc",
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

    /// <summary>Deduplicated, hyphen-joined names for a desktop's windows.</summary>
    public static string Label(IEnumerable<AppWindow> windows)
    {
        var seen = new HashSet<string>();
        var parts = new List<string>();
        foreach (var w in windows)
        {
            string n = Short(w.ExePath, w.Title);
            if (seen.Add(n)) parts.Add(n);
        }
        return string.Join("-", parts);
    }

    static string FirstWord(string s)
    {
        s = s.Trim();
        int sp = s.IndexOf(' ');
        return sp < 0 ? s : s[..sp];
    }
}
