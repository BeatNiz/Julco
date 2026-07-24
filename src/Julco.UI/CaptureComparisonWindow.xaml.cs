using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;

namespace Julco.UI;

public partial class CaptureComparisonWindow : Window
{
    private readonly IReadOnlyList<CaptureSnapshot> _captures;
    private string _currentReport = string.Empty;

    public CaptureComparisonWindow(IEnumerable<string> captureDirectories, string? selectedDirectory = null)
    {
        InitializeComponent();

        _captures = captureDirectories
            .Where(Directory.Exists)
            .Select(CaptureSnapshot.FromDirectory)
            .OrderByDescending(item => item.CreatedAt)
            .ToArray();

        CaptureAComboBox.ItemsSource = _captures;
        CaptureBComboBox.ItemsSource = _captures;

        if (_captures.Count > 0)
        {
            CaptureAComboBox.SelectedItem = _captures.FirstOrDefault(item =>
                string.Equals(item.DirectoryPath, selectedDirectory, StringComparison.OrdinalIgnoreCase))
                ?? _captures[0];
        }

        if (_captures.Count > 1)
        {
            CaptureBComboBox.SelectedItem = _captures.FirstOrDefault(item =>
                !ReferenceEquals(item, CaptureAComboBox.SelectedItem));
        }

        UpdateComparison();
    }

