using System.Text.Json;

namespace Julco.Cdp;

public sealed class SelectorInspectionService
{
    public async Task<SelectorInspectionResult> InspectAsync(
        CdpTarget target,
        string selector,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(target.WebSocketDebuggerUrl))
        {
            throw new InvalidOperationException("The selected target does not expose a CDP WebSocket URL.");
        }

        await using var connection = new CdpConnection();
        await connection.ConnectAsync(new Uri(target.WebSocketDebuggerUrl), cancellationToken);

        await EnableIfAvailableAsync(connection, "DOM.enable", cancellationToken);
        await EnableIfAvailableAsync(connection, "CSS.enable", cancellationToken);
        await EnableIfAvailableAsync(connection, "Runtime.enable", cancellationToken);
        await EnableIfAvailableAsync(connection, "Log.enable", cancellationToken);

        var document = await connection.SendAsync("DOM.getDocument", new { depth = 0, pierce = true }, cancellationToken);
        var rootNodeId = document.GetProperty("root").GetProperty("nodeId").GetInt32();

        var query = await connection.SendAsync(
            "DOM.querySelector",
            new { nodeId = rootNodeId, selector },
            cancellationToken);

        var nodeId = query.GetProperty("nodeId").GetInt32();
        if (nodeId == 0)
        {
            throw new InvalidOperationException($"No element matched selector '{selector}'.");
        }

        var description = await connection.SendAsync(
            "DOM.describeNode",
            new { nodeId, depth = 0, pierce = false },
            cancellationToken);

        var node = description.GetProperty("node");
        var tagName = node.GetProperty("nodeName").GetString() ?? string.Empty;
        var attributes = ReadAttributes(node);

        var html = await connection.SendAsync("DOM.getOuterHTML", new { nodeId }, cancellationToken);
        var outerHtml = html.GetProperty("outerHTML").GetString() ?? string.Empty;

        var computed = await connection.SendAsync("CSS.getComputedStyleForNode", new { nodeId }, cancellationToken);
        var computedStyle = computed.GetProperty("computedStyle")
            .EnumerateArray()
            .Where(item => item.TryGetProperty("name", out _) && item.TryGetProperty("value", out _))
            .ToDictionary(
                item => item.GetProperty("name").GetString() ?? string.Empty,
                item => item.GetProperty("value").GetString() ?? string.Empty);

        var matchedRules = await GetMatchedRulesAsync(connection, nodeId, cancellationToken);

