using System.Text.Json;

namespace Julco.Cdp;

public sealed class SelectorInspectionService
{
    public async Task<SelectorInspectionResult> InspectAsync(
        CdpTarget target,
        string selector,
        CancellationToken cancellationToken)
    {
        return await InspectWithRuntimeAsync(
            target,
            InspectionRuntime.BuildSelectorExpression(selector),
            $"No element matched selector '{selector}'.",
            cancellationToken);
    }

    public async Task<SelectorInspectionResult> InspectScreenPointAsync(
        CdpTarget target,
        double screenX,
        double screenY,
        double regionLeft,
        double regionTop,
        double regionWidth,
        double regionHeight,
        CancellationToken cancellationToken)
    {
        return await InspectWithRuntimeAsync(
            target,
            InspectionRuntime.BuildScreenPointExpression(
                screenX,
                screenY,
                regionLeft,
                regionTop,
                regionWidth,
                regionHeight),
            "No element found at lens center.",
            cancellationToken);
    }

    private static async Task<SelectorInspectionResult> InspectWithRuntimeAsync(
        CdpTarget target,
        string expression,
        string missingElementFallbackMessage,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(target.WebSocketDebuggerUrl))
        {
            throw new InvalidOperationException("The selected target does not expose a CDP WebSocket URL.");
        }

        await using var connection = new CdpConnection();
        await connection.ConnectAsync(new Uri(target.WebSocketDebuggerUrl), cancellationToken);
        await EnableIfAvailableAsync(connection, "Runtime.enable", cancellationToken);
        await EnableIfAvailableAsync(connection, "Log.enable", cancellationToken);

        var result = await connection.SendAsync(
            "Runtime.evaluate",
            new
            {
                expression,
                returnByValue = true,
                awaitPromise = true,
                userGesture = false
            },
            cancellationToken);

        var runtimeResult = result.GetProperty("result");
        if (runtimeResult.TryGetProperty("subtype", out var subtype) && subtype.GetString() == "error")
        {
            throw new InvalidOperationException(runtimeResult.ToString());
        }

        if (!runtimeResult.TryGetProperty("value", out var value) || value.ValueKind != JsonValueKind.String)
        {
            throw new InvalidOperationException("CDP returned a non-string inspection payload.");
        }

        return SelectorInspectionPayloadReader.Read(
            value.GetString() ?? "{}",
            connection.ConsoleMessages,
            missingElementFallbackMessage);
    }

    private static async Task EnableIfAvailableAsync(
        CdpConnection connection,
        string method,
        CancellationToken cancellationToken)
    {
        try
        {
            await connection.SendAsync(method, null, cancellationToken);
        }
        catch (InvalidOperationException)
        {
        }
    }
}
