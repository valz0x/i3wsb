using System.Drawing;
using System.Drawing.Drawing2D;

namespace I3wsb;

internal static class TrayIconGenerator
{
    public static Icon CreateAppIcon()
    {
        using var bmp = new Bitmap(32, 32);
        using var g = Graphics.FromImage(bmp);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(Color.Transparent);

        using var bgBrush = new SolidBrush(Color.FromArgb(28, 30, 38));
        using var path = new GraphicsPath();
        path.AddArc(1, 1, 8, 8, 180, 90);
        path.AddArc(23, 1, 8, 8, 270, 90);
        path.AddArc(23, 23, 8, 8, 0, 90);
        path.AddArc(1, 23, 8, 8, 90, 90);
        path.CloseFigure();
        g.FillPath(bgBrush, path);

        using var borderPen = new Pen(Color.FromArgb(65, 75, 95), 1.5f);
        g.DrawPath(borderPen, path);

        // 2x2 grid representing virtual desktops with desktop 1 active in accent
        using var activeBrush = new SolidBrush(Color.FromArgb(64, 158, 255));
        using var inactiveBrush = new SolidBrush(Color.FromArgb(85, 95, 115));

        g.FillRectangle(activeBrush, 6, 6, 8, 8);
        g.FillRectangle(inactiveBrush, 18, 6, 8, 8);
        g.FillRectangle(inactiveBrush, 6, 18, 8, 8);
        g.FillRectangle(inactiveBrush, 18, 18, 8, 8);

        IntPtr hIcon = bmp.GetHicon();
        var icon = (Icon)Icon.FromHandle(hIcon).Clone();
        Native.DestroyIcon(hIcon);
        return icon;
    }
}
