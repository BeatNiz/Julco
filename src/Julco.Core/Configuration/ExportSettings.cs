using Julco.Core.Exporting;

namespace Julco.Core.Configuration;

public sealed record ExportSettings(
    ExportFormat DefaultFormat,
    bool IncludeWarnings,
    bool IncludeAccessibility)
{
    public static ExportSettings Default { get; } = new(
        ExportFormat.Json,
        IncludeWarnings: true,
        IncludeAccessibility: true);
}
