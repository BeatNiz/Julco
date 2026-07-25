using System.IO;
using System.Text.Json;
using Julco.Cdp;
using Julco.Core.Privacy;

namespace Julco.UI;

public sealed record CaptureReport(
    string CaptureDirectory,
    string Title,
    DateTimeOffset CreatedAt,
    string Browser,
    string RemotePort,
    string TargetType,
    string PageUrl,
    string PageTitle,
    string UsageProfile,
    string TagName,
    string Selector,
    EvidenceFrameContext Frame,
    CaptureNotes Notes,
    string ScreenshotPath,
    string Dom,
    string ComputedCss,
    string Console,
    string Attributes,
    string CommonIssues,
    IReadOnlyList<WebImageResource> Images)
{
    public static CaptureReport FromDirectory(string captureDirectory, string usageProfile)
    {
        var evidence = LoadJsonFile<EvidencePackage>(Path.Combine(captureDirectory, "evidence.json"));
        var manifest = evidence is null
            ? LoadJsonFile<CaptureManifest>(Path.Combine(captureDirectory, "manifest.json"))
            : null;
        var notes = evidence?.StructuredNotes ?? CaptureNotesStore.Load(captureDirectory);
        var screenshot = ResolvePath(captureDirectory, evidence?.Files.Screenshot ?? manifest?.Screenshot ?? "screenshot.png");
        var createdAt = evidence?.CreatedAt
            ?? manifest?.CreatedAt
            ?? new DateTimeOffset(Directory.GetCreationTime(captureDirectory));
        var frame = evidence?.Frame ?? new EvidenceFrameContext(
            manifest?.X ?? 0,
            manifest?.Y ?? 0,
            manifest?.Width ?? 0,
            manifest?.Height ?? 0,
            (manifest?.X ?? 0) + ((manifest?.Width ?? 0) / 2),
            (manifest?.Y ?? 0) + ((manifest?.Height ?? 0) / 2),
            "Unknown",
            0,
            0);
        var images = LoadJsonFile<WebImageResource[]>(
                ResolvePath(captureDirectory, evidence?.Files.Images ?? "image-resources.json"))
            ?? Array.Empty<WebImageResource>();
        var title = evidence?.Page.Title
            ?? manifest?.PageTitle
            ?? Path.GetFileName(captureDirectory);

        return new CaptureReport(
            captureDirectory,
            string.IsNullOrWhiteSpace(title) ? "Julco Capture Report" : title,
            createdAt,
            evidence?.Browser.Name ?? "Unknown",
            evidence?.Browser.RemotePort ?? "-",
            evidence?.Browser.TargetType ?? "-",
            evidence?.Page.Url ?? manifest?.Url ?? "-",
            title,
            usageProfile,
            evidence?.Element.TagName ?? manifest?.TagName ?? "-",
            evidence?.Element.Selector ?? manifest?.Selector ?? "-",
            frame,
            notes,
            File.Exists(screenshot) ? screenshot : string.Empty,
            ReadText(ResolvePath(captureDirectory, evidence?.Files.Dom ?? "dom.html")),
            ReadText(ResolvePath(captureDirectory, evidence?.Files.ComputedCss ?? "computed.css")),
            ReadText(ResolvePath(captureDirectory, evidence?.Files.Console ?? "console.txt")),
            ReadText(ResolvePath(captureDirectory, evidence?.Files.Attributes ?? "attributes.txt")),
            ReadText(Path.Combine(captureDirectory, "common-issues.md")),
            images);
    }

    public CaptureReport Redacted(PrivacyRedactorOptions options)
    {
        if (!options.Enabled)
        {
            return this;
        }

        return this with
        {
            Title = PrivacyRedactor.RedactText(Title, options),
            Browser = PrivacyRedactor.RedactText(Browser, options),
            TargetType = PrivacyRedactor.RedactText(TargetType, options),
            PageUrl = PrivacyRedactor.RedactText(PageUrl, options),
            PageTitle = PrivacyRedactor.RedactText(PageTitle, options),
            TagName = PrivacyRedactor.RedactText(TagName, options),
            Selector = PrivacyRedactor.RedactText(Selector, options),
            Notes = Notes with
            {
                Observation = PrivacyRedactor.RedactText(Notes.Observation, options),
                Tags = PrivacyRedactor.RedactText(Notes.Tags, options)
            },
            Dom = PrivacyRedactor.RedactHtml(Dom, options),
            ComputedCss = PrivacyRedactor.RedactText(ComputedCss, options),
            Console = PrivacyRedactor.RedactText(Console, options),
            Attributes = PrivacyRedactor.RedactText(Attributes, options),
            CommonIssues = PrivacyRedactor.RedactText(CommonIssues, options),
            Images = Images.Select(image => image with
            {
                Url = PrivacyRedactor.RedactText(image.Url, options),
                Alt = PrivacyRedactor.RedactText(image.Alt, options)
            }).ToArray()
        };
    }

    public string BuildMarkdown()
    {
        return new MarkdownReportRenderer().Render(CaptureReportRenderContext.CreateDefault(this));
    }

    public string BuildHtml()
    {
        return new HtmlReportRenderer().Render(CaptureReportRenderContext.CreateDefault(this));
    }

    public IReadOnlyList<string> BuildPdfLines()
    {
        return new PdfReportRenderer().RenderLines(CaptureReportRenderContext.CreateDefault(this));
    }
    public static string NormalizeMarkdownLine(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? "-"
            : value.ReplaceLineEndings(" ").Trim();
    }

    private static T? LoadJsonFile<T>(string path)
    {
        if (!File.Exists(path))
        {
            return default;
        }

        try
        {
            return JsonSerializer.Deserialize<T>(File.ReadAllText(path));
        }
        catch (JsonException)
        {
            return default;
        }
    }

    private static string ResolvePath(string captureDirectory, string? relativeOrAbsolute)
    {
        if (string.IsNullOrWhiteSpace(relativeOrAbsolute))
        {
            return captureDirectory;
        }

        return Path.IsPathRooted(relativeOrAbsolute)
            ? relativeOrAbsolute
            : Path.Combine(captureDirectory, relativeOrAbsolute);
    }

    private static string ReadText(string path)
    {
        return File.Exists(path) ? File.ReadAllText(path) : string.Empty;
    }
}