        return new SelectorInspectionResult(
            selector,
            tagName,
            attributes,
            outerHtml,
            computedStyle,
            matchedRules,
            connection.ConsoleMessages);
    }

    public async Task<SelectorInspectionResult> InspectScreenPointAsync(
        CdpTarget target,
        double screenX,
        double screenY,
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

        var script = $$"""
            (() => {
                const screenXValue = {{screenX.ToString(System.Globalization.CultureInfo.InvariantCulture)}};
                const screenYValue = {{screenY.ToString(System.Globalization.CultureInfo.InvariantCulture)}};
                const chromeLeft = Math.max(0, (window.outerWidth - window.innerWidth) / 2);
                const chromeTop = Math.max(0, window.outerHeight - window.innerHeight - chromeLeft);
                const dpr = window.devicePixelRatio || 1;
                const candidates = [
                    { screenX: screenXValue, screenY: screenYValue, mode: "raw" },
                    { screenX: screenXValue / dpr, screenY: screenYValue / dpr, mode: "devicePixelRatio" }
                ];
                const hit = candidates
                    .map(candidate => {
                        const viewportX = candidate.screenX - window.screenX - chromeLeft;
                        const viewportY = candidate.screenY - window.screenY - chromeTop;
                        const inside = viewportX >= 0
                            && viewportY >= 0
                            && viewportX <= window.innerWidth
                            && viewportY <= window.innerHeight;
                        return {
                            ...candidate,
                            viewportX,
                            viewportY,
                            inside,
                            element: inside ? document.elementFromPoint(viewportX, viewportY) : null
                        };
                    })
                    .find(candidate => candidate.element)
                    ?? candidates
                        .map(candidate => {
                            const viewportX = candidate.screenX - window.screenX - chromeLeft;
                            const viewportY = candidate.screenY - window.screenY - chromeTop;
                            return {
                                ...candidate,
                                viewportX,
                                viewportY,
                                inside: false,
                                element: document.elementFromPoint(viewportX, viewportY)
                            };
                        })
                        .find(candidate => candidate.element);

                const element = hit?.element;
                const viewportX = hit?.viewportX ?? 0;
                const viewportY = hit?.viewportY ?? 0;

                if (!element) {
                    return {
                        found: false,
                        viewportX,
                        viewportY,
                        message: "No element at calculated viewport point."
                    };
                }

                const computed = getComputedStyle(element);
                const computedStyle = {};
                for (const name of computed) {
                    computedStyle[name] = computed.getPropertyValue(name);
                }

                const attributes = {};
                for (const attribute of element.attributes) {
                    attributes[attribute.name] = attribute.value;
                }

                const selector = (() => {
                    if (element.id) return `#${CSS.escape(element.id)}`;
                    const parts = [];
                    let current = element;
                    while (current && current.nodeType === Node.ELEMENT_NODE && parts.length < 5) {
                        let part = current.localName;
                        if (current.classList.length > 0) {
                            part += "." + Array.from(current.classList).slice(0, 2).map(CSS.escape).join(".");
                        }
                        const parent = current.parentElement;
                        if (parent) {
                            const siblings = Array.from(parent.children).filter(item => item.localName === current.localName);
                            if (siblings.length > 1) {
                                part += `:nth-of-type(${siblings.indexOf(current) + 1})`;
                            }
                        }
                        parts.unshift(part);
                        current = parent;
                    }
                    return parts.join(" > ");
                })();

                return {
                    found: true,
                    viewportX,
                    viewportY,
                    selector,
                    tagName: element.tagName,
                    attributes,
                    outerHtml: element.outerHTML,
                    computedStyle
                };
            })()
            """;

        var result = await connection.SendAsync(
            "Runtime.evaluate",
            new
            {
                expression = script,
                returnByValue = true,
                awaitPromise = true,
                userGesture = false
            },
            cancellationToken);

        var value = result.GetProperty("result").GetProperty("value");
        if (value.TryGetProperty("found", out var found) && !found.GetBoolean())
        {
            var message = value.TryGetProperty("message", out var messageValue)
                ? messageValue.GetString()
                : "No element found at lens center.";
            throw new InvalidOperationException(message);
        }

        var selector = value.GetProperty("selector").GetString() ?? "unknown";
        var tagName = value.GetProperty("tagName").GetString() ?? "unknown";
        var outerHtml = value.GetProperty("outerHtml").GetString() ?? string.Empty;
        var attributes = ReadObjectDictionary(value.GetProperty("attributes"));
        var computedStyle = ReadObjectDictionary(value.GetProperty("computedStyle"));

        return new SelectorInspectionResult(
            selector,
            tagName,
            attributes,
            outerHtml,
            computedStyle,
            Array.Empty<string>(),
            connection.ConsoleMessages);
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

    private static IReadOnlyDictionary<string, string> ReadAttributes(JsonElement node)
    {
        if (!node.TryGetProperty("attributes", out var attributesElement))
        {
            return new Dictionary<string, string>();
        }

        var values = attributesElement.EnumerateArray()
            .Select(item => item.GetString() ?? string.Empty)
            .ToArray();

        var attributes = new Dictionary<string, string>();
        for (var index = 0; index + 1 < values.Length; index += 2)
        {
            attributes[values[index]] = values[index + 1];
        }

        return attributes;
    }

    private static IReadOnlyDictionary<string, string> ReadObjectDictionary(JsonElement element)
    {
        return element.EnumerateObject()
            .ToDictionary(
                property => property.Name,
                property => property.Value.ValueKind == JsonValueKind.String
                    ? property.Value.GetString() ?? string.Empty
                    : property.Value.ToString());
    }

    private static async Task<IReadOnlyList<string>> GetMatchedRulesAsync(
        CdpConnection connection,
        int nodeId,
        CancellationToken cancellationToken)
    {
        try
        {
            var matched = await connection.SendAsync("CSS.getMatchedStylesForNode", new { nodeId }, cancellationToken);
            if (!matched.TryGetProperty("matchedCSSRules", out var rules))
            {
                return Array.Empty<string>();
            }

            return rules.EnumerateArray()
                .Select(item => item.GetProperty("rule").GetProperty("selectorList").GetProperty("text").GetString())
                .Where(text => !string.IsNullOrWhiteSpace(text))
                .Cast<string>()
                .Distinct()
                .ToArray();
        }
        catch (InvalidOperationException)
        {
            return Array.Empty<string>();
        }
    }
}
