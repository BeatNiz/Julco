namespace Julco.Core.Inspection;

public sealed record AccessibilitySnapshot(
    string? Role,
    string? Name,
    IReadOnlyDictionary<string, string> Properties,
    IReadOnlyList<string> States);
