using Julco.Core.Geometry;

namespace Julco.Core.Inspection;

public sealed record InspectionRequest(
    BrowserTarget Target,
    ScreenPoint ScreenPoint,
    ScreenRect? Region,
    InspectionOptions Options,
    InspectionTrigger Trigger = InspectionTrigger.ManualPoint);
