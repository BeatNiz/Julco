namespace Julco.Core.Configuration;

public sealed record IssueTrackerSettings(
    bool EnableGitHub,
    string GitHubOwner,
    string GitHubRepository,
    string GitHubToken,
    string GitHubLabels,
    bool EnableJira,
    string JiraBaseUrl,
    string JiraProjectKey,
    string JiraIssueType,
    string JiraEmail,
    string JiraApiToken)
{
    public static IssueTrackerSettings Default { get; } = new(
        EnableGitHub: false,
        GitHubOwner: string.Empty,
        GitHubRepository: string.Empty,
        GitHubToken: string.Empty,
        GitHubLabels: "julco,evidence",
        EnableJira: false,
        JiraBaseUrl: string.Empty,
        JiraProjectKey: string.Empty,
        JiraIssueType: "Bug",
        JiraEmail: string.Empty,
        JiraApiToken: string.Empty);

    public IssueTrackerSettings Normalized()
    {
        return this with
        {
            GitHubOwner = Normalize(GitHubOwner),
            GitHubRepository = Normalize(GitHubRepository),
            GitHubToken = Normalize(GitHubToken),
            GitHubLabels = string.IsNullOrWhiteSpace(GitHubLabels)
                ? Default.GitHubLabels
                : GitHubLabels.Trim(),
            JiraBaseUrl = Normalize(JiraBaseUrl).TrimEnd('/'),
            JiraProjectKey = Normalize(JiraProjectKey).ToUpperInvariant(),
            JiraIssueType = string.IsNullOrWhiteSpace(JiraIssueType)
                ? Default.JiraIssueType
                : JiraIssueType.Trim(),
            JiraEmail = Normalize(JiraEmail),
            JiraApiToken = Normalize(JiraApiToken)
        };
    }

    public bool IsGitHubConfigured =>
        EnableGitHub
        && !string.IsNullOrWhiteSpace(GitHubOwner)
        && !string.IsNullOrWhiteSpace(GitHubRepository)
        && !string.IsNullOrWhiteSpace(ResolveGitHubToken());

    public bool IsJiraConfigured =>
        EnableJira
        && !string.IsNullOrWhiteSpace(JiraBaseUrl)
        && !string.IsNullOrWhiteSpace(JiraProjectKey)
        && !string.IsNullOrWhiteSpace(JiraEmail)
        && !string.IsNullOrWhiteSpace(ResolveJiraApiToken());

    public IReadOnlyList<string> GitHubLabelList =>
        GitHubLabels
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(label => !string.IsNullOrWhiteSpace(label))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

    public string ResolveGitHubToken()
    {
        return string.IsNullOrWhiteSpace(GitHubToken)
            ? Environment.GetEnvironmentVariable("JULCO_GITHUB_TOKEN") ?? string.Empty
            : GitHubToken;
    }

    public string ResolveJiraApiToken()
    {
        return string.IsNullOrWhiteSpace(JiraApiToken)
            ? Environment.GetEnvironmentVariable("JULCO_JIRA_API_TOKEN") ?? string.Empty
            : JiraApiToken;
    }

    private static string Normalize(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }
}
