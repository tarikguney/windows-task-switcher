using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;

namespace WindowTaskSwitcher.Services;

public static class IconService
{
    /// <summary>
    /// Creates a tray icon programmatically — a minimal 4-window grid symbol.
    /// </summary>
    public static Icon CreateTrayIcon()
    {
        const int size = 32;
        using var bitmap = new Bitmap(size, size, PixelFormat.Format32bppArgb);
        using var g = Graphics.FromImage(bitmap);

        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(Color.Transparent);

        // Draw a 2x2 grid of rounded rectangles (window grid icon)
        int padding = 2;
        int gap = 3;
        int cellW = (size - 2 * padding - gap) / 2;
        int cellH = (size - 2 * padding - gap) / 2;

        using var brush = new SolidBrush(Color.FromArgb(230, 79, 193, 255)); // #4FC1FF accent

        var cells = new[]
        {
            new Rectangle(padding, padding, cellW, cellH),
            new Rectangle(padding + cellW + gap, padding, cellW, cellH),
            new Rectangle(padding, padding + cellH + gap, cellW, cellH),
            new Rectangle(padding + cellW + gap, padding + cellH + gap, cellW, cellH),
        };

        foreach (var cell in cells)
        {
            using var path = RoundedRect(cell, 3);
            g.FillPath(brush, path);
        }

        return Icon.FromHandle(bitmap.GetHicon());
    }

    private static GraphicsPath RoundedRect(Rectangle bounds, int radius)
    {
        int d = radius * 2;
        var path = new GraphicsPath();
        path.AddArc(bounds.X, bounds.Y, d, d, 180, 90);
        path.AddArc(bounds.Right - d, bounds.Y, d, d, 270, 90);
        path.AddArc(bounds.Right - d, bounds.Bottom - d, d, d, 0, 90);
        path.AddArc(bounds.X, bounds.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }
}
