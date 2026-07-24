using System.Globalization;
using System.Text.Json;

namespace Julco.Cdp;

public sealed class FirefoxBidiInspectionService
{
    public async Task<SelectorInspectionResult> InspectAsync(
        CdpTarget target,
        string selector,
        CancellationToken cancellationToken)
    {
        var selectorJson = JsonSerializer.Serialize(selector);
        var script = $$"""
            JSON.stringify((() => {
                const selector = {{selectorJson}};
                const element = document.querySelector(selector);
                return window.__julcoInspectElement(element, selector);
            })())
            """;

        return await InspectWithScriptAsync(target, script, cancellationToken);
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
        var script = $$"""
            JSON.stringify((() => {
                const screenXValue = {{screenX.ToString(CultureInfo.InvariantCulture)}};
                const screenYValue = {{screenY.ToString(CultureInfo.InvariantCulture)}};
                const regionLeftValue = {{regionLeft.ToString(CultureInfo.InvariantCulture)}};
                const regionTopValue = {{regionTop.ToString(CultureInfo.InvariantCulture)}};
                const regionWidthValue = {{regionWidth.ToString(CultureInfo.InvariantCulture)}};
                const regionHeightValue = {{regionHeight.ToString(CultureInfo.InvariantCulture)}};
                const chromeLeft = Math.max(0, (window.outerWidth - window.innerWidth) / 2);
                const chromeTop = Math.max(0, window.outerHeight - window.innerHeight - chromeLeft);
                const dpr = window.devicePixelRatio || 1;
                const toViewport = (screenX, screenY) => ({
                    x: screenX - window.screenX - chromeLeft,
                    y: screenY - window.screenY - chromeTop
                });
                const candidates = [
                    { screenX: screenXValue, screenY: screenYValue },
                    { screenX: screenXValue / dpr, screenY: screenYValue / dpr }
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
                            viewportX,
                            viewportY,
                            element: inside ? document.elementFromPoint(viewportX, viewportY) : null
                        };
                    })
                    .find(candidate => candidate.element);

                const regionTopLeft = toViewport(regionLeftValue, regionTopValue);
                const regionBottomRight = toViewport(regionLeftValue + regionWidthValue, regionTopValue + regionHeightValue);
                const region = {
                    left: Math.min(regionTopLeft.x, regionBottomRight.x),
                    top: Math.min(regionTopLeft.y, regionBottomRight.y),
                    right: Math.max(regionTopLeft.x, regionBottomRight.x),
                    bottom: Math.max(regionTopLeft.y, regionBottomRight.y)
                };

                return window.__julcoInspectElement(hit?.element ?? null, "lens center", region);
            })())
            """;

        return await InspectWithScriptAsync(target, script, cancellationToken);
    }

    private static async Task<SelectorInspectionResult> InspectWithScriptAsync(
        CdpTarget target,
        string inspectionScript,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(target.WebSocketDebuggerUrl))
        {
            throw new InvalidOperationException("The selected Firefox target does not expose a BiDi WebSocket URL.");
        }

        await using var connection = new FirefoxBidiConnection();
        await connection.ConnectAsync(new Uri(target.WebSocketDebuggerUrl), cancellationToken);
        await SubscribeToConsoleIfAvailableAsync(connection, target.Id, cancellationToken);

