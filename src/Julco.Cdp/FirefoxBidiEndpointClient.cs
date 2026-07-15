using System.Text.Json;

namespace Julco.Cdp;

public sealed class FirefoxBidiEndpointClient
{
    public async Task<IReadOnlyList<CdpTarget>> GetPageTargetsAsync(
        int remoteDebuggingPort,
        CancellationToken cancellationToken)
    {
        var webSocketUri = new Uri($"ws://127.0.0.1:{remoteDebuggingPort}/session");
        await using var connection = new FirefoxBidiConnection();
        await connection.ConnectAsync(webSocketUri, cancellationToken);

        var tree = await connection.SendAsync("browsingContext.getTree", new { }, cancellationToken);
        if (!tree.TryGetProperty("contexts", out var contexts))
        {
            return Array.Empty<CdpTarget>();
        }

        var targets = new List<CdpTarget>();
        foreach (var context in contexts.EnumerateArray())
        {
            AddContext(context, webSocketUri.ToString(), targets);
        }

        return targets
            .Where(target => target.IsInspectableWebPage)
            .OrderBy(target => target.Title)
            .ToArray();
    }

    private static void AddContext(JsonElement context, string webSocketUri, List<CdpTarget> targets)
    {
        var contextId = context.GetProperty("context").GetString() ?? string.Empty;
        var url = context.TryGetProperty("url", out var urlProperty)
            ? urlProperty.GetString() ?? string.Empty
            : string.Empty;

        targets.Add(new CdpTarget(
            contextId,
            "firefox-page",
            "Firefox tab",
            url,
            webSocketUri));

        if (!context.TryGetProperty("children", out var children) || children.ValueKind != JsonValueKind.Array)
        {
            return;
        }

        foreach (var child in children.EnumerateArray())
        {
            AddContext(child, webSocketUri, targets);
        }
    }
}
