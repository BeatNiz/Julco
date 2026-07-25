using System.IO;

namespace Julco.UI;

public sealed class CaptureHistoryViewModel
{
    private readonly List<CaptureFileRecord> _captures = new();
    private readonly List<CaptureFileRecord> _filteredCaptures = new();

    public IReadOnlyList<CaptureFileRecord> Captures => _captures;

    public IReadOnlyList<CaptureFileRecord> FilteredCaptures => _filteredCaptures;

    public CaptureFileRecord? SelectedCapture { get; private set; }

    public IReadOnlyList<CaptureLibraryGroup> Groups { get; private set; } = Array.Empty<CaptureLibraryGroup>();

    public IReadOnlyList<CaptureSessionTimelineItem> SessionTimeline { get; private set; } = Array.Empty<CaptureSessionTimelineItem>();

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
            .Where(capture => string.IsNullOrWhiteSpace(filter.Group) || MatchesGroup(capture, filter.Group))
            .Where(capture => string.IsNullOrWhiteSpace(filter.QuickTag) || HasTag(capture, filter.QuickTag))
            .Where(capture => !filter.FavoritesOnly || capture.IsFavorite)
            .OrderByDescending(capture => capture.CreatedAt)
            .ToArray();

        _filteredCaptures.Clear();
        _filteredCaptures.AddRange(filtered);
        Groups = BuildGroups(filtered);
        SessionTimeline = BuildSessionTimeline(filtered, SelectedCapture?.SessionDisplay);

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
        SessionTimeline = BuildSessionTimeline(_filteredCaptures, SelectedCapture?.SessionDisplay);
    }

    public void Select(string directory)
    {
        SelectedCapture = _filteredCaptures.FirstOrDefault(item =>
            string.Equals(item.DirectoryPath, directory, StringComparison.OrdinalIgnoreCase));
        SessionTimeline = BuildSessionTimeline(_filteredCaptures, SelectedCapture?.SessionDisplay);
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

    public IReadOnlyList<string> GetGroupFilterValues()
    {
        return new[] { "All groups" }
            .Concat(_captures
                .SelectMany(capture => new[] { capture.ProjectDisplay, capture.Domain })
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(value => value))
            .ToArray();
    }

    public IReadOnlyList<string> GetTagFilterValues()
    {
        return new[] { "All tags" }
            .Concat(_captures
                .SelectMany(capture => CaptureLibraryItemMetadata.SplitTags(capture.LibraryTags)
                    .Concat(CaptureLibraryItemMetadata.SplitTags(capture.NoteTags)))
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

    private static bool MatchesGroup(CaptureFileRecord capture, string group)
    {
        return capture.ProjectDisplay.Equals(group, StringComparison.OrdinalIgnoreCase)
            || capture.Domain.Equals(group, StringComparison.OrdinalIgnoreCase);
    }

    private static bool HasTag(CaptureFileRecord capture, string tag)
    {
        return CaptureLibraryItemMetadata.SplitTags(capture.LibraryTags)
            .Concat(CaptureLibraryItemMetadata.SplitTags(capture.NoteTags))
            .Any(value => value.Equals(tag, StringComparison.OrdinalIgnoreCase));
    }

    private static IReadOnlyList<CaptureLibraryGroup> BuildGroups(IReadOnlyList<CaptureFileRecord> captures)
    {
        return captures
            .GroupBy(capture => string.IsNullOrWhiteSpace(capture.ProjectDisplay) ? "Ungrouped" : capture.ProjectDisplay)
            .Select(group => new CaptureLibraryGroup(
                group.Key,
                group.Count(),
                group.Max(capture => capture.CreatedAt),
                group.Count(capture => capture.IsFavorite),
                group.SelectMany(capture => CaptureLibraryItemMetadata.SplitTags(capture.LibraryTags))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(tag => tag)
                    .ToArray()))
            .OrderByDescending(group => group.LastCaptureAt)
            .ToArray();
    }

    private static IReadOnlyList<CaptureSessionTimelineItem> BuildSessionTimeline(
        IReadOnlyList<CaptureFileRecord> captures,
        string? selectedSession)
    {
        var session = !string.IsNullOrWhiteSpace(selectedSession)
            ? selectedSession
            : captures.FirstOrDefault()?.SessionDisplay;
        if (string.IsNullOrWhiteSpace(session))
        {
            return Array.Empty<CaptureSessionTimelineItem>();
        }

        return captures
            .Where(capture => capture.SessionDisplay.Equals(session, StringComparison.OrdinalIgnoreCase))
            .OrderBy(capture => capture.CreatedAt)
            .Select((capture, index) => new CaptureSessionTimelineItem(
                index + 1,
                capture.CreatedLocalText,
                capture.HistoryTitle,
                capture.HistorySubtitle,
                capture.NoteSeverity,
                capture.NoteStatus,
                capture.DirectoryPath))
            .ToArray();
    }
}

public sealed record CaptureHistoryFilter(
    string Query,
    string Browser,
    string Status,
    string Severity,
    string DateRange,
    string Group,
    string QuickTag,
    bool FavoritesOnly)
{
    public static CaptureHistoryFilter Empty { get; } = new(
        string.Empty,
        string.Empty,
        string.Empty,
        string.Empty,
        string.Empty,
        string.Empty,
        string.Empty,
        false);
}

public sealed record CaptureLibraryGroup(
    string Name,
    int Count,
    DateTimeOffset LastCaptureAt,
    int FavoriteCount,
    IReadOnlyList<string> Tags)
{
    public string Summary => $"{Count} capture(s), {FavoriteCount} favorite(s)";

    public string LastCaptureText => LastCaptureAt.LocalDateTime.ToString("MM-dd HH:mm");

    public string TagsText => Tags.Count == 0 ? "-" : string.Join(", ", Tags);
}

public sealed record CaptureSessionTimelineItem(
    int Step,
    string Time,
    string Title,
    string Subtitle,
    string Severity,
    string Status,
    string DirectoryPath)
{
    public string DisplayTitle => $"{Step}. {Time}  {Title}";

    public string DisplayMeta => $"{Severity}/{Status}  {Subtitle}";
}
