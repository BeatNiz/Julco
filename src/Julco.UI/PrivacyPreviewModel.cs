using Julco.Core.Privacy;

namespace Julco.UI;

public sealed record PrivacyPreviewModel(
    CaptureReport Original,
    CaptureReport Redacted,
    PrivacyRedactionSummary Summary,
    bool IncludeScreenshotInSafeExport)
{
    public bool HasChanges => Summary.HasChanges;

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
            IncludeScreenshotInSafeExport
                ? "Screenshot: included by Settings. Review it before sharing."
                : "Screenshot: omitted from safe export."
        });

    public string OriginalPreview => BuildPreview(Original);

    public string RedactedPreview => BuildPreview(Redacted);

    public static PrivacyPreviewModel Create(
        CaptureReport report,
        PrivacyRedactorOptions options,
        bool includeScreenshotInSafeExport)
    {
        return new PrivacyPreviewModel(
            report,
            report.Redacted(options),
            Analyze(report, options),
            includeScreenshotInSafeExport);
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
