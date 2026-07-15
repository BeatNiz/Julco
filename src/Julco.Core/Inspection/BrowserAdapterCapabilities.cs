namespace Julco.Core.Inspection;

public sealed record BrowserAdapterCapabilities(
    bool SupportsPointInspection,
    bool SupportsRegionInspection,
    bool SupportsComputedStyle,
    bool SupportsMatchedCss,
    bool SupportsAccessibilityTree,
    bool SupportsRuntimeConsole,
    bool SupportsElementCapture,
    bool SupportsNodeHighlight);
