using Julco.Core.Inspection;

namespace Julco.Core.Exporting;

public interface IInspectionExporter
{
    ExportFormat Format { get; }

    ExportPackage Export(InspectionResult inspection);
}
