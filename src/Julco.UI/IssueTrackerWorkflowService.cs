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
        PrivacyRedactorOptions privacyOptions,
        IssueTrackerSettings settings)
    {
        var report = CaptureReport.FromDirectory(captureDirectory, usageProfile)
            .Redacted(privacyOptions);
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

        return new IssueTrackerWorkflowResult(drafts, outputDirectory, settings.Normalized());
    }
}

public sealed record IssueTrackerWorkflowResult(
    IReadOnlyList<IssueTrackerDraft> Drafts,
    string OutputDirectory,
    IssueTrackerSettings Settings);
