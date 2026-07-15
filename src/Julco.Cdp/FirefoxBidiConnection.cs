using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;

namespace Julco.Cdp;

public sealed class FirefoxBidiConnection : IAsyncDisposable
{
    private readonly ClientWebSocket _webSocket = new();
    private readonly ConcurrentDictionary<int, TaskCompletionSource<JsonElement>> _pendingCommands = new();
    private readonly List<string> _consoleMessages = new();
    private readonly CancellationTokenSource _disposeCts = new();
    private int _nextId;
    private Task? _receiveLoop;
    private bool _sessionStarted;

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
        await _webSocket.ConnectAsync(webSocketUri, cancellationToken);
        _receiveLoop = Task.Run(() => ReceiveLoopAsync(_disposeCts.Token));
        await SendAsync("session.new", new { capabilities = new { } }, cancellationToken);
        _sessionStarted = true;
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

    public async Task<string> EvaluateJsonAsync(
        string contextId,
        string expression,
        CancellationToken cancellationToken)
    {
        var result = await SendAsync(
            "script.evaluate",
            new
            {
                expression,
                target = new { context = contextId },
                awaitPromise = true,
                resultOwnership = "none"
            },
            cancellationToken);

        if (!result.TryGetProperty("result", out var remoteValue))
        {
            throw new InvalidOperationException("Firefox BiDi did not return a script result.");
        }

        if (remoteValue.TryGetProperty("type", out var resultType)
            && resultType.GetString() == "exception")
        {
            throw new InvalidOperationException(remoteValue.ToString());
        }

        if (!remoteValue.TryGetProperty("value", out var value) || value.ValueKind != JsonValueKind.String)
        {
            throw new InvalidOperationException("Firefox BiDi returned a non-string script result.");
        }

        return value.GetString() ?? string.Empty;
    }

    public async ValueTask DisposeAsync()
    {
        if (_sessionStarted && _webSocket.State == WebSocketState.Open)
        {
            try
            {
                await SendAsync("session.end", new { }, CancellationToken.None);
            }
            catch (InvalidOperationException)
            {
            }
            catch (WebSocketException)
            {
            }
        }

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
        var buffer = new byte[256 * 1024];

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

            if (!root.TryGetProperty("id", out var idProperty)
                || !_pendingCommands.TryRemove(idProperty.GetInt32(), out var completion))
            {
                CaptureEvent(root);
                continue;
            }

            if (root.TryGetProperty("type", out var type) && type.GetString() == "error")
            {
                var message = root.TryGetProperty("message", out var messageProperty)
                    ? messageProperty.GetString()
                    : root.ToString();
                completion.TrySetException(new InvalidOperationException(message));
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
    }

    private void CaptureEvent(JsonElement root)
    {
        if (!root.TryGetProperty("method", out var methodProperty)
            || methodProperty.GetString() != "log.entryAdded"
            || !root.TryGetProperty("params", out var parameters))
        {
            return;
        }

        var level = parameters.TryGetProperty("level", out var levelProperty)
            ? levelProperty.GetString() ?? "log"
            : "log";

        var text = parameters.TryGetProperty("text", out var textProperty)
            ? textProperty.GetString()
            : null;

        if (string.IsNullOrWhiteSpace(text) && parameters.TryGetProperty("args", out var args))
        {
            text = string.Join(" ", args.EnumerateArray().Select(FormatRemoteValue));
        }

        if (string.IsNullOrWhiteSpace(text))
        {
            text = parameters.ToString();
        }

        lock (_consoleMessages)
        {
            _consoleMessages.Add($"[{level}] {text}");
        }
    }

    private static string FormatRemoteValue(JsonElement value)
    {
        if (value.TryGetProperty("value", out var rawValue))
        {
            return rawValue.ValueKind == JsonValueKind.String
                ? rawValue.GetString() ?? string.Empty
                : rawValue.ToString();
        }

        if (value.TryGetProperty("type", out var type))
        {
            return type.GetString() ?? value.ToString();
        }

        return value.ToString();
    }
}
