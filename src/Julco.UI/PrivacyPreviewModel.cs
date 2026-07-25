using Julco.Core.Privacy;
using Julco.Core.Configuration;

namespace Julco.UI;

public sealed record PrivacyPreviewModel(
    CaptureReport Original,
    CaptureReport Redacted,
    PrivacyRedactionSummary Summary,
    bool IncludeScreenshotInSafeExport,
    bool ScreenshotWillBeRedacted,
    PrivacySettings PrivacySettings,
    IReadOnlyList<PrivacyRedactionFieldPreview> FieldPreviews)
{
    public bool HasChanges => Summary.HasChanges;

    public bool ScreenshotRisk => IncludeScreenshotInSafeExport && !ScreenshotWillBeRedacted && !string.IsNullOrWhiteSpace(Original.ScreenshotPath);

    public string SummaryText => string.Join(
        Environment.NewLine,
        new[]
        {
            $"Total findings: {Summary.TotalMatches}",
            $"Emails: {Summary.EmailMatches}",
            $"Tokens/secrets: {Summary.TokenMatches}",
            $"Cookies/sessions: {Summary.CookieMatches}",
            $"Private URLs: {Summary.PrivateUrlMatches}",
            $"HTML visible text nodes: {Summary.HtmlTextNodeMatches}",
            $"Custom rules: {Summary.CustomRuleMatches}",
            IncludeScreenshotInSafeExport
                ? ScreenshotWillBeRedacted
                    ? "Screenshot: included as redacted image."
                    : "Screenshot: included as original image. Review it before sharing."
                : "Screenshot: omitted from safe export.",
            ScreenshotRisk
                ? "Warning: screenshot may contain visible sensitive data."
                : "Screenshot risk: controlled by current settings."
        });

    public string OriginalPreview => BuildPreview(Original);

    public string RedactedPreview => BuildPreview(Redacted);

    public static PrivacyPreviewModel Create(
        CaptureReport report,
        PrivacyRedactorOptions options,
        bool includeScreenshotInSafeExport,
        bool screenshotWillBeRedacted,
        PrivacySettings privacySettings)
    {
        var redacted = report.Redacted(options);
        return new PrivacyPreviewModel(
            report,
            redacted,
            Analyze(report, options),
            includeScreenshotInSafeExport,
            screenshotWillBeRedacted,
            privacySettings.Normalized(),
            BuildFieldPreviews(report, redacted, options));
    }

    public CaptureReport SafeReport()
    {
        return IncludeScreenshotInSafeExport
            ? Redacted
            : Redacted with { ScreenshotPath = string.Empty };
    }

    private static PrivacyRedactionSummary Analyze(CaptureReport report, PrivacyRedactorOptions options)
    {
        return PrivacyRedactor.AnalyzeText(report.Title, options)
            .Add(PrivacyRedactor.AnalyzeText(report.Browser, options))
            .Add(PrivacyRedactor.AnalyzeText(report.TargetType, options))
            .Add(PrivacyRedactor.AnalyzeText(report.PageUrl, options))
            .Add(PrivacyRedactor.AnalyzeText(report.PageTitle, options))
            .Add(PrivacyRedactor.AnalyzeText(report.TagName, options))
            .Add(PrivacyRedactor.AnalyzeText(report.Selector, options))
            .Add(PrivacyRedactor.AnalyzeText(report.Notes.Observation, options))
            .Add(PrivacyRedactor.AnalyzeText(report.Notes.Tags, options))
            .Add(PrivacyRedactor.AnalyzeHtml(report.Dom, options))
            .Add(PrivacyRedactor.AnalyzeText(report.ComputedCss, options))
            .Add(PrivacyRedactor.AnalyzeText(report.Console, options))
            .Add(PrivacyRedactor.AnalyzeText(report.Attributes, options))
            .Add(PrivacyRedactor.AnalyzeText(report.CommonIssues, options))
            .Add(report.Images.Aggregate(PrivacyRedactionSummary.Empty, (summary, image) =>
                summary
                    .Add(PrivacyRedactor.AnalyzeText(image.Url, options))
                    .Add(PrivacyRedactor.AnalyzeText(image.Alt, options))));
    }

    private static IReadOnlyList<PrivacyRedactionFieldPreview> BuildFieldPreviews(
        CaptureReport original,
        CaptureReport redacted,
        PrivacyRedactorOptions options)
    {
        var fields = new[]
        {
            Field("Title", original.Title, redacted.Title, options, html: false),
            Field("URL", original.PageUrl, redacted.PageUrl, options, html: false),
            Field("Selector", original.Selector, redacted.Selector, options, html: false),
            Field("Notes", original.Notes.Observation, redacted.Notes.Observation, options, html: false),
            Field("Note tags", original.Notes.Tags, redacted.Notes.Tags, options, html: false),
            Field("DOM", original.Dom, redacted.Dom, options, html: true),
            Field("Computed CSS", original.ComputedCss, redacted.ComputedCss, options, html: false),
            Field("Console", original.Console, redacted.Console, options, html: false),
            Field("Attributes", original.Attributes, redacted.Attributes, options, html: false),
            Field("Common issues", original.CommonIssues, redacted.CommonIssues, options, html: false),
            Field(
                "Image URLs",
                string.Join(Environment.NewLine, original.Images.Select(image => image.Url)),
                string.Join(Environment.NewLine, redacted.Images.Select(image => image.Url)),
                options,
                html: false)
        };

        return fields.Where(field => field.HasChanges).ToArray();
    }

    private static PrivacyRedactionFieldPreview Field(
        string name,
        string before,
        string after,
        PrivacyRedactorOptions options,
        bool html)
    {
        var summary = html
            ? PrivacyRedactor.AnalyzeHtml(before, options)
            : PrivacyRedactor.AnalyzeText(before, options);
        return new PrivacyRedactionFieldPreview(name, summary, Shorten(before), Shorten(after));
    }

    private static string BuildPreview(CaptureReport report)
    {
        return string.Join(
            Environment.NewLine,
            new[]
            {
                $"Title: {report.Title}",
                $"URL: {report.PageUrl}",
                $"Selector: {report.Selector}",
                $"Notes: {report.Notes.ShortSummary}",
                string.Empty,
                "Attributes",
                Shorten(report.Attributes),
                string.Empty,
                "Console",
                Shorten(report.Console),
                string.Empty,
                "DOM",
                Shorten(report.Dom)
            });
    }

    private static string Shorten(string? value)
    {
        var text = string.IsNullOrWhiteSpace(value)
            ? "-"
            : value.Trim();
        return text.Length <= 4000
            ? text
            : text[..4000] + Environment.NewLine + "...";
    }
}
