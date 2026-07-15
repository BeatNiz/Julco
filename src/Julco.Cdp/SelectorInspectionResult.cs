namespace Julco.Cdp;

public sealed record WebImageResource(
    string Url,
    string Kind,
    string Format,
    string Alt,
    int Width,
    int Height,
    bool IsAnimated)
{
    public string DisplayName
    {
        get
        {
            var label = string.IsNullOrWhiteSpace(Alt) ? Kind : Alt;
            var size = Width > 0 && Height > 0 ? $"{Width}x{Height}" : "unknown size";
            return $"{label} | {Format} | {size}";
        }
    }
}

public sealed record SelectorInspectionResult(
    string Selector,
    string TagName,
    IReadOnlyDictionary<string, string> Attributes,
    string OuterHtml,
    IReadOnlyDictionary<string, string> ComputedStyle,
    IReadOnlyList<string> MatchedCssRules,
    IReadOnlyList<string> ConsoleMessages,
    IReadOnlyList<WebImageResource> Images);