    private void CaptureComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        UpdateComparison();
    }

    private void UpdateComparison()
    {
        if (CaptureAComboBox.SelectedItem is not CaptureSnapshot captureA
            || CaptureBComboBox.SelectedItem is not CaptureSnapshot captureB)
        {
            ClearComparison("Choose two captures to compare.");
            return;
        }

        if (string.Equals(captureA.DirectoryPath, captureB.DirectoryPath, StringComparison.OrdinalIgnoreCase))
        {
            ClearComparison("Choose two different captures.");
            return;
        }

        ImageA.Source = LoadBitmapImage(captureA.ScreenshotPath);
        ImageB.Source = LoadBitmapImage(captureB.ScreenshotPath);
        var visual = CompareImages(captureA.ScreenshotPath, captureB.ScreenshotPath);
        DiffImage.Source = visual.DiffImage;

        var comparison = CaptureComparison.Create(captureA, captureB, visual);
        TechnicalTextBox.Text = comparison.TechnicalText;
        FilesTextBox.Text = comparison.FilesText;
        _currentReport = comparison.MarkdownReport;
        StatusTextBlock.Text = comparison.StatusText;
    }

    private void ClearComparison(string message)
    {
        ImageA.Source = null;
        ImageB.Source = null;
        DiffImage.Source = null;
        TechnicalTextBox.Text = string.Empty;
        FilesTextBox.Text = string.Empty;
        _currentReport = string.Empty;
        StatusTextBlock.Text = message;
    }

    private void SaveReportButton_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_currentReport))
        {
            StatusTextBlock.Text = "No comparison report to save.";
            return;
        }

        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Title = "Save comparison report",
            Filter = "Markdown report|*.md|Text file|*.txt",
            FileName = $"julco-comparison-{DateTime.Now:yyyyMMdd-HHmmss}.md"
        };

        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        File.WriteAllText(dialog.FileName, _currentReport);
        StatusTextBlock.Text = $"Comparison report saved: {dialog.FileName}";
    }

    private static BitmapImage? LoadBitmapImage(string path)
    {
        if (!File.Exists(path))
        {
            return null;
        }

        var bitmap = new BitmapImage();
        using var stream = File.OpenRead(path);
        bitmap.BeginInit();
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.StreamSource = stream;
        bitmap.EndInit();
        bitmap.Freeze();
        return bitmap;
    }

    private static VisualComparisonResult CompareImages(string pathA, string pathB)
    {
        if (!File.Exists(pathA) || !File.Exists(pathB))
        {
            return VisualComparisonResult.Missing;
        }

        using var bitmapA = new Bitmap(pathA);
        using var bitmapB = new Bitmap(pathB);
        var width = Math.Min(bitmapA.Width, bitmapB.Width);
        var height = Math.Min(bitmapA.Height, bitmapB.Height);
        if (width <= 0 || height <= 0)
        {
            return VisualComparisonResult.Missing;
        }

        using var diff = new Bitmap(width, height);
        long changedPixels = 0;
        double totalDelta = 0;

        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var a = bitmapA.GetPixel(x, y);
                var b = bitmapB.GetPixel(x, y);
                var delta = Math.Abs(a.R - b.R)
                    + Math.Abs(a.G - b.G)
                    + Math.Abs(a.B - b.B);
                totalDelta += delta / 3.0;

                if (delta > 24)
                {
                    changedPixels++;
                    diff.SetPixel(x, y, Color.FromArgb(255, 255, 72, 72));
                }
                else
                {
                    var gray = (byte)Math.Clamp((a.R + a.G + a.B) / 3, 0, 255);
                    diff.SetPixel(x, y, Color.FromArgb(255, gray, gray, gray));
                }
            }
        }

        using var stream = new MemoryStream();
        diff.Save(stream, ImageFormat.Png);
        stream.Position = 0;
        var diffImage = new BitmapImage();
        diffImage.BeginInit();
        diffImage.CacheOption = BitmapCacheOption.OnLoad;
        diffImage.StreamSource = stream;
        diffImage.EndInit();
        diffImage.Freeze();

        var comparedPixels = (long)width * height;
        var changedPercent = comparedPixels == 0
            ? 0
            : changedPixels * 100.0 / comparedPixels;
        var averageDelta = comparedPixels == 0
            ? 0
            : totalDelta / comparedPixels;

        return new VisualComparisonResult(
            true,
            diffImage,
            bitmapA.Width,
            bitmapA.Height,
            bitmapB.Width,
            bitmapB.Height,
            width,
            height,
            changedPixels,
            changedPercent,
            averageDelta);
    }

    private sealed record CaptureComparison(
        string TechnicalText,
        string FilesText,
        string MarkdownReport,
        string StatusText)
    {
        public static CaptureComparison Create(
            CaptureSnapshot captureA,
            CaptureSnapshot captureB,
            VisualComparisonResult visual)
        {
            var technical = BuildTechnicalText(captureA, captureB, visual);
            var files = BuildFilesText(captureA, captureB);
            var markdown = BuildMarkdownReport(captureA, captureB, visual, technical, files);
            var status = visual.IsAvailable
                ? $"Compared {visual.ComparedWidth}x{visual.ComparedHeight}px. Changed pixels: {visual.ChangedPercent:0.00}%."
                : "Technical comparison completed. Visual diff unavailable because one screenshot is missing.";
            return new CaptureComparison(technical, files, markdown, status);
        }

        private static string BuildTechnicalText(
            CaptureSnapshot a,
            CaptureSnapshot b,
            VisualComparisonResult visual)
        {
            var builder = new StringBuilder();
            AppendPair(builder, "Created", a.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss zzz"), b.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss zzz"));
            AppendPair(builder, "Browser", a.Browser, b.Browser);
            AppendPair(builder, "URL", a.Url, b.Url);
            AppendPair(builder, "Page title", a.PageTitle, b.PageTitle);
            AppendPair(builder, "Element", $"{a.TagName} {a.Selector}", $"{b.TagName} {b.Selector}");
            AppendPair(builder, "Frame", a.FrameText, b.FrameText);
            AppendPair(builder, "Console messages", a.ConsoleMessageCount.ToString(), b.ConsoleMessageCount.ToString());
            AppendPair(builder, "Image resources", a.ImageResourceCount.ToString(), b.ImageResourceCount.ToString());
            AppendPair(builder, "Notes", a.Notes.ShortSummary, b.Notes.ShortSummary);

            builder.AppendLine();
            builder.AppendLine("VISUAL");
            builder.AppendLine("------");
            if (visual.IsAvailable)
            {
                builder.AppendLine($"A screenshot: {visual.WidthA}x{visual.HeightA}");
                builder.AppendLine($"B screenshot: {visual.WidthB}x{visual.HeightB}");
                builder.AppendLine($"Compared area: {visual.ComparedWidth}x{visual.ComparedHeight}");
                builder.AppendLine($"Changed pixels: {visual.ChangedPixels:n0} ({visual.ChangedPercent:0.00}%)");
                builder.AppendLine($"Average color delta: {visual.AverageDelta:0.00}");
            }
            else
            {
                builder.AppendLine("Visual diff unavailable.");
            }

            return builder.ToString();
        }

        private static string BuildFilesText(CaptureSnapshot a, CaptureSnapshot b)
        {
            var builder = new StringBuilder();
            foreach (var fileName in CaptureSnapshot.KnownEvidenceFiles)
            {
                var hashA = a.Hashes.GetValueOrDefault(fileName, "-");
                var hashB = b.Hashes.GetValueOrDefault(fileName, "-");
                var state = hashA == hashB ? "same" : "different";
                builder.AppendLine($"{fileName,-24} {state,-10} A:{ShortHash(hashA),-14} B:{ShortHash(hashB),-14}");
            }

            return builder.ToString();
        }

        private static string BuildMarkdownReport(
            CaptureSnapshot a,
            CaptureSnapshot b,
            VisualComparisonResult visual,
            string technical,
            string files)
        {
            return string.Join(
                Environment.NewLine,
                "# Julco Capture Comparison",
                string.Empty,
                "## Captures",
                $"- A: {a.DisplayName}",
                $"- B: {b.DisplayName}",
                string.Empty,
                "## Visual Difference",
                visual.IsAvailable
                    ? $"- Changed pixels: {visual.ChangedPixels:n0} ({visual.ChangedPercent:0.00}%)"
                    : "- Visual diff unavailable.",
                visual.IsAvailable
                    ? $"- Average color delta: {visual.AverageDelta:0.00}"
                    : string.Empty,
                string.Empty,
                "## Technical",
                "```text",
                technical.TrimEnd(),
                "```",
                string.Empty,
                "## Files",
                "```text",
                files.TrimEnd(),
                "```");
        }

        private static void AppendPair(StringBuilder builder, string label, string a, string b)
        {
            var state = string.Equals(a, b, StringComparison.Ordinal) ? "same" : "different";
            builder.AppendLine($"{label}");
            builder.AppendLine($"  A: {a}");
            builder.AppendLine($"  B: {b}");
            builder.AppendLine($"  Result: {state}");
            builder.AppendLine();
        }

        private static string ShortHash(string hash)
        {
            return hash.Length <= 12 ? hash : hash[..12];
        }
    }

    private sealed record VisualComparisonResult(
        bool IsAvailable,
        BitmapImage? DiffImage,
        int WidthA,
        int HeightA,
        int WidthB,
        int HeightB,
        int ComparedWidth,
        int ComparedHeight,
        long ChangedPixels,
        double ChangedPercent,
        double AverageDelta)
    {
        public static VisualComparisonResult Missing { get; } = new(
            false,
            null,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0);
    }

    private sealed record CaptureSnapshot(
        string DirectoryPath,
        string DisplayName,
        DateTimeOffset CreatedAt,
        string Browser,
        string PageTitle,
        string Url,
        string TagName,
        string Selector,
        double X,
        double Y,
        double Width,
        double Height,
        int ConsoleMessageCount,
        int ImageResourceCount,
        CaptureNotes Notes,
        IReadOnlyDictionary<string, string> Hashes)
    {
        public static readonly string[] KnownEvidenceFiles =
        {
            "screenshot.png",
            "inspection.json",
            "dom.html",
            "computed.css",
            "console.txt",
            "attributes.txt",
            "image-resources.json",
            "capture-notes.json",
            "notes.md",
            "evidence-summary.md"
        };

        public string ScreenshotPath => Path.Combine(DirectoryPath, "screenshot.png");

        public string FrameText => $"{Width:0}x{Height:0} at {X:0},{Y:0}";

        public static CaptureSnapshot FromDirectory(string directoryPath)
        {
            var evidencePath = Path.Combine(directoryPath, "evidence.json");
            if (File.Exists(evidencePath))
            {
                try
                {
                    using var evidence = JsonDocument.Parse(File.ReadAllText(evidencePath));
                    var root = evidence.RootElement;
                    var element = root.GetPropertyOrDefault("Element");
                    var frame = root.GetPropertyOrDefault("Frame");
                    var page = root.GetPropertyOrDefault("Page");
                    var browser = root.GetPropertyOrDefault("Browser");
                    var notes = LoadNotes(root, directoryPath);
                    var createdAt = GetDate(root, "CreatedAt") ?? Directory.GetCreationTime(directoryPath);
                    var tagName = GetString(element, "TagName");
                    var selector = GetString(element, "Selector");

                    return new CaptureSnapshot(
                        directoryPath,
                        $"{createdAt:MM-dd HH:mm}  {GetString(browser, "Name")}  {tagName}  {selector}",
                        createdAt,
                        GetString(browser, "Name"),
                        GetString(page, "Title"),
                        GetString(page, "Url"),
                        tagName,
                        selector,
                        GetDouble(frame, "X"),
                        GetDouble(frame, "Y"),
                        GetDouble(frame, "Width"),
                        GetDouble(frame, "Height"),
                        GetInt(element, "ConsoleMessageCount"),
                        GetInt(element, "ImageResourceCount"),
                        notes,
                        BuildHashes(directoryPath));
                }
                catch (JsonException)
                {
                }
                catch (InvalidOperationException)
                {
                }
            }

            return FromManifestOrDirectory(directoryPath);
        }

        private static CaptureSnapshot FromManifestOrDirectory(string directoryPath)
        {
            var manifestPath = Path.Combine(directoryPath, "manifest.json");
            if (File.Exists(manifestPath))
            {
                try
                {
                    using var manifest = JsonDocument.Parse(File.ReadAllText(manifestPath));
                    var root = manifest.RootElement;
                    var createdAt = GetDate(root, "CreatedAt") ?? Directory.GetCreationTime(directoryPath);
                    var tagName = GetString(root, "TagName");
                    var selector = GetString(root, "Selector");
                    return new CaptureSnapshot(
                        directoryPath,
                        $"{createdAt:MM-dd HH:mm}  {tagName}  {selector}",
                        createdAt,
                        "Unknown",
                        GetString(root, "PageTitle"),
                        GetString(root, "Url"),
                        tagName,
                        selector,
                        GetDouble(root, "X"),
                        GetDouble(root, "Y"),
                        GetDouble(root, "Width"),
                        GetDouble(root, "Height"),
                        CountLines(Path.Combine(directoryPath, "console.txt")),
                        CountImages(Path.Combine(directoryPath, "image-resources.json")),
                        LoadCaptureNotes(directoryPath),
                        BuildHashes(directoryPath));
                }
                catch (JsonException)
                {
                }
            }

            var created = Directory.GetCreationTime(directoryPath);
            return new CaptureSnapshot(
                directoryPath,
                Path.GetFileName(directoryPath),
                created,
                "Unknown",
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                0,
                0,
                0,
                0,
                CountLines(Path.Combine(directoryPath, "console.txt")),
                CountImages(Path.Combine(directoryPath, "image-resources.json")),
                LoadCaptureNotes(directoryPath),
                BuildHashes(directoryPath));
        }

        private static CaptureNotes LoadNotes(JsonElement root, string directoryPath)
        {
            if (root.TryGetProperty("StructuredNotes", out var structuredNotes)
                && structuredNotes.ValueKind == JsonValueKind.Object)
            {
                try
                {
                    var notes = structuredNotes.Deserialize<CaptureNotes>();
                    if (notes is not null)
                    {
                        return notes;
                    }
                }
                catch (JsonException)
                {
                }
            }

            return LoadCaptureNotes(directoryPath);
        }

        private static CaptureNotes LoadCaptureNotes(string captureDirectory)
        {
            var structuredNotesPath = Path.Combine(captureDirectory, "capture-notes.json");
            if (File.Exists(structuredNotesPath))
            {
                try
                {
                    var notes = JsonSerializer.Deserialize<CaptureNotes>(File.ReadAllText(structuredNotesPath));
                    if (notes is not null)
                    {
                        return notes;
                    }
                }
                catch (JsonException)
                {
                }
            }

            return CaptureNotes.Empty;
        }

        private static IReadOnlyDictionary<string, string> BuildHashes(string directoryPath)
        {
            return KnownEvidenceFiles.ToDictionary(
                fileName => fileName,
                fileName =>
                {
                    var path = Path.Combine(directoryPath, fileName);
                    return File.Exists(path) ? Sha256(path) : "-";
                },
                StringComparer.OrdinalIgnoreCase);
        }

        private static string Sha256(string path)
        {
            using var stream = File.OpenRead(path);
            var hash = SHA256.HashData(stream);
            return Convert.ToHexString(hash).ToLowerInvariant();
        }

        private static int CountLines(string path)
        {
            return File.Exists(path)
                ? File.ReadLines(path).Count(line => !string.IsNullOrWhiteSpace(line))
                : 0;
        }

        private static int CountImages(string path)
        {
            if (!File.Exists(path))
            {
                return 0;
            }

            try
            {
                using var document = JsonDocument.Parse(File.ReadAllText(path));
                return document.RootElement.ValueKind == JsonValueKind.Array
                    ? document.RootElement.GetArrayLength()
                    : 0;
            }
            catch (JsonException)
            {
                return 0;
            }
        }

        private static string GetString(JsonElement element, string property)
        {
            return element.ValueKind == JsonValueKind.Object
                && element.TryGetProperty(property, out var value)
                && value.ValueKind == JsonValueKind.String
                ? value.GetString() ?? string.Empty
                : string.Empty;
        }

        private static int GetInt(JsonElement element, string property)
        {
            return element.ValueKind == JsonValueKind.Object
                && element.TryGetProperty(property, out var value)
                && value.TryGetInt32(out var result)
                ? result
                : 0;
        }

        private static double GetDouble(JsonElement element, string property)
        {
            return element.ValueKind == JsonValueKind.Object
                && element.TryGetProperty(property, out var value)
                && value.TryGetDouble(out var result)
                ? result
                : 0;
        }

        private static DateTimeOffset? GetDate(JsonElement element, string property)
        {
            return element.ValueKind == JsonValueKind.Object
                && element.TryGetProperty(property, out var value)
                && value.ValueKind == JsonValueKind.String
                && DateTimeOffset.TryParse(value.GetString(), out var result)
                ? result
                : null;
        }
    }
}

internal static class JsonElementExtensions
{
    public static JsonElement GetPropertyOrDefault(this JsonElement element, string property)
    {
        return element.ValueKind == JsonValueKind.Object && element.TryGetProperty(property, out var value)
            ? value
            : default;
    }
}
