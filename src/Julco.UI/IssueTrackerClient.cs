using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Julco.Core.Configuration;

namespace Julco.UI;

public sealed class IssueTrackerClient
{
    private readonly HttpClient _httpClient;

    public IssueTrackerClient(HttpClient? httpClient = null)
    {
        _httpClient = httpClient ?? new HttpClient();
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Julco/1.0");
    }

    public async Task<IssueTrackerSubmissionResult> SubmitAsync(
        IssueTrackerDraft draft,
        IssueTrackerSettings settings,
        CancellationToken cancellationToken)
    {
        var normalized = settings.Normalized();
        return draft.Provider switch
        {
            IssueTrackerProvider.GitHub => await SubmitGitHubAsync(draft, normalized, cancellationToken),
            IssueTrackerProvider.Jira => await SubmitJiraAsync(draft, normalized, cancellationToken),
            _ => IssueTrackerSubmissionResult.Failure(draft.Name, "Generic drafts are local-only.")
        };
    }

    public static bool CanSubmit(IssueTrackerDraft draft, IssueTrackerSettings settings)
    {
        var normalized = settings.Normalized();
        return draft.Provider switch
        {
            IssueTrackerProvider.GitHub => normalized.IsGitHubConfigured,
            IssueTrackerProvider.Jira => normalized.IsJiraConfigured,
            _ => false
        };
    }

    public static string BuildConfigurationHint(IssueTrackerDraft draft, IssueTrackerSettings settings)
    {
        var normalized = settings.Normalized();
        return draft.Provider switch
        {
            IssueTrackerProvider.GitHub when normalized.IsGitHubConfigured =>
                $"Ready to create an issue in {normalized.GitHubOwner}/{normalized.GitHubRepository}.",
            IssueTrackerProvider.GitHub =>
                "Enable GitHub in Settings, then add owner, repository, and a token or JULCO_GITHUB_TOKEN.",
            IssueTrackerProvider.Jira when normalized.IsJiraConfigured =>
                $"Ready to create a {normalized.JiraIssueType} in {normalized.JiraProjectKey}.",
            IssueTrackerProvider.Jira =>
                "Enable Jira in Settings, then add base URL, project key, email, and API token or JULCO_JIRA_API_TOKEN.",
            _ => "Generic tickets can be copied locally, but cannot be submitted."
        };
    }

    private async Task<IssueTrackerSubmissionResult> SubmitGitHubAsync(
        IssueTrackerDraft draft,
        IssueTrackerSettings settings,
        CancellationToken cancellationToken)
    {
        if (!settings.IsGitHubConfigured)
        {
            return IssueTrackerSubmissionResult.Failure(draft.Name, BuildConfigurationHint(draft, settings));
        }

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"https://api.github.com/repos/{Uri.EscapeDataString(settings.GitHubOwner)}/{Uri.EscapeDataString(settings.GitHubRepository)}/issues");
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", settings.ResolveGitHubToken());
        request.Content = JsonContent(new
        {
            title = draft.Title,
            body = draft.Body,
            labels = settings.GitHubLabelList
        });

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        var payload = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return IssueTrackerSubmissionResult.Failure(
                draft.Name,
                $"GitHub rejected the request ({(int)response.StatusCode}): {ExtractJsonMessage(payload)}");
        }

        using var document = JsonDocument.Parse(payload);
        var url = document.RootElement.TryGetProperty("html_url", out var urlElement)
            ? urlElement.GetString()
            : null;
        var number = document.RootElement.TryGetProperty("number", out var numberElement)
            ? numberElement.ToString()
            : "created";
        return IssueTrackerSubmissionResult.Success(draft.Name, $"GitHub issue #{number} created.", url);
    }

    private async Task<IssueTrackerSubmissionResult> SubmitJiraAsync(
        IssueTrackerDraft draft,
        IssueTrackerSettings settings,
        CancellationToken cancellationToken)
    {
        if (!settings.IsJiraConfigured)
        {
            return IssueTrackerSubmissionResult.Failure(draft.Name, BuildConfigurationHint(draft, settings));
        }

        var baseUrl = settings.JiraBaseUrl.TrimEnd('/');
        using var request = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl}/rest/api/3/issue");
        var authBytes = Encoding.UTF8.GetBytes($"{settings.JiraEmail}:{settings.ResolveJiraApiToken()}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(authBytes));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Content = JsonContent(new
        {
            fields = new
            {
                project = new { key = settings.JiraProjectKey },
                summary = draft.Title,
                issuetype = new { name = settings.JiraIssueType },
                description = BuildJiraDescription(draft.Body)
            }
        });

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        var payload = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return IssueTrackerSubmissionResult.Failure(
                draft.Name,
                $"Jira rejected the request ({(int)response.StatusCode}): {ExtractJsonMessage(payload)}");
        }

        using var document = JsonDocument.Parse(payload);
        var key = document.RootElement.TryGetProperty("key", out var keyElement)
            ? keyElement.GetString()
            : null;
        var url = string.IsNullOrWhiteSpace(key) ? null : $"{baseUrl}/browse/{key}";
        return IssueTrackerSubmissionResult.Success(draft.Name, $"Jira issue {key ?? "created"} created.", url);
    }

    private static StringContent JsonContent<T>(T value)
    {
        return new StringContent(
            JsonSerializer.Serialize(value),
            Encoding.UTF8,
            "application/json");
    }

    private static object BuildJiraDescription(string body)
    {
        var content = body
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Split('\n')
            .Select(line => new
            {
                type = "paragraph",
                content = string.IsNullOrWhiteSpace(line)
                    ? Array.Empty<object>()
                    : new object[] { new { type = "text", text = line } }
            })
            .Cast<object>()
            .ToArray();

        return new
        {
            type = "doc",
            version = 1,
            content
        };
    }

    private static string ExtractJsonMessage(string payload)
    {
        if (string.IsNullOrWhiteSpace(payload))
        {
            return "empty response";
        }

        try
        {
            using var document = JsonDocument.Parse(payload);
            if (document.RootElement.TryGetProperty("message", out var message))
            {
                return message.GetString() ?? payload;
            }

            if (document.RootElement.TryGetProperty("errorMessages", out var errors)
                && errors.ValueKind == JsonValueKind.Array)
            {
                var messages = errors
                    .EnumerateArray()
                    .Select(error => error.GetString())
                    .Where(error => !string.IsNullOrWhiteSpace(error));
                return string.Join("; ", messages);
            }
        }
        catch (JsonException)
        {
        }

        return payload.Length > 500 ? payload[..500] + "..." : payload;
    }
}