        var bootstrap = """
            (() => {
                window.__julcoInspectElement = (element, fallbackSelector, region = null) => {
                    if (!element) {
                        return {
                            found: false,
                            message: "No element matched the selected target."
                        };
                    }

                    const attributes = {};
                    for (const attribute of element.attributes) {
                        attributes[attribute.name] = attribute.value;
                    }

                    const computed = getComputedStyle(element);
                    const computedStyle = {};
                    for (const name of computed) {
                        computedStyle[name] = computed.getPropertyValue(name);
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
                        if (!imageElement?.getBoundingClientRect || !region) return imageElement === element ? 100000000 : 0;
                        const rect = imageElement.getBoundingClientRect();
                        const overlapWidth = Math.max(0, Math.min(rect.right, region.right) - Math.max(rect.left, region.left));
                        const overlapHeight = Math.max(0, Math.min(rect.bottom, region.bottom) - Math.max(rect.top, region.top));
                        const overlapArea = overlapWidth * overlapHeight;
                        const centerX = (region.left + region.right) / 2;
                        const centerY = (region.top + region.bottom) / 2;
                        const centerInside = centerX >= rect.left && centerX <= rect.right && centerY >= rect.top && centerY <= rect.bottom;
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
                            naturalWidth: Math.round(imageElement?.naturalWidth || imageElement?.videoWidth || 0),
                            naturalHeight: Math.round(imageElement?.naturalHeight || imageElement?.videoHeight || 0),
                            displayedWidth: Math.round(imageElement?.getBoundingClientRect?.().width || imageElement?.clientWidth || 0),
                            displayedHeight: Math.round(imageElement?.getBoundingClientRect?.().height || imageElement?.clientHeight || 0),
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
                    const imageRoots = region
                        ? Array.from(document.querySelectorAll("*")).filter(item => {
                            const rect = item.getBoundingClientRect();
                            if (rect.width <= 0 || rect.height <= 0) return item === element;
                            return rect.right >= region.left
                                && rect.left <= region.right
                                && rect.bottom >= region.top
                                && rect.top <= region.bottom;
                        })
                        : [element, ...element.querySelectorAll("*")];
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
                        if (fallbackSelector && fallbackSelector !== "lens center") return fallbackSelector;

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

                        return parts.join(" > ") || fallbackSelector || element.localName;
                    })();

                    const matchedCssRules = [];
                    const visitRules = rules => {
                        for (const rule of rules) {
                            if (rule.cssRules) {
                                try {
                                    visitRules(rule.cssRules);
                                } catch {
                                }
                            }

                            if (!rule.selectorText) {
                                continue;
                            }

                            try {
                                if (element.matches(rule.selectorText)) {
                                    matchedCssRules.push(rule.selectorText);
                                }
                            } catch {
                            }
                        }
                    };

                    for (const sheet of document.styleSheets) {
                        try {
                            visitRules(sheet.cssRules);
                        } catch {
                        }
                    }

                    return {
                        found: true,
                        selector,
                        tagName: element.tagName,
                        attributes,
                        outerHtml: element.outerHTML,
                        computedStyle,
                        matchedCssRules: Array.from(new Set(matchedCssRules)).slice(0, 200),
                        images: images.sort((a, b) => (b.priority || 0) - (a.priority || 0))
                    };
                };
                return "ready";
            })()
            """;

        await connection.EvaluateJsonAsync(target.Id, $"JSON.stringify({bootstrap})", cancellationToken);
        var payload = await connection.EvaluateJsonAsync(target.Id, inspectionScript, cancellationToken);
        using var document = JsonDocument.Parse(payload);
        var value = document.RootElement;

        if (value.TryGetProperty("found", out var found) && !found.GetBoolean())
        {
            var message = value.TryGetProperty("message", out var messageValue)
                ? messageValue.GetString()
                : "No element found.";
            throw new InvalidOperationException(message);
        }

        return new SelectorInspectionResult(
            value.GetProperty("selector").GetString() ?? "unknown",
            value.GetProperty("tagName").GetString() ?? "unknown",
            ReadObjectDictionary(value.GetProperty("attributes")),
            value.GetProperty("outerHtml").GetString() ?? string.Empty,
            ReadObjectDictionary(value.GetProperty("computedStyle")),
            value.TryGetProperty("matchedCssRules", out var rules)
                ? rules.EnumerateArray()
                    .Select(item => item.GetString())
                    .Where(item => !string.IsNullOrWhiteSpace(item))
                    .Cast<string>()
                    .ToArray()
                : Array.Empty<string>(),
            connection.ConsoleMessages,
            value.TryGetProperty("images", out var images)
                ? ReadImages(images)
                : Array.Empty<WebImageResource>());
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
            element.TryGetProperty("isAnimated", out var animated) && animated.ValueKind == JsonValueKind.True,
            GetInt(element, "naturalWidth"),
            GetInt(element, "naturalHeight"),
            GetInt(element, "displayedWidth"),
            GetInt(element, "displayedHeight"),
            GetLong(element, "byteSize"),
            element.TryGetProperty("isLensFrame", out var lensFrame) && lensFrame.ValueKind == JsonValueKind.True);
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

    private static long GetLong(JsonElement element, string property)
    {
        return element.TryGetProperty(property, out var value) && value.TryGetInt64(out var result)
            ? result
            : 0;
    }
}
