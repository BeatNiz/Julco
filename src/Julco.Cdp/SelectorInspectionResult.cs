namespace Julco.Cdp;

public sealed record WebImageResource(
    string Url,
    string Kind,
    string Format,
    string Alt,
    int Width,
    int Height,
    bool IsAnimated,
    int NaturalWidth = 0,
    int NaturalHeight = 0,
    int DisplayedWidth = 0,
    int DisplayedHeight = 0,
    long ByteSize = 0,
    bool IsLensFrame = false)
{
    public string DisplayName
    {
        get
        {
            var label = IsLensFrame
                ? "Lens frame"
                : string.IsNullOrWhiteSpace(Alt) ? Kind : Alt;
            var natural = NaturalWidth > 0 && NaturalHeight > 0
                ? $"{NaturalWidth}x{NaturalHeight}"
                : Width > 0 && Height > 0 ? $"{Width}x{Height}" : "unknown";
            var displayed = DisplayedWidth > 0 && DisplayedHeight > 0
                ? $"shown {DisplayedWidth}x{DisplayedHeight}"
                : "shown unknown";
            var size = ByteSize > 0 ? $" | {ByteSize / 1024d:0.#} KB" : string.Empty;
            return $"{label} | {Format} | natural {natural} | {displayed}{size}";
        }
    }

    public string NaturalSizeText => NaturalWidth > 0 && NaturalHeight > 0
        ? $"{NaturalWidth} x {NaturalHeight}"
        : Width > 0 && Height > 0 ? $"{Width} x {Height}" : "Unknown";

    public string DisplayedSizeText => DisplayedWidth > 0 && DisplayedHeight > 0
        ? $"{DisplayedWidth} x {DisplayedHeight}"
        : "Unknown";

    public string ByteSizeText => ByteSize > 0
        ? ByteSize >= 1024 * 1024
            ? $"{ByteSize / 1024d / 1024d:0.##} MB"
            : $"{ByteSize / 1024d:0.#} KB"
        : "Unknown";
}

public sealed record ElementScreenBounds(
    double X,
    double Y,
    double Width,
    double Height)
{
    public bool IsEmpty => Width <= 0 || Height <= 0;
}

public sealed record LensMatchInfo(
    string Confidence,
    string Reason);

public sealed record SelectorInspectionResult(
    string Selector,
    string TagName,
    IReadOnlyDictionary<string, string> Attributes,
    string OuterHtml,
    IReadOnlyDictionary<string, string> ComputedStyle,
    IReadOnlyList<string> MatchedCssRules,
    IReadOnlyList<string> ConsoleMessages,
    IReadOnlyList<WebImageResource> Images,
    ElementScreenBounds? ElementBounds = null,
    LensMatchInfo? LensMatch = null);
