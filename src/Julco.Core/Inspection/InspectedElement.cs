using Julco.Core.Geometry;

namespace Julco.Core.Inspection;

public sealed record InspectedElement(
    ElementHandle Handle,
    string TagName,
    string? Id,
    IReadOnlyList<string> Classes,
    IReadOnlyDictionary<string, string> Attributes,
    ScreenRect ScreenBounds,
    ScreenRect ViewportBounds,
    string? CssSelector,
    string? ShortSelector,
    string? XPath,
    int Depth,
    int ChildCount,
    int SiblingCount);
