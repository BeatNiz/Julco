using Julco.Core.Exporting;
using Julco.Core.Inspection;

namespace Julco.Export;

public sealed class InspectionExportService
{
    private readonly IReadOnlyDictionary<ExportFormat, IInspectionExporter> _exporters;

    public InspectionExportService(IEnumerable<IInspectionExporter> exporters)
    {
        _exporters = exporters.ToDictionary(exporter => exporter.Format);
    }

    public ExportPackage Export(InspectionResult inspection, ExportFormat format)
    {
        if (!_exporters.TryGetValue(format, out var exporter))
        {
            throw new NotSupportedException($"No exporter is registered for '{format}'.");
        }

        return exporter.Export(inspection);
    }

    public static InspectionExportService CreateDefault()
    {
        return new InspectionExportService(new IInspectionExporter[]
        {
            new JsonInspectionExporter(),
            new TextInspectionExporter(ExportFormat.OuterHtml),
            new TextInspectionExporter(ExportFormat.ComputedCss),
            new TextInspectionExporter(ExportFormat.CssRules),
            new TextInspectionExporter(ExportFormat.XPath),
            new TextInspectionExporter(ExportFormat.CssSelector),
            new TextInspectionExporter(ExportFormat.Accessibility),
            new TextInspectionExporter(ExportFormat.RuntimeConsole)
        });
    }
}
