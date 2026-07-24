using System.IO;

namespace Julco.UI;

public static class IssueTrackerDraftFactory
{
    public static IReadOnlyList<IssueTrackerDraft> Create(CaptureReport report, string outputDirectory)
    {
        Directory.CreateDirectory(outputDirectory);
        var title = BuildTitle(report);
        return new[]
        {
            new IssueTrackerDraft(
                "GitHub Issues",
                title,
                BuildGitHubBody(report),
                Path.Combine(outputDirectory, "github-issue.md")),
            new IssueTrackerDraft(
                "Jira",
                title,
                BuildJiraBody(report),
                Path.Combine(outputDirectory, "jira-issue.txt")),
            new IssueTrackerDraft(
                "Generic ticket",
                title,
                BuildGenericBody(report),
                Path.Combine(outputDirectory, "generic-ticket.txt"))
        };
    }

    private static string BuildTitle(CaptureReport report)
    {
        var severity = report.Notes.HasContent ? report.Notes.Severity : "Needs review";
        var tag = string.IsNullOrWhiteSpace(report.TagName) ? "element" : report.TagName.ToLowerInvariant();
        var page = string.IsNullOrWhiteSpace(report.PageTitle) || report.PageTitle == "-"
            ? Shorten(report.PageUrl, 54)
            : Shorten(report.PageTitle, 54);
        return $"[{severity}] {tag} issue on {page}";
    }

    private static string BuildGitHubBody(CaptureReport report)
    {
        return string.Join(
            Environment.NewLine,
            "## Summary",
            BuildSummary(report),
            string.Empty,
            "## Current behavior",
            "- [ ] Confirm the visible behavior in the attached Julco screenshot.",
            "- [ ] Confirm whether this reproduces in the same browser/profile.",
            string.Empty,
            "## Expected behavior",
            "- [ ] Describe the expected UI, DOM, CSS, accessibility, or content behavior.",
            string.Empty,
            "## Evidence",
            $"- Screenshot: `{RelativeEvidencePath("screenshot.png")}`",
            $"- HTML report: `{RelativeEvidencePath("report/report.html")}`",
            $"- PDF report: `{RelativeEvidencePath("report/report.pdf")}`",
            $"- Markdown report: `{RelativeEvidencePath("report/report.md")}`",
            string.Empty,
            "## Technical details",
            BuildMarkdownDetails(report),
            string.Empty,
            "## Notes",
            BuildNotes(report),
            string.Empty,
            "## Common issues detected",
            string.IsNullOrWhiteSpace(report.CommonIssues) ? "_No issue report found._" : report.CommonIssues);
    }

    private static string BuildJiraBody(CaptureReport report)
    {
        return string.Join(
            Environment.NewLine,
            "h2. Summary",
            BuildSummary(report),
            string.Empty,
            "h2. Current behavior",
            "* Confirm the visible behavior in the attached Julco screenshot.",
            "* Confirm whether this reproduces in the same browser/profile.",
            string.Empty,
            "h2. Expected behavior",
            "* Describe the expected UI, DOM, CSS, accessibility, or content behavior.",
            string.Empty,
            "h2. Evidence",
            $"* Screenshot: {{code}}{RelativeEvidencePath("screenshot.png")}{{code}}",
            $"* HTML report: {{code}}{RelativeEvidencePath("report/report.html")}{{code}}",
            $"* PDF report: {{code}}{RelativeEvidencePath("report/report.pdf")}{{code}}",
            $"* Markdown report: {{code}}{RelativeEvidencePath("report/report.md")}{{code}}",
            string.Empty,
            "h2. Technical details",
            BuildJiraDetails(report),
            string.Empty,
            "h2. Notes",
            BuildNotes(report),
            string.Empty,
            "h2. Common issues detected",
            string.IsNullOrWhiteSpace(report.CommonIssues) ? "No issue report found." : report.CommonIssues);
    }

    private static string BuildGenericBody(CaptureReport report)
    {
        return string.Join(
            Environment.NewLine,
            "SUMMARY",
            BuildSummary(report),
            string.Empty,
            "WHAT HAPPENED",
            "Confirm the visible behavior in the attached Julco screenshot.",
            string.Empty,
            "WHAT SHOULD HAPPEN",
            "Describe the expected UI, DOM, CSS, accessibility, or content behavior.",
            string.Empty,
            "EVIDENCE FILES",
            $"- {RelativeEvidencePath("screenshot.png")}",
            $"- {RelativeEvidencePath("report/report.html")}",
            $"- {RelativeEvidencePath("report/report.pdf")}",
            $"- {RelativeEvidencePath("report/report.md")}",
            string.Empty,
            "TECHNICAL DETAILS",
            BuildPlainDetails(report),
            string.Empty,
            "NOTES",
            BuildNotes(report),
            string.Empty,
            "COMMON ISSUES",
            string.IsNullOrWhiteSpace(report.CommonIssues) ? "No issue report found." : report.CommonIssues);
    }

