namespace Julco.Core.Inspection;

public sealed record InspectionOptions(
    bool IncludeDom = true,
    bool IncludeComputedStyle = true,
    bool IncludeMatchedCss = true,
    bool IncludeAccessibility = true,
    bool IncludeRuntimeConsole = true,
    bool IncludeResources = false,
    bool CaptureElementImage = false,
    bool AllowControlledRuntimeEvaluation = false);
