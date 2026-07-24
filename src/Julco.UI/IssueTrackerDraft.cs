namespace Julco.UI;

public sealed record IssueTrackerDraft(
    string Name,
    string Title,
    string Body,
    string FilePath);
