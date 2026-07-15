using Julco.Core.Geometry;

namespace Julco.Core.Inspection;

public sealed record BrowserTarget(
    string TargetId,
    BrowserKind BrowserKind,
    string Title,
    Uri Url,
    int? ProcessId,
    ScreenRect? WindowBounds,
    bool IsInspectable);
