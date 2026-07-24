namespace Julco.UI;

public sealed record IssueTrackerSubmissionResult(
    bool Succeeded,
    string Provider,
    string Message,
    string? Url)
{
    public static IssueTrackerSubmissionResult Success(string provider, string message, string? url)
    {
        return new IssueTrackerSubmissionResult(true, provider, message, url);
    }

    public static IssueTrackerSubmissionResult Failure(string provider, string message)
    {
        return new IssueTrackerSubmissionResult(false, provider, message, null);
    }
}
