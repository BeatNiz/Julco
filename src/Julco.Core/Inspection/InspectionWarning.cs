namespace Julco.Core.Inspection;

public sealed record InspectionWarning(
    string Code,
    string Message,
    string? TechnicalDetail = null);
