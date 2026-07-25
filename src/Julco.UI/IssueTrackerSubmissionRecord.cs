namespace Julco.UI;

public sealed record IssueTrackerSubmissionRecord(
    DateTimeOffset CreatedAt,
    string Provider,
    string DraftTitle,
    bool Succeeded,
    string Message,
    string? Url,
    string OutputDirectory);
