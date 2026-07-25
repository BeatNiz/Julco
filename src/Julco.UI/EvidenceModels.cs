namespace Julco.UI;

public static class EvidenceSchemaVersion
{
    public const string Current = "2.0";
}

public sealed record CaptureManifest(
    string SchemaVersion,
    DateTimeOffset CreatedAt,
    string PageTitle,
    string Url,
    string TagName,
    string Selector,
    double X,
    double Y,
    double Width,
    double Height,
    string Screenshot,
    string Inspection)
{
    public static CaptureManifest CreateCurrent(
        DateTimeOffset createdAt,
        string pageTitle,
        string url,
        string tagName,
        string selector,
        double x,
        double y,
        double width,
        double height,
        string screenshot,
        string inspection)
    {
        return new CaptureManifest(
            EvidenceSchemaVersion.Current,
            createdAt,
            pageTitle,
            url,
            tagName,
            selector,
            x,
            y,
            width,
            height,
            screenshot,
            inspection);
    }
}

public sealed record EvidencePackage(
    string Version,
    DateTimeOffset CreatedAt,
    EvidenceBrowserContext Browser,
    EvidencePageContext Page,
    EvidenceElementContext Element,
    EvidenceFrameContext Frame,
    EvidenceFiles Files,
    string Notes,
    CaptureNotes? StructuredNotes);

public sealed record EvidenceBrowserContext(
    string Name,
    string TargetType,
    string RemotePort,
    string TargetId);

public sealed record EvidencePageContext(
    string Title,
    string Url);

public sealed record EvidenceElementContext(
    string TagName,
    string Selector,
    string DetectedType,
    IReadOnlyDictionary<string, string> Attributes,
    int ImageResourceCount,
    int ConsoleMessageCount);

public sealed record EvidenceFrameContext(
    double X,
    double Y,
    double Width,
    double Height,
    double CenterX,
    double CenterY,
    string ScreenName,
    int ScreenWidth,
    int ScreenHeight);

public sealed record EvidenceFiles(
    string Screenshot,
    string Inspection,
    string Dom,
    string ComputedCss,
    string Console,
    string Attributes,
    string Images,
    string StructuredNotes,
    string Notes,
    string Summary);

public sealed record EvidenceValidationIssue(
    string Severity,
    string Code,
    string Message,
    string? RelativePath);

public sealed record EvidenceValidationResult(
    string SchemaVersion,
    bool IsCurrent,
    IReadOnlyList<EvidenceValidationIssue> Issues)
{
    public bool IsValid => Issues.All(issue => !issue.Severity.Equals("Error", StringComparison.OrdinalIgnoreCase));

    public string Summary
    {
        get
        {
            if (Issues.Count == 0)
            {
                return $"Evidence package OK. Schema {SchemaVersion}.";
            }

            var errors = Issues.Count(issue => issue.Severity.Equals("Error", StringComparison.OrdinalIgnoreCase));
            var warnings = Issues.Count(issue => issue.Severity.Equals("Warning", StringComparison.OrdinalIgnoreCase));
            return $"Schema {SchemaVersion}. {errors} error(s), {warnings} warning(s).";
        }
    }
}

public sealed record EvidenceRepairResult(
    EvidenceValidationResult Before,
    EvidenceValidationResult After,
    IReadOnlyList<string> Actions,
    string CaptureDirectory)
{
    public string Summary => $"{Actions.Count} repair action(s). {After.Summary}";
}
