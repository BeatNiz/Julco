using System.IO;
using System.Text.Json;

namespace Julco.UI;

public sealed record CaptureFileRecord(
    string DirectoryPath,
    string DisplayName,
    DateTimeOffset CreatedAt,
    string Browser,
    string Url,
    string PageTitle,
    string TagName,
    string Selector,
    string NoteStatus,
    string NoteSeverity,
    string NoteTags,
    string NoteText,
    string ScreenshotPath,
    bool IsFavorite,
    string LibraryTags,
    string Project,
    string SessionId,
    string Domain,
    string SearchText)
{
    public string ThumbnailPath => ResolveThumbnailPath(ScreenshotPath);

    public string ThumbnailFallback => File.Exists(ScreenshotPath) ? string.Empty : "No image";

    public string CreatedLocalText => CreatedAt.LocalDateTime.ToString("MM-dd HH:mm");

    public string FavoriteGlyph => IsFavorite ? "★" : "☆";

    public string LibraryTagsDisplay => string.IsNullOrWhiteSpace(LibraryTags) ? "-" : LibraryTags;

    public string ProjectDisplay => string.IsNullOrWhiteSpace(Project) ? Domain : Project;

    public string SessionDisplay => string.IsNullOrWhiteSpace(SessionId) ? CreatedAt.LocalDateTime.ToString("yyyy-MM-dd") : SessionId;

    public string HistoryTitle => string.IsNullOrWhiteSpace(PageTitle)
        ? Path.GetFileName(DirectoryPath)
        : PageTitle;

    public string HistorySubtitle => string.IsNullOrWhiteSpace(Selector)
        ? $"{TagName}  {Url}"
        : $"{TagName}  {Selector}";

    public string HistoryMeta
    {
        get
        {
            var note = string.IsNullOrWhiteSpace(NoteText)
                ? string.Empty
                : $"  notes:{NoteSeverity}/{NoteStatus}";
            return $"{CreatedLocalText}  {Browser}  {ShortenForHistory(Url, 90)}{note}";
        }
    }

    public string GalleryMeta => $"{FavoriteGlyph} {CreatedLocalText}  {Browser}  {ProjectDisplay}";

    public static CaptureFileRecord FromDirectory(string directoryPath)
    {
        var library = CaptureLibraryStore.LoadItem(directoryPath);
        var evidencePath = Path.Combine(directoryPath, "evidence.json");
        if (File.Exists(evidencePath))
        {
            try
            {
                var evidence = JsonSerializer.Deserialize<EvidencePackage>(File.ReadAllText(evidencePath));
                if (evidence is not null)
                {
                    var notes = evidence.StructuredNotes ?? CaptureNotesStore.Load(directoryPath);
                    var noteMarker = notes.HasContent
                        ? $"  notes:{notes.Severity}/{notes.Status}"
                        : string.Empty;
                    var displayName = $"{evidence.CreatedAt:MM-dd HH:mm}  evidence  {evidence.Element.TagName}  {evidence.Element.Selector}{noteMarker}";
                    return new CaptureFileRecord(
                        directoryPath,
                        displayName,
                        evidence.CreatedAt,
                        evidence.Browser.Name,
                        evidence.Page.Url,
                        evidence.Page.Title,
                        evidence.Element.TagName,
                        evidence.Element.Selector,
                        notes.Status,
                        notes.Severity,
                        notes.Tags,
                        notes.Observation,
                        ResolveCaptureFile(directoryPath, evidence.Files.Screenshot),
                        library.IsFavorite,
                        library.Tags,
                        library.Project,
                        ResolveSessionId(library, evidence.CreatedAt),
                        ResolveDomain(evidence.Page.Url),
                        BuildCaptureSearchText(
                            directoryPath,
                            displayName,
                            evidence.Browser.Name,
                            evidence.Page.Url,
                            evidence.Page.Title,
                            evidence.Element.TagName,
                            evidence.Element.Selector,
                            notes,
                            library));
                }
            }
            catch (JsonException)
            {
            }
        }

        var manifestPath = Path.Combine(directoryPath, "manifest.json");
        if (File.Exists(manifestPath))
        {
            try
            {
                var manifest = JsonSerializer.Deserialize<CaptureManifest>(File.ReadAllText(manifestPath));
                if (manifest is not null)
                {
                    var notes = CaptureNotesStore.Load(directoryPath);
                    var noteMarker = notes.HasContent
                        ? $"  notes:{notes.Severity}/{notes.Status}"
                        : string.Empty;
                    var displayName = $"{manifest.CreatedAt:MM-dd HH:mm}  {manifest.TagName}  {manifest.Selector}{noteMarker}";
                    return new CaptureFileRecord(
                        directoryPath,
                        displayName,
                        manifest.CreatedAt,
                        "Unknown",
                        manifest.Url,
                        manifest.PageTitle,
                        manifest.TagName,
                        manifest.Selector,
                        notes.Status,
                        notes.Severity,
                        notes.Tags,
                        notes.Observation,
                        ResolveCaptureFile(directoryPath, manifest.Screenshot),
                        library.IsFavorite,
                        library.Tags,
                        library.Project,
                        ResolveSessionId(library, manifest.CreatedAt),
                        ResolveDomain(manifest.Url),
                        BuildCaptureSearchText(
                            directoryPath,
                            displayName,
                            "Unknown",
                            manifest.Url,
                            manifest.PageTitle,
                            manifest.TagName,
                            manifest.Selector,
                            notes,
                            library));
                }
            }
            catch (JsonException)
            {
            }
        }

        var fallbackNotes = CaptureNotesStore.Load(directoryPath);
        var fallbackName = Path.GetFileName(directoryPath);
        return new CaptureFileRecord(
            directoryPath,
            fallbackName,
            Directory.GetCreationTime(directoryPath),
            "Unknown",
            string.Empty,
            string.Empty,
            string.Empty,
            string.Empty,
            fallbackNotes.Status,
            fallbackNotes.Severity,
            fallbackNotes.Tags,
            fallbackNotes.Observation,
            ResolveCaptureFile(directoryPath, "screenshot.png"),
            library.IsFavorite,
            library.Tags,
            library.Project,
            ResolveSessionId(library, Directory.GetCreationTime(directoryPath)),
            string.Empty,
            BuildCaptureSearchText(
                directoryPath,
                fallbackName,
                "Unknown",
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                fallbackNotes,
                library));
    }

    private static string BuildCaptureSearchText(
        string directoryPath,
        string displayName,
        string browser,
        string url,
        string pageTitle,
        string tagName,
        string selector,
        CaptureNotes notes,
        CaptureLibraryItemMetadata library)
    {
        return string.Join(
            " ",
            displayName,
            browser,
            url,
            pageTitle,
            tagName,
            selector,
            notes.Category,
            notes.Severity,
            notes.Status,
            notes.Tags,
            notes.Observation,
            library.Tags,
            library.Project,
            library.SessionId,
            Path.GetFileName(directoryPath));
    }

    private static string ResolveDomain(string? url)
    {
        return Uri.TryCreate(url, UriKind.Absolute, out var uri)
            ? uri.Host
            : string.Empty;
    }

    private static string ResolveSessionId(CaptureLibraryItemMetadata library, DateTimeOffset createdAt)
    {
        return string.IsNullOrWhiteSpace(library.SessionId)
            ? createdAt.LocalDateTime.ToString("yyyy-MM-dd")
            : library.SessionId;
    }

    private static string ResolveCaptureFile(string directoryPath, string? relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
        {
            return string.Empty;
        }

        var path = Path.IsPathRooted(relativePath)
            ? relativePath
            : Path.Combine(directoryPath, relativePath);
        return File.Exists(path) ? path : string.Empty;
    }

    private static string ResolveThumbnailPath(string screenshotPath)
    {
        if (string.IsNullOrWhiteSpace(screenshotPath) || !File.Exists(screenshotPath))
        {
            return string.Empty;
        }

        var thumbnailPath = Path.Combine(
            Path.GetDirectoryName(screenshotPath) ?? string.Empty,
            ".julco-thumbnail.png");
        try
        {
            if (File.Exists(thumbnailPath)
                && File.GetLastWriteTimeUtc(thumbnailPath) >= File.GetLastWriteTimeUtc(screenshotPath))
            {
                return thumbnailPath;
            }

            using var source = System.Drawing.Image.FromFile(screenshotPath);
            const int maxWidth = 180;
            const int maxHeight = 120;
            var scale = Math.Min(maxWidth / (double)source.Width, maxHeight / (double)source.Height);
            var width = Math.Max(1, (int)Math.Round(source.Width * scale));
            var height = Math.Max(1, (int)Math.Round(source.Height * scale));
            using var thumbnail = new System.Drawing.Bitmap(maxWidth, maxHeight);
            using var graphics = System.Drawing.Graphics.FromImage(thumbnail);
            graphics.Clear(System.Drawing.Color.FromArgb(10, 14, 20));
            graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
            graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
            var x = (maxWidth - width) / 2;
            var y = (maxHeight - height) / 2;
            graphics.DrawImage(source, x, y, width, height);
            thumbnail.Save(thumbnailPath, System.Drawing.Imaging.ImageFormat.Png);
            return thumbnailPath;
        }
        catch
        {
            return screenshotPath;
        }
    }

    private static string ShortenForHistory(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "-";
        }

        return value.Length <= maxLength
            ? value
            : value[..Math.Max(0, maxLength - 1)] + "...";
    }
}
