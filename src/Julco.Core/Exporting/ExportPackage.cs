namespace Julco.Core.Exporting;

public sealed record ExportPackage(
    ExportFormat Format,
    string FileExtension,
    string MimeType,
    string Content);
