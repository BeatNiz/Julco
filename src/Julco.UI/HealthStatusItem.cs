namespace Julco.UI;

public sealed record HealthStatusItem(
    string Name,
    string State,
    string Detail,
    string Severity);
