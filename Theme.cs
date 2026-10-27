using System.Drawing;

namespace I3wsb;

internal sealed class Theme
{
    public required string Name { get; init; }
    public required string DisplayName { get; init; }

    public required Color BarBg { get; init; }
    public required Color PillBg { get; init; }
    public required Color PillBorder { get; init; }
    public required Color PillHoverBg { get; init; }

    public required Color FocusedPillBg { get; init; }
    public required Color FocusedPillBorder { get; init; }
    public required Color FocusedPillFg { get; init; }

    public required Color OccupiedPillFg { get; init; }
    public required Color EmptyPillFg { get; init; }

    public required Color WidgetBg { get; init; }
    public required Color WidgetFg { get; init; }
    public required Color WidgetBorder { get; init; }
    public required Color WidgetWarningFg { get; init; }
    public required Color WidgetCriticalFg { get; init; }

    public required Color ActiveWindowBg { get; init; }
    public required Color ActiveWindowFg { get; init; }
    public required Color ActiveWindowBorder { get; init; }

    public required Color ClockFg { get; init; }
    public required Color SeparatorColor { get; init; }

    public static readonly Theme Dark = new()
    {
        Name = "dark",
        DisplayName = "Modern Dark",
        BarBg = Color.FromArgb(20, 20, 24),
        PillBg = Color.FromArgb(32, 34, 40),
        PillBorder = Color.FromArgb(48, 52, 62),
        PillHoverBg = Color.FromArgb(44, 48, 58),
        FocusedPillBg = Color.FromArgb(41, 104, 219),
        FocusedPillBorder = Color.FromArgb(88, 146, 255),
        FocusedPillFg = Color.White,
        OccupiedPillFg = Color.FromArgb(220, 224, 232),
        EmptyPillFg = Color.FromArgb(120, 126, 140),
        WidgetBg = Color.FromArgb(28, 30, 36),
        WidgetFg = Color.FromArgb(200, 206, 218),
        WidgetBorder = Color.FromArgb(44, 48, 56),
        WidgetWarningFg = Color.FromArgb(250, 180, 50),
        WidgetCriticalFg = Color.FromArgb(245, 90, 80),
        ActiveWindowBg = Color.FromArgb(26, 28, 34),
        ActiveWindowFg = Color.FromArgb(210, 215, 225),
        ActiveWindowBorder = Color.FromArgb(42, 46, 54),
        ClockFg = Color.FromArgb(235, 238, 245),
        SeparatorColor = Color.FromArgb(45, 48, 58),
    };

    public static readonly Theme Catppuccin = new()
    {
        Name = "catppuccin",
        DisplayName = "Catppuccin Mocha",
        BarBg = Color.FromArgb(30, 30, 46),            // #1E1E2E
        PillBg = Color.FromArgb(49, 50, 68),           // #313244
        PillBorder = Color.FromArgb(69, 71, 90),        // #45475A
        PillHoverBg = Color.FromArgb(60, 62, 84),
        FocusedPillBg = Color.FromArgb(137, 180, 250),  // #89B4FA (Blue)
        FocusedPillBorder = Color.FromArgb(180, 190, 254),
        FocusedPillFg = Color.FromArgb(17, 17, 27),     // #11111B (Crust)
        OccupiedPillFg = Color.FromArgb(205, 214, 244),// #CDD6F4 (Text)
        EmptyPillFg = Color.FromArgb(147, 153, 178),   // #9399B2 (Overlay2)
        WidgetBg = Color.FromArgb(36, 39, 58),
        WidgetFg = Color.FromArgb(205, 214, 244),
        WidgetBorder = Color.FromArgb(58, 62, 86),
        WidgetWarningFg = Color.FromArgb(249, 226, 175),// #F9E2AF (Yellow)
        WidgetCriticalFg = Color.FromArgb(243, 139, 168),// #F38BA8 (Red)
        ActiveWindowBg = Color.FromArgb(36, 39, 58),
        ActiveWindowFg = Color.FromArgb(186, 194, 222),
        ActiveWindowBorder = Color.FromArgb(58, 62, 86),
        ClockFg = Color.FromArgb(245, 224, 220),       // #F5E0DC (Rosewater)
        SeparatorColor = Color.FromArgb(58, 62, 86),
    };

    public static readonly Theme Nord = new()
    {
        Name = "nord",
        DisplayName = "Nord",
        BarBg = Color.FromArgb(46, 52, 64),            // #2E3440
        PillBg = Color.FromArgb(59, 66, 82),           // #3B4252
        PillBorder = Color.FromArgb(76, 86, 106),       // #4C566A
        PillHoverBg = Color.FromArgb(67, 76, 94),
        FocusedPillBg = Color.FromArgb(136, 192, 208), // #88C0D0 (Frost)
        FocusedPillBorder = Color.FromArgb(143, 188, 187),
        FocusedPillFg = Color.FromArgb(46, 52, 64),
        OccupiedPillFg = Color.FromArgb(236, 239, 244),// #ECEFF4
        EmptyPillFg = Color.FromArgb(160, 170, 185),
        WidgetBg = Color.FromArgb(53, 60, 74),
        WidgetFg = Color.FromArgb(229, 233, 240),
        WidgetBorder = Color.FromArgb(67, 76, 94),
        WidgetWarningFg = Color.FromArgb(235, 203, 139),// #EBCB8B
        WidgetCriticalFg = Color.FromArgb(191, 97, 106),// #BF616A
        ActiveWindowBg = Color.FromArgb(53, 60, 74),
        ActiveWindowFg = Color.FromArgb(216, 222, 233),
        ActiveWindowBorder = Color.FromArgb(67, 76, 94),
        ClockFg = Color.FromArgb(236, 239, 244),
        SeparatorColor = Color.FromArgb(67, 76, 94),
    };

    public static readonly Theme TokyoNight = new()
    {
        Name = "tokyonight",
        DisplayName = "Tokyo Night",
        BarBg = Color.FromArgb(26, 27, 38),            // #1A1B26
        PillBg = Color.FromArgb(36, 40, 59),           // #24283B
        PillBorder = Color.FromArgb(48, 54, 78),
        PillHoverBg = Color.FromArgb(44, 49, 74),
        FocusedPillBg = Color.FromArgb(122, 162, 247), // #7AA2F7
        FocusedPillBorder = Color.FromArgb(157, 124, 216),
        FocusedPillFg = Color.FromArgb(26, 27, 38),
        OccupiedPillFg = Color.FromArgb(192, 202, 245),
        EmptyPillFg = Color.FromArgb(115, 126, 165),
        WidgetBg = Color.FromArgb(31, 35, 53),
        WidgetFg = Color.FromArgb(192, 202, 245),
        WidgetBorder = Color.FromArgb(48, 54, 78),
        WidgetWarningFg = Color.FromArgb(224, 175, 104),
        WidgetCriticalFg = Color.FromArgb(247, 118, 142),
        ActiveWindowBg = Color.FromArgb(31, 35, 53),
        ActiveWindowFg = Color.FromArgb(180, 190, 230),
        ActiveWindowBorder = Color.FromArgb(48, 54, 78),
        ClockFg = Color.FromArgb(192, 202, 245),
        SeparatorColor = Color.FromArgb(48, 54, 78),
    };

    public static Theme FromName(string? name) => name?.ToLowerInvariant() switch
    {
        "catppuccin" or "mocha" => Catppuccin,
        "nord" => Nord,
        "tokyonight" or "tokyo" => TokyoNight,
        _ => Dark,
    };
}
