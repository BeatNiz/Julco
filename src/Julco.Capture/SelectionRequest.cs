using Julco.Core.Geometry;

namespace Julco.Capture;

public sealed record SelectionRequest(
    SelectionMode Mode,
    ScreenPoint StartPoint,
    ScreenPoint EndPoint,
    ScreenRect? Region,
    LensFrameState? LensFrame,
    nint? WindowHandle);
