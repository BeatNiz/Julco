namespace Julco.Core.Inspection;

public sealed record CssDeclaration(
    string Name,
    string Value,
    bool IsImportant,
    string? Source,
    bool IsOverridden);
