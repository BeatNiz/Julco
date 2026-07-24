namespace Julco.UI;

public sealed record CaptureManifest(
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
    string Inspection);

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
