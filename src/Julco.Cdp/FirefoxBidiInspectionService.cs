namespace Julco.Cdp;

public sealed class FirefoxBidiInspectionService
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
            throw new InvalidOperationException("The selected Firefox target does not expose a BiDi WebSocket URL.");
        }

        await using var connection = new FirefoxBidiConnection();
        await connection.ConnectAsync(new Uri(target.WebSocketDebuggerUrl), cancellationToken);
        await SubscribeToConsoleIfAvailableAsync(connection, target.Id, cancellationToken);

        var payload = await connection.EvaluateJsonAsync(target.Id, expression, cancellationToken);
        return SelectorInspectionPayloadReader.Read(
            payload,
            connection.ConsoleMessages,
            missingElementFallbackMessage);
    }

    private static async Task SubscribeToConsoleIfAvailableAsync(
        FirefoxBidiConnection connection,
        string contextId,
        CancellationToken cancellationToken)
    {
        try
        {
            await connection.SendAsync(
                "session.subscribe",
                new
                {
                    events = new[] { "log.entryAdded" },
                    contexts = new[] { contextId }
                },
                cancellationToken);
        }
        catch (InvalidOperationException)
        {
        }
    }
}
