using System.Globalization;
using System.IO;
using System.Text;

namespace Julco.UI;

public static class SimplePdfReportWriter
{
    public static void Write(string path, CaptureReport report)
    {
        var image = PdfImage.TryLoad(report.ScreenshotPath);
        var lines = report.BuildPdfLines();
        var pages = lines.Chunk(42).ToArray();
        if (pages.Length == 0)
        {
            pages = new[] { Array.Empty<string>() };
        }

        var hasImage = image is not null;
        var pageCount = pages.Length;
        var fontId = 3;
        var imageId = hasImage ? 4 : 0;
        var firstPageId = hasImage ? 5 : 4;
        var objectCount = firstPageId + (pageCount * 2) - 1;
        var pageIds = Enumerable.Range(0, pageCount)
            .Select(index => firstPageId + (index * 2))
            .ToArray();

        var objects = new byte[objectCount + 1][];
        objects[1] = Ascii("<< /Type /Catalog /Pages 2 0 R >>");
        objects[2] = Ascii($"<< /Type /Pages /Kids [{string.Join(" ", pageIds.Select(id => $"{id} 0 R"))}] /Count {pageCount} >>");
        objects[3] = Ascii("<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>");

        if (hasImage)
        {
            objects[imageId] = BuildImageObject(image!);
        }

        for (var index = 0; index < pageCount; index++)
        {
            var pageId = pageIds[index];
            var contentId = pageId + 1;
            var resources = hasImage
                ? $"<< /Font << /F1 {fontId} 0 R >> /XObject << /Im1 {imageId} 0 R >> >>"
                : $"<< /Font << /F1 {fontId} 0 R >> >>";
            objects[pageId] = Ascii($"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Resources {resources} /Contents {contentId} 0 R >>");
            objects[contentId] = BuildContentObject(pages[index], image, index == 0);
        }

        using var stream = File.Create(path);
        WriteAscii(stream, "%PDF-1.4\n%Julco\n");
        var offsets = new long[objects.Length];
        for (var id = 1; id < objects.Length; id++)
        {
            offsets[id] = stream.Position;
            WriteAscii(stream, $"{id} 0 obj\n");
            stream.Write(objects[id]);
            WriteAscii(stream, "\nendobj\n");
        }

        var xref = stream.Position;
        WriteAscii(stream, $"xref\n0 {objects.Length}\n0000000000 65535 f \n");
        for (var id = 1; id < objects.Length; id++)
        {
            WriteAscii(stream, $"{offsets[id]:0000000000} 00000 n \n");
        }

        WriteAscii(stream, $"trailer\n<< /Size {objects.Length} /Root 1 0 R >>\nstartxref\n{xref}\n%%EOF");
    }

    private static byte[] BuildContentObject(string[] lines, PdfImage? image, bool includeImage)
    {
        using var content = new MemoryStream();
        if (includeImage && image is not null)
        {
            var maxWidth = 470.0;
            var maxHeight = 245.0;
            var scale = Math.Min(maxWidth / image.Width, maxHeight / image.Height);
            var width = image.Width * scale;
            var height = image.Height * scale;
            var x = (612 - width) / 2;
            var y = 792 - height - 42;
            WriteAscii(content, FormattableString.Invariant($"q {width:0.##} 0 0 {height:0.##} {x:0.##} {y:0.##} cm /Im1 Do Q\n"));
        }

        var startY = includeImage && image is not null ? 480 : 742;
        WriteAscii(content, "BT /F1 10 Tf 46 ");
        WriteAscii(content, startY.ToString(CultureInfo.InvariantCulture));
        WriteAscii(content, " Td 14 TL\n");
        foreach (var line in lines)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                WriteAscii(content, "T*\n");
                continue;
            }

            WriteAscii(content, $"{ToPdfHexString(line)} Tj T*\n");
        }

        WriteAscii(content, "ET\n");
        return WrapStream(content.ToArray());
    }

    private static byte[] BuildImageObject(PdfImage image)
    {
        var header = Ascii($"<< /Type /XObject /Subtype /Image /Width {image.Width} /Height {image.Height} /ColorSpace /DeviceRGB /BitsPerComponent 8 /Length {image.Rgb.Length} >>\nstream\n");
        var footer = Ascii("\nendstream");
        return Combine(header, image.Rgb, footer);
    }

    private static byte[] WrapStream(byte[] content)
    {
        return Combine(
            Ascii($"<< /Length {content.Length} >>\nstream\n"),
            content,
            Ascii("\nendstream"));
    }

    private static string ToPdfHexString(string value)
    {
        var bytes = Encoding.BigEndianUnicode.GetBytes(value);
        var builder = new StringBuilder("<FEFF", 4 + (bytes.Length * 2) + 1);
        foreach (var b in bytes)
        {
            builder.Append(b.ToString("X2", CultureInfo.InvariantCulture));
        }

        builder.Append('>');
        return builder.ToString();
    }

    private static byte[] Combine(params byte[][] parts)
    {
        var length = parts.Sum(part => part.Length);
        var result = new byte[length];
        var offset = 0;
        foreach (var part in parts)
        {
            Buffer.BlockCopy(part, 0, result, offset, part.Length);
            offset += part.Length;
        }

        return result;
    }

    private static byte[] Ascii(string value)
    {
        return Encoding.ASCII.GetBytes(value);
    }

    private static void WriteAscii(Stream stream, string value)
    {
        var bytes = Ascii(value);
        stream.Write(bytes);
    }

    private sealed record PdfImage(int Width, int Height, byte[] Rgb)
    {
        public static PdfImage? TryLoad(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                return null;
            }

            try
            {
                using var source = new System.Drawing.Bitmap(path);
                using var bitmap = new System.Drawing.Bitmap(source.Width, source.Height, System.Drawing.Imaging.PixelFormat.Format24bppRgb);
                using (var graphics = System.Drawing.Graphics.FromImage(bitmap))
                {
                    graphics.Clear(System.Drawing.Color.White);
                    graphics.DrawImage(source, 0, 0, source.Width, source.Height);
                }

                var bytes = new byte[bitmap.Width * bitmap.Height * 3];
                var offset = 0;
                for (var y = 0; y < bitmap.Height; y++)
                {
                    for (var x = 0; x < bitmap.Width; x++)
                    {
                        var pixel = bitmap.GetPixel(x, y);
                        bytes[offset++] = pixel.R;
                        bytes[offset++] = pixel.G;
                        bytes[offset++] = pixel.B;
                    }
                }

                return new PdfImage(bitmap.Width, bitmap.Height, bytes);
            }
            catch
            {
                return null;
            }
        }
    }
}
