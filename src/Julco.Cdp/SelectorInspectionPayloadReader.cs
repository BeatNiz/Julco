using System.Text.Json;

namespace Julco.Cdp;

internal static class SelectorInspectionPayloadReader
{
    public static SelectorInspectionResult Read(
        string payload,
        IReadOnlyList<string> consoleMessages,
        string missingElementFallbackMessage)
    {
        using var document = JsonDocument.Parse(payload);
        var value = document.RootElement;
        if (value.TryGetProperty("found", out var found) && !found.GetBoolean())
        {
            var message = value.TryGetProperty("message", out var messageValue)
                ? messageValue.GetString()
                : missingElementFallbackMessage;
            throw new InvalidOperationException(message);
        }

        var outerHtml = value.GetProperty("outerHtml").GetString() ?? string.Empty;
        return new SelectorInspectionResult(
            value.GetProperty("selector").GetString() ?? "unknown",
            value.GetProperty("tagName").GetString() ?? "unknown",
            ReadObjectDictionary(value.GetProperty("attributes")),
            outerHtml,
            ReadObjectDictionary(value.GetProperty("computedStyle")),
            ReadStringArray(value, "matchedCssRules"),
            consoleMessages,
            value.TryGetProperty("images", out var images)
                ? ReadImages(images)
                : Array.Empty<WebImageResource>(),
            value.TryGetProperty("elementBounds", out var bounds)
                ? ReadBounds(bounds)
                : null,
            value.TryGetProperty("lensMatch", out var lensMatch)
                ? ReadLensMatch(lensMatch)
                : null);
    }

    public static IReadOnlyDictionary<string, string> ReadObjectDictionary(JsonElement element)
    {
        return element.EnumerateObject()
            .ToDictionary(
                property => property.Name,
                property => property.Value.ValueKind == JsonValueKind.String
                    ? property.Value.GetString() ?? string.Empty
                    : property.Value.ToString());
    }

    private static IReadOnlyList<string> ReadStringArray(JsonElement element, string property)
    {
        return element.TryGetProperty(property, out var array) && array.ValueKind == JsonValueKind.Array
            ? array.EnumerateArray()
                .Select(item => item.GetString())
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .Cast<string>()
                .Distinct()
                .ToArray()
            : Array.Empty<string>();
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

    private static ElementScreenBounds? ReadBounds(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        return new ElementScreenBounds(
            GetDouble(element, "x"),
            GetDouble(element, "y"),
            GetDouble(element, "width"),
            GetDouble(element, "height"));
    }

    private static LensMatchInfo? ReadLensMatch(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        return new LensMatchInfo(
            GetString(element, "confidence"),
            GetString(element, "reason"));
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

    private static double GetDouble(JsonElement element, string property)
    {
        return element.TryGetProperty(property, out var value) && value.TryGetDouble(out var result)
            ? result
            : 0;
    }
}
