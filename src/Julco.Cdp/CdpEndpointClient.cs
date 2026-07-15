using System.Net.Http.Json;

namespace Julco.Cdp;

public sealed class CdpEndpointClient
{
    private readonly HttpClient _httpClient = new();

    public async Task<IReadOnlyList<CdpTarget>> GetPageTargetsAsync(
        int remoteDebuggingPort,
        CancellationToken cancellationToken)
    {
        var uri = new Uri($"http://127.0.0.1:{remoteDebuggingPort}/json/list");
        var targets = await _httpClient.GetFromJsonAsync<List<CdpTarget>>(uri, cancellationToken)
            ?? new List<CdpTarget>();

        return targets
            .Where(target => target.Type == "page")
            .Where(target => target.IsInspectableWebPage)
            .OrderBy(target => target.Url.StartsWith("https://example.com", StringComparison.OrdinalIgnoreCase) ? 0 : 1)
            .ThenBy(target => target.Title)
            .ToArray();
    }
}
