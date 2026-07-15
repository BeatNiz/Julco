using Julco.Core.History;

namespace Julco.History;

public sealed class InMemoryInspectionHistoryStore : IInspectionHistoryStore
{
    private readonly int _maxEntries;
    private readonly List<InspectionHistoryEntry> _entries = new();

    public InMemoryInspectionHistoryStore(int maxEntries)
    {
        if (maxEntries <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxEntries), "History must keep at least one entry.");
        }

        _maxEntries = maxEntries;
    }

    public IReadOnlyList<InspectionHistoryEntry> Entries => _entries;

    public void Add(InspectionHistoryEntry entry)
    {
        _entries.Insert(0, entry);

        if (_entries.Count > _maxEntries)
        {
            _entries.RemoveRange(_maxEntries, _entries.Count - _maxEntries);
        }
    }

    public void Clear()
    {
        _entries.Clear();
    }
}
