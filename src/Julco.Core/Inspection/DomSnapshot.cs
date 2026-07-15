namespace Julco.Core.Inspection;

public sealed record DomSnapshot(
    string? OuterHtml,
    string? InnerHtml,
    string? Text,
    IReadOnlyList<string> Ancestors,
    bool IsInsideIframe,
    bool IsCrossOriginFrame,
    bool HasOpenShadowRoot,
    bool HasClosedShadowRoot);
