using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;

namespace Julco.Cdp;

public sealed class CdpConnection : IAsyncDisposable
{
    private readonly ClientWebSocket _webSocket = new();
    private readonly ConcurrentDictionary<int, TaskCompletionSource<JsonElement>> _pendingCommands = new();
    private readonly List<string> _consoleMessages = new();
    private readonly CancellationTokenSource _disposeCts = new();
    private int _nextId;
    private Task? _receiveLoop;

    public IReadOnlyList<string> ConsoleMessages
    {
        get
        {
            lock (_consoleMessages)
            {
                return _consoleMessages.ToArray();
            }
        }
    }

    public async Task ConnectAsync(Uri webSocketUri, CancellationToken cancellationToken)
    {
        var uri = NormalizeWebSocketUri(webSocketUri);
        await _webSocket.ConnectAsync(uri, cancellationToken);
        _receiveLoop = Task.Run(() => ReceiveLoopAsync(_disposeCts.Token));
    }

    public async Task<JsonElement> SendAsync(
        string method,
        object? parameters,
        CancellationToken cancellationToken)
    {
        var id = Interlocked.Increment(ref _nextId);
        var message = parameters is null
            ? JsonSerializer.Serialize(new { id, method })
            : JsonSerializer.Serialize(new { id, method, @params = parameters });

        var completion = new TaskCompletionSource<JsonElement>(TaskCreationOptions.RunContinuationsAsynchronously);
        _pendingCommands[id] = completion;

        var bytes = Encoding.UTF8.GetBytes(message);
        await _webSocket.SendAsync(bytes, WebSocketMessageType.Text, true, cancellationToken);

        await using var registration = cancellationToken.Register(() => completion.TrySetCanceled(cancellationToken));
        return await completion.Task;
    }

    public async ValueTask DisposeAsync()
    {
        _disposeCts.Cancel();

        if (_webSocket.State is WebSocketState.Open or WebSocketState.CloseReceived)
        {
            await _webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Julco closed", CancellationToken.None);
        }

        if (_receiveLoop is not null)
        {
            try
            {
                await _receiveLoop;
            }
            catch (OperationCanceledException)
            {
            }
        }

        _webSocket.Dispose();
        _disposeCts.Dispose();
    }

    private async Task ReceiveLoopAsync(CancellationToken cancellationToken)
    {
        var buffer = new byte[64 * 1024];

        while (!cancellationToken.IsCancellationRequested && _webSocket.State == WebSocketState.Open)
        {
            using var stream = new MemoryStream();
            WebSocketReceiveResult result;

            do
            {
                result = await _webSocket.ReceiveAsync(buffer, cancellationToken);
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    return;
                }

                stream.Write(buffer, 0, result.Count);
            }
            while (!result.EndOfMessage);

            var json = Encoding.UTF8.GetString(stream.ToArray());
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement.Clone();

            if (root.TryGetProperty("id", out var idProperty)
                && _pendingCommands.TryRemove(idProperty.GetInt32(), out var completion))
            {
                if (root.TryGetProperty("error", out var error))
                {
                    completion.TrySetException(new InvalidOperationException(error.ToString()));
                }
                else if (root.TryGetProperty("result", out var commandResult))
                {
                    completion.TrySetResult(commandResult.Clone());
                }
                else
                {
                    completion.TrySetResult(root);
                }
            }
            else
            {
                CaptureEvent(root);
            }
        }
    }

    private void CaptureEvent(JsonElement root)
    {
        if (!root.TryGetProperty("method", out var methodProperty))
        {
            return;
        }

        var method = methodProperty.GetString();
        var message = method switch
        {
            "Runtime.consoleAPICalled" => FormatConsoleApi(root),
            "Runtime.exceptionThrown" => FormatException(root),
            "Log.entryAdded" => FormatLogEntry(root),
            _ => null
        };

        if (message is null)
        {
            return;
        }

        lock (_consoleMessages)
        {
            _consoleMessages.Add(message);
        }
    }

    private static string? FormatConsoleApi(JsonElement root)
    {
        var parameters = root.GetProperty("params");
        var type = parameters.GetProperty("type").GetString() ?? "log";
        var args = parameters.GetProperty("args")
            .EnumerateArray()
            .Select(FormatRemoteObject);

        return $"[{type}] {string.Join(" ", args)}";
    }

    private static string? FormatException(JsonElement root)
    {
        var details = root.GetProperty("params").GetProperty("exceptionDetails");
        var text = details.GetProperty("text").GetString();
        return $"[exception] {text}";
    }

    private static string? FormatLogEntry(JsonElement root)
    {
        var entry = root.GetProperty("params").GetProperty("entry");
        var level = entry.GetProperty("level").GetString();
        var text = entry.GetProperty("text").GetString();
        return $"[{level}] {text}";
    }

    private static string FormatRemoteObject(JsonElement value)
    {
        if (value.TryGetProperty("value", out var rawValue))
        {
            return rawValue.ValueKind == JsonValueKind.String
                ? rawValue.GetString() ?? string.Empty
                : rawValue.ToString();
        }

        if (value.TryGetProperty("description", out var description))
        {
            return description.GetString() ?? string.Empty;
        }

        return value.ToString();
    }

    private static Uri NormalizeWebSocketUri(Uri uri)
    {
        if (!uri.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase))
        {
            return uri;
        }

        var builder = new UriBuilder(uri)
        {
            Host = "127.0.0.1"
        };

        return builder.Uri;
    }
}
