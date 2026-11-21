using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace I3wsb;

internal static class TrayIconGenerator
{
    public static Icon CreateAppIcon()
    {
        try
        {
            string exe = Environment.ProcessPath ?? "";
            if (!string.IsNullOrEmpty(exe) && File.Exists(exe))
            {
                var assoc = Icon.ExtractAssociatedIcon(exe);
                if (assoc != null) return assoc;
            }
        }
        catch { }

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

public class IcoGenerator
{
    public static void GenerateIcon(string outputPath)
    {
        int[] sizes = new int[] { 16, 24, 32, 48, 64, 128, 256 };
        var pngDataList = new List<byte[]>();

        foreach (int size in sizes)
        {
            using (var bmp = new Bitmap(size, size, PixelFormat.Format32bppArgb))
            {
                using (var g = Graphics.FromImage(bmp))
                {
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                    g.Clear(Color.Transparent);

                    float scale = size / 32f;
                    float pad = 1f * scale;
                    float arc = 8f * scale;
                    float cardSize = size - (2f * pad);

                    using (var path = new GraphicsPath())
                    {
                        path.AddArc(pad, pad, arc, arc, 180, 90);
                        path.AddArc(pad + cardSize - arc, pad, arc, arc, 270, 90);
                        path.AddArc(pad + cardSize - arc, pad + cardSize - arc, arc, arc, 0, 90);
                        path.AddArc(pad, pad + cardSize - arc, arc, arc, 90, 90);
                        path.CloseFigure();

                        using (var bgBrush = new SolidBrush(Color.FromArgb(28, 30, 38)))
                        {
                            g.FillPath(bgBrush, path);
                        }

                        using (var borderPen = new Pen(Color.FromArgb(65, 75, 95), Math.Max(1f, 1.5f * scale)))
                        {
                            g.DrawPath(borderPen, path);
                        }
                    }

                    // 2x2 grid representing virtual desktops with desktop 1 active in accent
                    float sq = 8f * scale;
                    float p1 = 6f * scale;
                    float p2 = 18f * scale;
                    float cornerRadius = 1.5f * scale;

                    using (var activeBrush = new SolidBrush(Color.FromArgb(64, 158, 255)))
                    using (var inactiveBrush = new SolidBrush(Color.FromArgb(85, 95, 115)))
                    {
                        DrawRoundedSquare(g, activeBrush, p1, p1, sq, cornerRadius);
                        DrawRoundedSquare(g, inactiveBrush, p2, p1, sq, cornerRadius);
                        DrawRoundedSquare(g, inactiveBrush, p1, p2, sq, cornerRadius);
                        DrawRoundedSquare(g, inactiveBrush, p2, p2, sq, cornerRadius);
                    }
                }

                using (var ms = new MemoryStream())
                {
                    bmp.Save(ms, ImageFormat.Png);
                    pngDataList.Add(ms.ToArray());
                }
            }
        }

        using (var fs = new FileStream(outputPath, FileMode.Create, FileAccess.Write))
        using (var bw = new BinaryWriter(fs))
        {
            // ICONDIR
            bw.Write((short)0); // reserved
            bw.Write((short)1); // icon type (1 = icon)
            bw.Write((short)sizes.Length); // count

            int offset = 6 + (16 * sizes.Length);
            for (int i = 0; i < sizes.Length; i++)
            {
                int size = sizes[i];
                byte[] data = pngDataList[i];

                bw.Write((byte)(size >= 256 ? 0 : size)); // width
                bw.Write((byte)(size >= 256 ? 0 : size)); // height
                bw.Write((byte)0); // color count
                bw.Write((byte)0); // reserved
                bw.Write((short)1); // color planes
                bw.Write((short)32); // bits per pixel
                bw.Write(data.Length); // size of image data
                bw.Write(offset); // offset

                offset += data.Length;
            }

            for (int i = 0; i < sizes.Length; i++)
            {
                bw.Write(pngDataList[i]);
            }
        }
    }

    static void DrawRoundedSquare(Graphics g, Brush brush, float x, float y, float s, float r)
    {
        if (r < 1f)
        {
            g.FillRectangle(brush, x, y, s, s);
            return;
        }
        using (var p = new GraphicsPath())
        {
            float d = r * 2f;
            p.AddArc(x, y, d, d, 180, 90);
            p.AddArc(x + s - d, y, d, d, 270, 90);
            p.AddArc(x + s - d, y + s - d, d, d, 0, 90);
            p.AddArc(x, y + s - d, d, d, 90, 90);
            p.CloseFigure();
            g.FillPath(brush, p);
        }
    }
}