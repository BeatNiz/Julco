namespace Julco.Core.Configuration;

public sealed record IssueTrackerSettings(
    bool EnableGitHub,
    string GitHubOwner,
    string GitHubRepository,
    string GitHubToken,
    string GitHubLabels,
    string GitHubAssignees,
    string GitHubMilestone,
    bool EnableJira,
    string JiraBaseUrl,
    string JiraProjectKey,
    string JiraIssueType,
    string JiraPriority,
    string JiraEmail,
    string JiraApiToken)
{
    public static IssueTrackerSettings Default { get; } = new(
        EnableGitHub: false,
        GitHubOwner: string.Empty,
        GitHubRepository: string.Empty,
        GitHubToken: string.Empty,
        GitHubLabels: "julco,evidence",
        GitHubAssignees: string.Empty,
        GitHubMilestone: string.Empty,
        EnableJira: false,
        JiraBaseUrl: string.Empty,
        JiraProjectKey: string.Empty,
        JiraIssueType: "Bug",
        JiraPriority: string.Empty,
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
            GitHubAssignees = Normalize(GitHubAssignees),
            GitHubMilestone = Normalize(GitHubMilestone),
            JiraBaseUrl = Normalize(JiraBaseUrl).TrimEnd('/'),
            JiraProjectKey = Normalize(JiraProjectKey).ToUpperInvariant(),
            JiraIssueType = string.IsNullOrWhiteSpace(JiraIssueType)
                ? Default.JiraIssueType
                : JiraIssueType.Trim(),
            JiraPriority = Normalize(JiraPriority),
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
        SplitList(GitHubLabels);

    public IReadOnlyList<string> GitHubAssigneeList =>
        SplitList(GitHubAssignees);

    public int? GitHubMilestoneNumber =>
        int.TryParse(GitHubMilestone, out var milestone) && milestone > 0
            ? milestone
            : null;

    public IssueTrackerSettings WithProtectedSecrets()
    {
        return Normalized() with
        {
            GitHubToken = SecretProtector.Protect(GitHubToken),
            JiraApiToken = SecretProtector.Protect(JiraApiToken)
        };
    }

    public string ResolveGitHubToken()
    {
        return string.IsNullOrWhiteSpace(GitHubToken)
            ? Environment.GetEnvironmentVariable("JULCO_GITHUB_TOKEN") ?? string.Empty
            : SecretProtector.Unprotect(GitHubToken);
    }

    public string ResolveJiraApiToken()
    {
        return string.IsNullOrWhiteSpace(JiraApiToken)
            ? Environment.GetEnvironmentVariable("JULCO_JIRA_API_TOKEN") ?? string.Empty
            : SecretProtector.Unprotect(JiraApiToken);
    }

    private static IReadOnlyList<string> SplitList(string value)
    {
        return value
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static string Normalize(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }
}
