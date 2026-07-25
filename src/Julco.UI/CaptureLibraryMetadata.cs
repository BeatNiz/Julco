using System.IO;
using System.Text.Json;

namespace Julco.UI;

public sealed record CaptureLibraryItemMetadata(
    bool IsFavorite,
    string Tags,
    string Project,
    string SessionId,
    DateTimeOffset UpdatedAt)
{
    public static CaptureLibraryItemMetadata Empty { get; } = new(
        false,
        string.Empty,
        string.Empty,
        string.Empty,
        DateTimeOffset.Now);

    public IReadOnlyList<string> TagList => SplitTags(Tags);

    public CaptureLibraryItemMetadata Normalized()
    {
        return this with
        {
            Tags = string.Join(", ", SplitTags(Tags)),
            Project = Project.Trim(),
            SessionId = SessionId.Trim(),
            UpdatedAt = UpdatedAt == default ? DateTimeOffset.Now : UpdatedAt
        };
    }

    internal static IReadOnlyList<string> SplitTags(string? tags)
    {
        return string.IsNullOrWhiteSpace(tags)
            ? Array.Empty<string>()
            : tags.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(tag => tag, StringComparer.OrdinalIgnoreCase)
                .ToArray();
    }
}

public sealed record CaptureSavedFilter(
    string Name,
    string Query,
    string Browser,
    string Status,
    string Severity,
    string DateRange,
    string Group,
    string QuickTag,
    bool FavoritesOnly);

public sealed record CaptureLibraryIndex(
    IReadOnlyList<CaptureSavedFilter> SavedFilters)
{
    public static CaptureLibraryIndex Empty { get; } = new(Array.Empty<CaptureSavedFilter>());
}

public static class CaptureLibraryStore
{
    private const string ItemFileName = ".julco-library.json";
    private const string IndexFileName = ".julco-library-index.json";
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public static CaptureLibraryItemMetadata LoadItem(string captureDirectory)
    {
        var path = Path.Combine(captureDirectory, ItemFileName);
        if (!File.Exists(path))
        {
            return CaptureLibraryItemMetadata.Empty;
        }

        try
        {
            return (JsonSerializer.Deserialize<CaptureLibraryItemMetadata>(File.ReadAllText(path))
                ?? CaptureLibraryItemMetadata.Empty).Normalized();
        }
        catch (JsonException)
        {
            return CaptureLibraryItemMetadata.Empty;
        }
    }

    public static void SaveItem(string captureDirectory, CaptureLibraryItemMetadata metadata)
    {
        Directory.CreateDirectory(captureDirectory);
        File.WriteAllText(
            Path.Combine(captureDirectory, ItemFileName),
            JsonSerializer.Serialize(metadata.Normalized() with { UpdatedAt = DateTimeOffset.Now }, JsonOptions));
    }

    public static CaptureLibraryIndex LoadIndex(string rootDirectory)
    {
        var path = Path.Combine(rootDirectory, IndexFileName);
        if (!File.Exists(path))
        {
            return CaptureLibraryIndex.Empty;
        }

        try
        {
            return JsonSerializer.Deserialize<CaptureLibraryIndex>(File.ReadAllText(path))
                ?? CaptureLibraryIndex.Empty;
        }
        catch (JsonException)
        {
            return CaptureLibraryIndex.Empty;
        }
    }

    public static void SaveIndex(string rootDirectory, CaptureLibraryIndex index)
    {
        Directory.CreateDirectory(rootDirectory);
        File.WriteAllText(
            Path.Combine(rootDirectory, IndexFileName),
            JsonSerializer.Serialize(index, JsonOptions));
    }
}
