namespace Julco.UI;

public sealed record IssueTrackerDraft(
    IssueTrackerProvider Provider,
    string Name,
    string Title,
    string Body,
    string FilePath);

public enum IssueTrackerProvider
{
    GitHub,
    Jira,
    Generic
}
