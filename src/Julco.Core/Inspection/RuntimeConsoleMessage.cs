namespace Julco.Core.Inspection;

public sealed record RuntimeConsoleMessage(
    RuntimeConsoleMessageLevel Level,
    string Text,
    string? Source,
    string? Url,
    int? Line,
    int? Column,
    DateTimeOffset Timestamp);
