using System.IO;
using System.Text;
using Julco.Core.Configuration;
using Julco.Core.Privacy;

namespace Julco.UI;

public sealed class IssueTrackerWorkflowService
{
    public IssueTrackerWorkflowResult BuildDrafts(
        string captureDirectory,
        string usageProfile,
        PrivacySettings privacySettings,
        IssueTrackerSettings settings)
    {
        privacySettings = privacySettings.Normalized();
        var privacyOptions = PrivacyRedactorOptions.FromSettings(privacySettings.SafeIssueTrackersByDefault
            ? privacySettings with
            {
                RedactOnExport = true,
                RedactEmails = true,
                RedactTokens = true,
                RedactCookies = true,
                RedactPrivateUrls = true
            }
            : privacySettings);
        var report = CaptureReport.FromDirectory(captureDirectory, usageProfile)
            .Redacted(privacyOptions);
        if (privacySettings.SafeIssueTrackersByDefault || !privacySettings.IncludeScreenshotsInSafeExports)
        {
            report = report with { ScreenshotPath = string.Empty };
        }

        var outputDirectory = Path.Combine(captureDirectory, "issue-trackers");
        Directory.CreateDirectory(outputDirectory);

        var drafts = IssueTrackerDraftFactory.Create(report, outputDirectory);
        foreach (var draft in drafts)
        {
            File.WriteAllText(
                draft.FilePath,
                $"{draft.Title}{Environment.NewLine}{Environment.NewLine}{draft.Body}",
                Encoding.UTF8);
        }

        var original = CaptureReport.FromDirectory(captureDirectory, usageProfile);
        var privacyPreview = PrivacyPreviewModel.Create(
            original,
            privacyOptions,
            privacySettings.IncludeScreenshotsInSafeExports,
            privacySettings.BlurScreenshotsInSafeExports || !string.IsNullOrWhiteSpace(privacySettings.ScreenshotRedactionBoxes),
            privacySettings);

        return new IssueTrackerWorkflowResult(drafts, outputDirectory, settings.Normalized(), privacyPreview);
    }
}

public sealed record IssueTrackerWorkflowResult(
    IReadOnlyList<IssueTrackerDraft> Drafts,
    string OutputDirectory,
    IssueTrackerSettings Settings,
    PrivacyPreviewModel PrivacyPreview);
