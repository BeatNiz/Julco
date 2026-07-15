namespace Julco.Core.Inspection;

public sealed record CssRule(
    string Selector,
    string? StyleSheetUrl,
    int? Line,
    int? Column,
    IReadOnlyList<CssDeclaration> Declarations);
