namespace Julco.Cdp;

public sealed record SelectorInspectionResult(
    string Selector,
    string TagName,
    IReadOnlyDictionary<string, string> Attributes,
    string OuterHtml,
    IReadOnlyDictionary<string, string> ComputedStyle,
    IReadOnlyList<string> MatchedCssRules,
    IReadOnlyList<string> ConsoleMessages);
