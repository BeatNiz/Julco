using Julco.Core.Inspection;

namespace Julco.Core.History;

public sealed record InspectionHistoryEntry(
    string Id,
    DateTimeOffset CapturedAt,
    BrowserKind Browser,
    Uri Url,
    string TagName,
    string? CssSelector,
    string? XPath)
{
    public static InspectionHistoryEntry FromInspection(InspectionResult inspection)
    {
        return new InspectionHistoryEntry(
            Guid.NewGuid().ToString("N"),
            inspection.CapturedAt,
            inspection.Target.BrowserKind,
            inspection.Target.Url,
            inspection.Element.TagName,
            inspection.Element.CssSelector,
            inspection.Element.XPath);
    }
}
