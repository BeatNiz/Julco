using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using Julco.Core.Configuration;

namespace Julco.UI;

public static class ScreenshotRedactionService
{
    public static string? CreateRedactedScreenshot(
        string sourcePath,
        string destinationPath,
        PrivacySettings settings)
    {
        if (string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath))
        {
            return null;
        }

        settings = settings.Normalized();
        using var source = new Bitmap(sourcePath);
        using var redacted = new Bitmap(source.Width, source.Height);
        using var graphics = Graphics.FromImage(redacted);
        graphics.DrawImage(source, 0, 0, source.Width, source.Height);

        if (settings.BlurScreenshotsInSafeExports)
        {
            DrawBlurred(graphics, source, new Rectangle(0, 0, source.Width, source.Height));
        }

        foreach (var box in ParseBoxes(settings.ScreenshotRedactionBoxes, source.Width, source.Height))
        {
            DrawRedactionBox(graphics, box);
        }

        Directory.CreateDirectory(Path.GetDirectoryName(destinationPath) ?? ".");
        redacted.Save(destinationPath, ImageFormat.Png);
        return destinationPath;
    }

    public static IReadOnlyList<Rectangle> ParseBoxes(string? value, int width, int height)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return Array.Empty<Rectangle>();
        }

        var boxes = new List<Rectangle>();
        foreach (var line in value.Split(["\r\n", "\n"], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var parts = line.Split(',', StringSplitOptions.TrimEntries);
            if (parts.Length != 4
                || !int.TryParse(parts[0], out var x)
                || !int.TryParse(parts[1], out var y)
                || !int.TryParse(parts[2], out var boxWidth)
                || !int.TryParse(parts[3], out var boxHeight))
            {
                continue;
            }

            var rectangle = Rectangle.Intersect(
                new Rectangle(0, 0, width, height),
                new Rectangle(x, y, Math.Max(1, boxWidth), Math.Max(1, boxHeight)));
            if (!rectangle.IsEmpty)
            {
                boxes.Add(rectangle);
            }
        }

        return boxes;
    }

    private static void DrawBlurred(Graphics graphics, Bitmap source, Rectangle area)
    {
        using var tiny = new Bitmap(Math.Max(1, area.Width / 18), Math.Max(1, area.Height / 18));
        using (var tinyGraphics = Graphics.FromImage(tiny))
        {
            tinyGraphics.InterpolationMode = InterpolationMode.Low;
            tinyGraphics.DrawImage(source, new Rectangle(0, 0, tiny.Width, tiny.Height), area, GraphicsUnit.Pixel);
        }

        graphics.InterpolationMode = InterpolationMode.NearestNeighbor;
        graphics.DrawImage(tiny, area);
        using var overlay = new SolidBrush(Color.FromArgb(72, 5, 7, 10));
        graphics.FillRectangle(overlay, area);
    }

    private static void DrawRedactionBox(Graphics graphics, Rectangle box)
    {
        using var brush = new SolidBrush(Color.FromArgb(235, 0, 0, 0));
        graphics.FillRectangle(brush, box);
        using var pen = new Pen(Color.FromArgb(255, 64, 199, 255), 2);
        graphics.DrawRectangle(pen, box);
    }
}