    private static string BuildSummary(CaptureReport report)
    {
        if (!string.IsNullOrWhiteSpace(report.Notes.Observation))
        {
            return report.Notes.Observation.Trim();
        }

        return $"Julco captured `{report.Selector}` on {report.PageUrl}. Review the attached evidence package for visual, DOM, CSS, console, and accessibility signals.";
    }

    private static string BuildMarkdownDetails(CaptureReport report)
    {
        return string.Join(
            Environment.NewLine,
            "| Field | Value |",
            "| --- | --- |",
            $"| URL | {CaptureReport.NormalizeMarkdownLine(report.PageUrl)} |",
            $"| Page | {CaptureReport.NormalizeMarkdownLine(report.PageTitle)} |",
            $"| Browser | {CaptureReport.NormalizeMarkdownLine(report.Browser)} |",
            $"| Profile | {CaptureReport.NormalizeMarkdownLine(report.UsageProfile)} |",
            $"| Element | `{CaptureReport.NormalizeMarkdownLine(report.TagName)}` |",
            $"| Selector | `{CaptureReport.NormalizeMarkdownLine(report.Selector)}` |",
            $"| Lens frame | {report.Frame.Width:0}x{report.Frame.Height:0} at {report.Frame.X:0},{report.Frame.Y:0} |",
            $"| Center | {report.Frame.CenterX:0},{report.Frame.CenterY:0} |",
            $"| Screen | {CaptureReport.NormalizeMarkdownLine(report.Frame.ScreenName)} {report.Frame.ScreenWidth}x{report.Frame.ScreenHeight} |",
            $"| Images detected | {report.Images.Count} |");
    }

    private static string BuildJiraDetails(CaptureReport report)
    {
        return string.Join(
            Environment.NewLine,
            $"* URL: {report.PageUrl}",
            $"* Page: {report.PageTitle}",
            $"* Browser: {report.Browser}",
            $"* Profile: {report.UsageProfile}",
            $"* Element: {report.TagName}",
            $"* Selector: {{code}}{report.Selector}{{code}}",
            $"* Lens frame: {report.Frame.Width:0}x{report.Frame.Height:0} at {report.Frame.X:0},{report.Frame.Y:0}",
            $"* Center: {report.Frame.CenterX:0},{report.Frame.CenterY:0}",
            $"* Screen: {report.Frame.ScreenName} {report.Frame.ScreenWidth}x{report.Frame.ScreenHeight}",
            $"* Images detected: {report.Images.Count}");
    }

    private static string BuildPlainDetails(CaptureReport report)
    {
        return string.Join(
            Environment.NewLine,
            $"URL: {report.PageUrl}",
            $"Page: {report.PageTitle}",
            $"Browser: {report.Browser}",
            $"Profile: {report.UsageProfile}",
            $"Element: {report.TagName}",
            $"Selector: {report.Selector}",
            $"Lens frame: {report.Frame.Width:0}x{report.Frame.Height:0} at {report.Frame.X:0},{report.Frame.Y:0}",
            $"Center: {report.Frame.CenterX:0},{report.Frame.CenterY:0}",
            $"Screen: {report.Frame.ScreenName} {report.Frame.ScreenWidth}x{report.Frame.ScreenHeight}",
            $"Images detected: {report.Images.Count}");
    }

    private static string BuildNotes(CaptureReport report)
    {
        if (!report.Notes.HasContent)
        {
            return "No notes added.";
        }

        return string.Join(
            Environment.NewLine,
            $"- Category: {report.Notes.Category}",
            $"- Severity: {report.Notes.Severity}",
            $"- Status: {report.Notes.Status}",
            $"- Tags: {CaptureReport.NormalizeMarkdownLine(report.Notes.Tags)}",
            string.Empty,
            report.Notes.Observation.Trim());
    }

    private static string RelativeEvidencePath(string relativePath)
    {
        return relativePath.Replace('\\', '/');
    }

    private static string Shorten(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length <= maxLength)
        {
            return value ?? string.Empty;
        }

        return value[..Math.Max(0, maxLength - 3)] + "...";
    }
}
