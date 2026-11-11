using System.Drawing.Drawing2D;

namespace MPBackend.Tray;

/// <summary>Tray icons drawn at runtime: a coloured dot on the app glyph for each server state.</summary>
public sealed class TrayIcons
{
    public TrayIcons()
    {
        Stopped = Make(Color.FromArgb(130, 130, 130));
        Running = Make(Color.FromArgb(40, 120, 230));
        Vehicle = Make(Color.FromArgb(40, 170, 80));
        Error = Make(Color.FromArgb(210, 50, 50));
    }

    /// <summary>API not running.</summary>
    public Icon Stopped { get; }

    /// <summary>API running, no vehicle.</summary>
    public Icon Running { get; }

    /// <summary>At least one vehicle connected.</summary>
    public Icon Vehicle { get; }

    /// <summary>Host failed or a link reported an error.</summary>
    public Icon Error { get; }

    private static Icon Make(Color dot)
    {
        // 32 px: Windows scales it for the notification area
        using var bmp = new Bitmap(32, 32);
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Color.Transparent);
            using var back = new SolidBrush(Color.FromArgb(28, 36, 52));
            using var path = RoundedRect(new Rectangle(1, 1, 30, 30), 7);
            g.FillPath(back, path);

            // three signal arcs, white
            using var pen = new Pen(Color.White, 2.5f) { StartCap = LineCap.Round, EndCap = LineCap.Round };
            for (var i = 0; i < 3; i++)
            {
                var r = 7 + i * 6;
                g.DrawArc(pen, 6 - r / 2f + 5, 24 - r / 2f, r, r, 225, 90);
            }

            using var dotBrush = new SolidBrush(dot);
            g.FillEllipse(dotBrush, 18, 18, 11, 11);
            using var ring = new Pen(Color.FromArgb(28, 36, 52), 1.5f);
            g.DrawEllipse(ring, 18, 18, 11, 11);
        }

        // the HICON stays alive for the process; four of them is fine
        return Icon.FromHandle(bmp.GetHicon());
    }

    private static GraphicsPath RoundedRect(Rectangle r, int radius)
    {
        var d = radius * 2;
        var path = new GraphicsPath();
        path.AddArc(r.X, r.Y, d, d, 180, 90);
        path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }
}
