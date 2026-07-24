using System.Globalization;
using System.IO;
using System.Net;
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
        var lines = new List<string>
        {
            "# Julco Capture Report",
            string.Empty,
            $"**Page:** {NormalizeMarkdownLine(PageTitle)}",
            $"**URL:** {NormalizeMarkdownLine(PageUrl)}",
            $"**Created:** {CreatedAt:yyyy-MM-dd HH:mm:ss zzz}",
            $"**Profile:** {NormalizeMarkdownLine(UsageProfile)}",
            string.Empty
        };

        if (!string.IsNullOrWhiteSpace(ScreenshotPath))
        {
            lines.Add("![Capture screenshot](../screenshot.png)");
            lines.Add(string.Empty);
        }

        lines.AddRange(new[]
        {
            "## Technical Summary",
            string.Empty,
            "| Field | Value |",
            "| --- | --- |",
            $"| Browser | {NormalizeMarkdownLine(Browser)} |",
            $"| Remote port | {NormalizeMarkdownLine(RemotePort)} |",
            $"| Target type | {NormalizeMarkdownLine(TargetType)} |",
            $"| Profile | {NormalizeMarkdownLine(UsageProfile)} |",
            $"| Element | `{NormalizeMarkdownLine(TagName)}` |",
            $"| Selector | `{NormalizeMarkdownLine(Selector)}` |",
            $"| Lens frame | {Frame.Width:0}x{Frame.Height:0} at {Frame.X:0},{Frame.Y:0} |",
            $"| Center | {Frame.CenterX:0},{Frame.CenterY:0} |",
            $"| Screen | {NormalizeMarkdownLine(Frame.ScreenName)} {Frame.ScreenWidth}x{Frame.ScreenHeight} |",
            $"| Image resources | {Images.Count} |",
            string.Empty,
            "## Notes",
            string.Empty,
            Notes.HasContent ? Notes.ToMarkdown() : "_No notes added._",
            string.Empty,
            "## Common Issues",
            string.Empty,
            string.IsNullOrWhiteSpace(CommonIssues) ? "_No issue report found._" : CommonIssues,
            string.Empty,
            "## Attributes",
            string.Empty,
            CodeBlock(Attributes, "text"),
            string.Empty,
            "## Computed CSS",
            string.Empty,
            CodeBlock(ComputedCss, "css"),
            string.Empty,
            "## DOM",
            string.Empty,
            CodeBlock(Dom, "html")
        });

        return string.Join(Environment.NewLine, lines);
    }

    public string BuildHtml()
    {
        var screenshotMarkup = string.IsNullOrWhiteSpace(ScreenshotPath)
            ? "<div class=\"empty\">No screenshot found.</div>"
            : "<img class=\"screenshot\" src=\"../screenshot.png\" alt=\"Capture screenshot\" />";
        var notesMarkup = Notes.HasContent
            ? $"<dl>{Definition("Category", Notes.Category)}{Definition("Severity", Notes.Severity)}{Definition("Status", Notes.Status)}{Definition("Tags", Notes.Tags)}</dl><p>{EscapeHtml(Notes.Observation)}</p>"
            : "<p class=\"empty\">No notes added.</p>";
        var imageRows = Images.Count == 0
            ? "<tr><td colspan=\"5\">No image resources detected.</td></tr>"
            : string.Join(Environment.NewLine, Images.Take(80).Select(image =>
                $"<tr><td>{EscapeHtml(image.Kind)}</td><td>{EscapeHtml(image.Format)}</td><td>{image.DisplayedWidth}x{image.DisplayedHeight}</td><td>{image.NaturalWidth}x{image.NaturalHeight}</td><td><code>{EscapeHtml(Shorten(image.Url, 120))}</code></td></tr>"));

        return $$"""
            <!doctype html>
            <html lang="en">
            <head>
              <meta charset="utf-8" />
              <meta name="viewport" content="width=device-width, initial-scale=1" />
              <title>Julco Capture Report</title>
              <style>
                :root { color-scheme: dark; --bg:#0f1218; --panel:#181d26; --ink:#f5f7fb; --muted:#aeb8c8; --line:#2d3645; --accent:#41c7ff; --soft:#111722; }
                * { box-sizing: border-box; }
                body { margin:0; background:var(--bg); color:var(--ink); font:14px/1.55 "Segoe UI", Arial, sans-serif; }
                main { max-width:1120px; margin:0 auto; padding:32px; }
                header { border-bottom:1px solid var(--line); padding-bottom:18px; margin-bottom:24px; }
                h1 { margin:0 0 6px; font-size:30px; letter-spacing:0; }
                h2 { margin:0 0 12px; font-size:18px; color:var(--accent); }
                .muted { color:var(--muted); }
                .hero { display:grid; grid-template-columns:minmax(280px, 1.2fr) minmax(260px, .8fr); gap:18px; align-items:start; }
                .panel { background:var(--panel); border:1px solid var(--line); border-radius:8px; padding:18px; margin-bottom:18px; }
                .screenshot { width:100%; max-height:520px; object-fit:contain; background:#05070a; border:1px solid var(--line); border-radius:6px; }
                dl { display:grid; grid-template-columns:130px 1fr; gap:8px 14px; margin:0; }
                dt { color:var(--muted); }
                dd { margin:0; word-break:break-word; }
                code, pre { font-family:Consolas, "Cascadia Mono", monospace; }
                pre { max-height:360px; overflow:auto; padding:14px; border-radius:6px; background:#05070a; border:1px solid var(--line); white-space:pre-wrap; }
                table { width:100%; border-collapse:collapse; }
                th, td { text-align:left; border-bottom:1px solid var(--line); padding:8px; vertical-align:top; }
                th { color:var(--accent); font-weight:600; }
                .empty { color:var(--muted); padding:18px; border:1px dashed var(--line); border-radius:6px; }
                @media (max-width: 760px) { main { padding:18px; } .hero { grid-template-columns:1fr; } dl { grid-template-columns:1fr; } }
              </style>
            </head>
            <body>
            <main>
              <header>
                <h1>Julco Capture Report</h1>
                <div class="muted">{{EscapeHtml(PageTitle)}} · {{EscapeHtml(CreatedAt.ToString("yyyy-MM-dd HH:mm:ss zzz"))}}</div>
              </header>
              <section class="hero">
                <div class="panel">{{screenshotMarkup}}</div>
                <div class="panel">
                  <h2>Technical Summary</h2>
                  <dl>
                    {{Definition("URL", PageUrl)}}
                    {{Definition("Browser", Browser)}}
                    {{Definition("Remote port", RemotePort)}}
                    {{Definition("Target type", TargetType)}}
                    {{Definition("Profile", UsageProfile)}}
                    {{Definition("Element", TagName)}}
                    {{Definition("Selector", Selector)}}
                    {{Definition("Lens frame", $"{Frame.Width:0}x{Frame.Height:0} at {Frame.X:0},{Frame.Y:0}")}}
                    {{Definition("Center", $"{Frame.CenterX:0},{Frame.CenterY:0}")}}
                    {{Definition("Screen", $"{Frame.ScreenName} {Frame.ScreenWidth}x{Frame.ScreenHeight}")}}
                    {{Definition("Images", Images.Count.ToString(CultureInfo.InvariantCulture))}}
                  </dl>
                </div>
              </section>
              <section class="panel"><h2>Notes</h2>{{notesMarkup}}</section>
              <section class="panel"><h2>Common Issues</h2><pre>{{EscapeHtml(string.IsNullOrWhiteSpace(CommonIssues) ? "No issue report found." : CommonIssues)}}</pre></section>
              <section class="panel"><h2>Image Resources</h2><table><thead><tr><th>Kind</th><th>Format</th><th>Shown</th><th>Natural</th><th>URL</th></tr></thead><tbody>{{imageRows}}</tbody></table></section>
              <section class="panel"><h2>Attributes</h2><pre>{{EscapeHtml(Attributes)}}</pre></section>
              <section class="panel"><h2>Computed CSS</h2><pre>{{EscapeHtml(ComputedCss)}}</pre></section>
              <section class="panel"><h2>DOM</h2><pre>{{EscapeHtml(Dom)}}</pre></section>
            </main>
            </body>
            </html>
            """;
    }

    public IReadOnlyList<string> BuildPdfLines()
    {
        var lines = new List<string>
        {
            "Julco Capture Report",
            $"Page: {PageTitle}",
            $"URL: {PageUrl}",
            $"Created: {CreatedAt:yyyy-MM-dd HH:mm:ss zzz}",
            $"Profile: {UsageProfile}",
            $"Browser: {Browser} | Port: {RemotePort} | Target: {TargetType}",
            $"Element: {TagName} | Selector: {Selector}",
            $"Lens: {Frame.Width:0}x{Frame.Height:0} at {Frame.X:0},{Frame.Y:0} | Center: {Frame.CenterX:0},{Frame.CenterY:0}",
            $"Screen: {Frame.ScreenName} {Frame.ScreenWidth}x{Frame.ScreenHeight}",
            string.Empty,
            "Notes",
            Notes.HasContent ? Notes.ShortSummary : "No notes added.",
            string.IsNullOrWhiteSpace(Notes.Observation) ? string.Empty : Notes.Observation,
            string.Empty,
            "Common Issues",
            string.IsNullOrWhiteSpace(CommonIssues) ? "No issue report found." : CommonIssues,
            string.Empty,
            "Attributes",
            Attributes,
            string.Empty,
            "Computed CSS",
            ComputedCss,
            string.Empty,
            "DOM preview",
            Shorten(Dom, 5000)
        };

        return lines
            .SelectMany(line => WrapLine(line.ReplaceLineEndings(" "), 96))
            .ToArray();
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

    private static string CodeBlock(string content, string language)
    {
        return $"```{language}{Environment.NewLine}{content.Trim()}{Environment.NewLine}```";
    }

    private static string Definition(string key, string? value)
    {
        return $"<dt>{EscapeHtml(key)}</dt><dd>{EscapeHtml(string.IsNullOrWhiteSpace(value) ? "-" : value)}</dd>";
    }

    private static string EscapeHtml(string? value)
    {
        return WebUtility.HtmlEncode(value ?? string.Empty);
    }

    private static string Shorten(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length <= maxLength)
        {
            return value ?? string.Empty;
        }

        return value[..Math.Max(0, maxLength - 1)] + "...";
    }

    private static IEnumerable<string> WrapLine(string line, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            yield return string.Empty;
            yield break;
        }

        var remaining = line.Trim();
        while (remaining.Length > maxLength)
        {
            var splitAt = remaining.LastIndexOf(' ', Math.Min(maxLength, remaining.Length - 1));
            if (splitAt < maxLength / 2)
            {
                splitAt = maxLength;
            }

            yield return remaining[..splitAt].Trim();
            remaining = remaining[splitAt..].Trim();
        }

        if (remaining.Length > 0)
        {
            yield return remaining;
        }
    }
}
