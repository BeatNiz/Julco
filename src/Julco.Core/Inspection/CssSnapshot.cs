namespace Julco.Core.Inspection;

public sealed record CssSnapshot(
    IReadOnlyList<CssDeclaration> Computed,
    IReadOnlyList<CssRule> MatchedRules,
    IReadOnlyDictionary<string, string> CustomProperties,
    IReadOnlyDictionary<string, string> PseudoElements);
