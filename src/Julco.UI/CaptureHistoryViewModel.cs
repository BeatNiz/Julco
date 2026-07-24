using System.IO;

namespace Julco.UI;

public sealed class CaptureHistoryViewModel
{
    private readonly List<CaptureFileRecord> _captures = new();
    private readonly List<CaptureFileRecord> _filteredCaptures = new();

    public IReadOnlyList<CaptureFileRecord> Captures => _captures;

    public IReadOnlyList<CaptureFileRecord> FilteredCaptures => _filteredCaptures;

    public CaptureFileRecord? SelectedCapture { get; private set; }

    public void Load(string rootDirectory, string? preferredSelection = null)
    {
        var selectedDirectory = preferredSelection ?? SelectedCapture?.DirectoryPath;
        _captures.Clear();
        Directory.CreateDirectory(rootDirectory);

        foreach (var directory in Directory.EnumerateDirectories(rootDirectory).OrderByDescending(Directory.GetCreationTimeUtc))
        {
            _captures.Add(CaptureFileRecord.FromDirectory(directory));
        }

        ApplyFilters(CaptureHistoryFilter.Empty, selectedDirectory);
    }

    public void ApplyFilters(CaptureHistoryFilter filter, string? preferredSelection = null)
    {
        var filtered = _captures
            .Where(capture => MatchesQuery(capture, filter.Query))
            .Where(capture => string.IsNullOrWhiteSpace(filter.Browser) || capture.Browser.Equals(filter.Browser, StringComparison.OrdinalIgnoreCase))
            .Where(capture => string.IsNullOrWhiteSpace(filter.Status) || capture.NoteStatus.Equals(filter.Status, StringComparison.OrdinalIgnoreCase))
            .Where(capture => string.IsNullOrWhiteSpace(filter.Severity) || capture.NoteSeverity.Equals(filter.Severity, StringComparison.OrdinalIgnoreCase))
            .Where(capture => MatchesDateRange(capture, filter.DateRange))
            .OrderByDescending(capture => capture.CreatedAt)
            .ToArray();

        _filteredCaptures.Clear();
        _filteredCaptures.AddRange(filtered);

        if (!string.IsNullOrWhiteSpace(preferredSelection))
        {
            Select(preferredSelection);
        }
        else if (SelectedCapture is not null && !_filteredCaptures.Contains(SelectedCapture))
        {
            Select(SelectedCapture.DirectoryPath);
        }

        if (SelectedCapture is null && _filteredCaptures.Count > 0)
        {
            SelectedCapture = _filteredCaptures[0];
        }
    }

    public void Select(CaptureFileRecord? capture)
    {
        SelectedCapture = capture;
    }

    public void Select(string directory)
    {
        SelectedCapture = _filteredCaptures.FirstOrDefault(item =>
            string.Equals(item.DirectoryPath, directory, StringComparison.OrdinalIgnoreCase));
    }

    public IReadOnlyList<string> GetBrowserFilterValues()
    {
        return new[] { "All browsers" }
            .Concat(_captures
                .Select(capture => capture.Browser)
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(value => value))
            .ToArray();
    }

    public string BuildFilterStatus()
    {
        var total = _captures.Count;
        var visible = _filteredCaptures.Count;
        return total == visible
            ? $"{total} capture(s) loaded."
            : $"{visible} of {total} capture(s) match the current filters.";
    }

    private static bool MatchesQuery(CaptureFileRecord capture, string query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return true;
        }

        return query
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .All(term => capture.SearchText.Contains(term, StringComparison.OrdinalIgnoreCase));
    }

    private static bool MatchesDateRange(CaptureFileRecord capture, string dateRange)
    {
        if (string.IsNullOrWhiteSpace(dateRange))
        {
            return true;
        }

        var now = DateTimeOffset.Now;
        return dateRange switch
        {
            "Today" => capture.CreatedAt.LocalDateTime.Date == now.LocalDateTime.Date,
            "Last 7 days" => capture.CreatedAt >= now.AddDays(-7),
            "Last 30 days" => capture.CreatedAt >= now.AddDays(-30),
            _ => true
        };
    }
}

public sealed record CaptureHistoryFilter(
    string Query,
    string Browser,
    string Status,
    string Severity,
    string DateRange)
{
    public static CaptureHistoryFilter Empty { get; } = new(string.Empty, string.Empty, string.Empty, string.Empty, string.Empty);
}
