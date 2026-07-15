namespace Julco.Core.Inspection;

public sealed record InspectionResult(
    BrowserTarget Target,
    InspectedElement Element,
    DomSnapshot Dom,
    CssSnapshot Css,
    AccessibilitySnapshot? Accessibility,
    RuntimeConsoleSnapshot? RuntimeConsole,
    DateTimeOffset CapturedAt,
    IReadOnlyList<InspectionWarning> Warnings);
