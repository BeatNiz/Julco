using System.IO;
using System.Text;
using Julco.Core.Privacy;

namespace Julco.UI;

public sealed class ReportWorkflowService
{
    public string ExportCaptureReport(
        string captureDirectory,
        string usageProfile,
        PrivacyRedactorOptions privacyOptions)
    {
        var report = CaptureReport.FromDirectory(captureDirectory, usageProfile)
            .Redacted(privacyOptions);
        var reportDirectory = Path.Combine(captureDirectory, "report");
        Directory.CreateDirectory(reportDirectory);

        File.WriteAllText(Path.Combine(reportDirectory, "report.md"), report.BuildMarkdown(), Encoding.UTF8);
        File.WriteAllText(Path.Combine(reportDirectory, "report.html"), report.BuildHtml(), Encoding.UTF8);
        SimplePdfReportWriter.Write(Path.Combine(reportDirectory, "report.pdf"), report);

        return reportDirectory;
    }

    public string ExportSafePackage(PrivacyPreviewModel model)
    {
        var safeDirectory = Path.Combine(
            model.Original.CaptureDirectory,
            $"privacy-safe-{DateTime.Now:yyyyMMdd-HHmmss}");
        Directory.CreateDirectory(safeDirectory);

        var safeReport = model.Redacted with { ScreenshotPath = string.Empty };
        File.WriteAllText(Path.Combine(safeDirectory, "safe-report.md"), safeReport.BuildMarkdown(), Encoding.UTF8);
        File.WriteAllText(Path.Combine(safeDirectory, "safe-report.html"), safeReport.BuildHtml(), Encoding.UTF8);
        SimplePdfReportWriter.Write(Path.Combine(safeDirectory, "safe-report.pdf"), safeReport);

        File.WriteAllText(
            Path.Combine(safeDirectory, "privacy-summary.md"),
            BuildPrivacySummaryMarkdown(model),
            Encoding.UTF8);
        File.WriteAllText(Path.Combine(safeDirectory, "dom.safe.html"), safeReport.Dom, Encoding.UTF8);
        File.WriteAllText(Path.Combine(safeDirectory, "computed.safe.css"), safeReport.ComputedCss, Encoding.UTF8);
        File.WriteAllText(Path.Combine(safeDirectory, "console.safe.txt"), safeReport.Console, Encoding.UTF8);
        File.WriteAllText(Path.Combine(safeDirectory, "attributes.safe.txt"), safeReport.Attributes, Encoding.UTF8);
        File.WriteAllText(
            Path.Combine(safeDirectory, "images.safe.json"),
            System.Text.Json.JsonSerializer.Serialize(safeReport.Images, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }),
            Encoding.UTF8);

        if (model.IncludeScreenshotInSafeExport && File.Exists(model.Original.ScreenshotPath))
        {
            File.Copy(model.Original.ScreenshotPath, Path.Combine(safeDirectory, "screenshot-unredacted.png"), overwrite: true);
        }

        return safeDirectory;
    }

    private static string BuildPrivacySummaryMarkdown(PrivacyPreviewModel model)
    {
        return string.Join(
            Environment.NewLine,
            new[]
            {
                "# Julco Privacy Summary",
                string.Empty,
                model.SummaryText,
                string.Empty,
                "## Policy",
                string.Empty,
                model.IncludeScreenshotInSafeExport
                    ? "Screenshot was included because Settings allows screenshots in safe exports. Review visual content before sharing."
                    : "Screenshot was omitted because safe exports do not include visual captures by default.",
                string.Empty,
                "## Source",
                string.Empty,
                $"- Capture: {model.Original.CaptureDirectory}",
                $"- Page: {model.Redacted.PageTitle}",
                $"- URL: {model.Redacted.PageUrl}",
                $"- Selector: {model.Redacted.Selector}"
            });
    }
}
