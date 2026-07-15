namespace Julco.Core.Inspection;

public sealed record RuntimeExceptionInfo(
    string Text,
    string? Url,
    int? Line,
    int? Column,
    string? StackTrace);
