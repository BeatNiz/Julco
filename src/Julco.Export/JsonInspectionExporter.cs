using System.Text.Json;
using Julco.Core.Exporting;
using Julco.Core.Inspection;

namespace Julco.Export;

public sealed class JsonInspectionExporter : IInspectionExporter
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true
    };

    public ExportFormat Format => ExportFormat.Json;

    public ExportPackage Export(InspectionResult inspection)
    {
        var content = JsonSerializer.Serialize(inspection, SerializerOptions);

        return new ExportPackage(
            ExportFormat.Json,
            ".json",
            "application/json",
            content);
    }
}
