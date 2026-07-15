using System.Text.Json.Serialization;

namespace Julco.Cdp;

public sealed record CdpTarget(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("url")] string Url,
    [property: JsonPropertyName("webSocketDebuggerUrl")] string? WebSocketDebuggerUrl)
{
    public string DisplayName
    {
        get
        {
            var title = string.IsNullOrWhiteSpace(Title) ? "(untitled)" : Title;
            return string.IsNullOrWhiteSpace(Url)
                ? title
                : $"{title} - {Url}";
        }
    }

    public bool IsInspectableWebPage
    {
        get
        {
            if (string.IsNullOrWhiteSpace(WebSocketDebuggerUrl)
                || string.IsNullOrWhiteSpace(Url)
                || !Uri.TryCreate(Url, UriKind.Absolute, out var uri))
            {
                return false;
            }

            return uri.Scheme is "http" or "https" or "file";
        }
    }
}
