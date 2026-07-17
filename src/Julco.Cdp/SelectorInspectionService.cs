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
            connection.ConsoleMessages,
            ExtractImagesFromHtml(outerHtml));
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
                const regionLeftValue = {{regionLeft.ToString(System.Globalization.CultureInfo.InvariantCulture)}};
                const regionTopValue = {{regionTop.ToString(System.Globalization.CultureInfo.InvariantCulture)}};
                const regionWidthValue = {{regionWidth.ToString(System.Globalization.CultureInfo.InvariantCulture)}};
                const regionHeightValue = {{regionHeight.ToString(System.Globalization.CultureInfo.InvariantCulture)}};
                const chromeLeft = Math.max(0, (window.outerWidth - window.innerWidth) / 2);
                const chromeTop = Math.max(0, window.outerHeight - window.innerHeight - chromeLeft);
                const dpr = window.devicePixelRatio || 1;
                const toViewport = (screenX, screenY) => ({
                    x: screenX - window.screenX - chromeLeft,
                    y: screenY - window.screenY - chromeTop
                });
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
                const regionTopLeft = toViewport(regionLeftValue, regionTopValue);
                const regionBottomRight = toViewport(regionLeftValue + regionWidthValue, regionTopValue + regionHeightValue);
                const region = {
                    left: Math.min(regionTopLeft.x, regionBottomRight.x),
                    top: Math.min(regionTopLeft.y, regionBottomRight.y),
                    right: Math.max(regionTopLeft.x, regionBottomRight.x),
                    bottom: Math.max(regionTopLeft.y, regionBottomRight.y)
                };

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

                const imageExtensions = [
                    "png", "jpg", "jpeg", "webp", "gif", "avif", "svg", "bmp", "ico",
                    "apng", "tif", "tiff", "jfif", "pjpeg", "pjp", "heic", "heif",
                    "jxl", "jp2", "j2k", "jpf", "jpx", "jpm", "mj2", "dds", "tga",
                    "psd", "raw", "cr2", "nef", "orf", "arw"
                ];
                const normalizeUrl = value => {
                    if (!value || typeof value !== "string") return "";
                    const trimmed = value.trim();
                    if (!trimmed || trimmed.startsWith("data:")) return trimmed;
                    try { return new URL(trimmed, document.baseURI).href; } catch { return trimmed; }
                };
                const formatFromUrl = url => {
                    if (!url) return "unknown";
                    const dataMatch = /^data:image\/([^;,]+)/i.exec(url);
                    if (dataMatch) return dataMatch[1].toLowerCase();
                    try {
                        const path = new URL(url, document.baseURI).pathname.toLowerCase();
                        const extension = path.includes(".") ? path.split(".").pop() : "";
                        return imageExtensions.includes(extension) ? extension : "unknown";
                    } catch {
                        const clean = url.split("?")[0].split("#")[0].toLowerCase();
                        const extension = clean.includes(".") ? clean.split(".").pop() : "";
                        return imageExtensions.includes(extension) ? extension : "unknown";
                    }
                };
                const images = [];
                const seenImages = new Map();
                const scoreImageElement = imageElement => {
                    if (!imageElement?.getBoundingClientRect) return 0;
                    const rect = imageElement.getBoundingClientRect();
                    const overlapWidth = Math.max(0, Math.min(rect.right, region.right) - Math.max(rect.left, region.left));
                    const overlapHeight = Math.max(0, Math.min(rect.bottom, region.bottom) - Math.max(rect.top, region.top));
                    const overlapArea = overlapWidth * overlapHeight;
                    const centerInside = viewportX >= rect.left && viewportX <= rect.right && viewportY >= rect.top && viewportY <= rect.bottom;
                    const elementBonus = imageElement === element ? 100000000 : 0;
                    const centerBonus = centerInside ? 10000000 : 0;
                    return elementBonus + centerBonus + overlapArea;
                };
                const addImage = (url, kind, imageElement, alt = "") => {
                    const normalized = normalizeUrl(url);
                    if (!normalized) return;
                    const format = formatFromUrl(normalized);
                    if (format === "unknown" && !normalized.startsWith("blob:")) return;
                    const image = {
                        url: normalized,
                        kind,
                        format,
                        alt: alt || imageElement?.getAttribute?.("alt") || imageElement?.getAttribute?.("aria-label") || "",
                        width: Math.round(imageElement?.naturalWidth || imageElement?.videoWidth || imageElement?.clientWidth || 0),
                        height: Math.round(imageElement?.naturalHeight || imageElement?.videoHeight || imageElement?.clientHeight || 0),
                        isAnimated: format === "gif" || format === "apng" || format === "webp",
                        priority: scoreImageElement(imageElement)
                    };
                    const existingIndex = seenImages.get(normalized);
                    if (existingIndex !== undefined) {
                        if ((images[existingIndex].priority || 0) < image.priority) {
                            images[existingIndex] = image;
                        }
                        return;
                    }

                    seenImages.set(normalized, images.length);
                    images.push(image);
                };
                const addSrcSet = (srcset, kind, imageElement) => {
                    if (!srcset) return;
                    for (const candidate of srcset.split(",")) {
                        addImage(candidate.trim().split(/\s+/)[0], kind, imageElement);
                    }
                };
                const addCssImages = (cssValue, imageElement) => {
                    if (!cssValue || cssValue === "none") return;
                    for (const match of cssValue.matchAll(/url\((['"]?)(.*?)\1\)/g)) {
                        addImage(match[2], "css-image", imageElement);
                    }
                };
                const imageRoots = Array.from(document.querySelectorAll("*")).filter(item => {
                    const rect = item.getBoundingClientRect();
                    if (rect.width <= 0 || rect.height <= 0) return item === element;
                    return rect.right >= region.left
                        && rect.left <= region.right
                        && rect.bottom >= region.top
                        && rect.top <= region.bottom;
                });
                if (!imageRoots.includes(element)) imageRoots.unshift(element);

                for (const item of imageRoots) {
                    const tag = item.localName;
                    if (tag === "img" || tag === "image") {
                        addImage(item.currentSrc || item.src || item.href?.baseVal, tag, item);
                        addSrcSet(item.srcset, "srcset", item);
                    }
                    if (tag === "source") {
                        addImage(item.src, "source", item);
                        addSrcSet(item.srcset, "source-srcset", item);
                    }
                    if (tag === "video") {
                        addImage(item.poster, "video-poster", item);
                    }
                    const style = getComputedStyle(item);
                    addCssImages(style.backgroundImage, item);
                    addCssImages(style.borderImageSource, item);
                    addCssImages(style.listStyleImage, item);
                    addCssImages(style.content, item);
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
                    computedStyle,
                    images: images.sort((a, b) => (b.priority || 0) - (a.priority || 0))
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
        var images = value.TryGetProperty("images", out var imagesElement)
            ? ReadImages(imagesElement)
            : ExtractImagesFromHtml(outerHtml);

        return new SelectorInspectionResult(
            selector,
            tagName,
            attributes,
            outerHtml,
            computedStyle,
            Array.Empty<string>(),
            connection.ConsoleMessages,
            images);
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

    private static IReadOnlyList<WebImageResource> ReadImages(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<WebImageResource>();
        }

        return element.EnumerateArray()
            .Select(ReadImage)
            .Where(image => !string.IsNullOrWhiteSpace(image.Url))
            .DistinctBy(image => image.Url)
            .ToArray();
    }

    private static WebImageResource ReadImage(JsonElement element)
    {
        return new WebImageResource(
            GetString(element, "url"),
            GetString(element, "kind"),
            GetString(element, "format"),
            GetString(element, "alt"),
            GetInt(element, "width"),
            GetInt(element, "height"),
            element.TryGetProperty("isAnimated", out var animated) && animated.ValueKind == JsonValueKind.True);
    }

    private static IReadOnlyList<WebImageResource> ExtractImagesFromHtml(string html)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return Array.Empty<WebImageResource>();
        }

        var matches = System.Text.RegularExpressions.Regex.Matches(
            html,
            """(?:src|href|poster)=["'](?<url>[^"']+\.(?:png|jpe?g|webp|gif|avif|svg|bmp|ico|apng|tiff?|jfif|pjpe?g|heic|heif|jxl|jp2|j2k|jpf|jpx|jpm|mj2|dds|tga|psd|raw|cr2|nef|orf|arw)(?:\?[^"']*)?)["']""",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        return matches
            .Select(match =>
            {
                var url = match.Groups["url"].Value;
                var format = url.Split('?')[0].Split('#')[0].Split('.').LastOrDefault() ?? "unknown";
                return new WebImageResource(
                    url,
                    "html",
                    format.ToLowerInvariant(),
                    string.Empty,
                    0,
                    0,
                    format.Equals("gif", StringComparison.OrdinalIgnoreCase));
            })
            .DistinctBy(image => image.Url)
            .ToArray();
    }

    private static string GetString(JsonElement element, string property)
    {
        return element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? string.Empty
            : string.Empty;
    }

    private static int GetInt(JsonElement element, string property)
    {
        return element.TryGetProperty(property, out var value) && value.TryGetInt32(out var result)
            ? result
            : 0;
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
