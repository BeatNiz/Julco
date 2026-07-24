namespace Julco.UI;

public sealed record CaptureNotes(
    string Observation,
    string Category,
    string Severity,
    string Status,
    string Tags,
    DateTimeOffset UpdatedAt)
{
    public static CaptureNotes Empty { get; } = new(
        string.Empty,
        "Visual issue",
        "Medium",
        "Open",
        string.Empty,
        DateTimeOffset.Now);

    public bool HasContent => !string.IsNullOrWhiteSpace(Observation)
        || !string.IsNullOrWhiteSpace(Tags);

    public string ShortSummary
    {
        get
        {
            if (!HasContent)
            {
                return "No notes for this capture.";
            }

            var firstLine = Observation
                .ReplaceLineEndings("\n")
                .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .FirstOrDefault();
            return string.IsNullOrWhiteSpace(firstLine)
                ? $"{Category} | {Severity} | {Status}"
                : $"{Category} | {Severity} | {Status} - {firstLine}";
        }
    }

    public string ToMarkdown()
    {
        var lines = new[]
        {
            "# Capture Notes",
            string.Empty,
            $"Category: {Category}",
            $"Severity: {Severity}",
            $"Status: {Status}",
            $"Tags: {Tags}",
            $"Updated: {UpdatedAt:yyyy-MM-dd HH:mm:ss zzz}",
            string.Empty,
            "## Observation",
            string.IsNullOrWhiteSpace(Observation) ? "_No observation added._" : Observation.Trim()
        };

        return string.Join(Environment.NewLine, lines);
    }

    public string ToEvidenceText()
    {
        if (!HasContent)
        {
            return string.Empty;
        }

        return string.Join(
            Environment.NewLine,
            $"Category: {Category}",
            $"Severity: {Severity}",
            $"Status: {Status}",
            string.IsNullOrWhiteSpace(Tags) ? "Tags: -" : $"Tags: {Tags}",
            string.Empty,
            Observation.Trim());
    }
}
