namespace Julco.Core.History;

public interface IInspectionHistoryStore
{
    IReadOnlyList<InspectionHistoryEntry> Entries { get; }

    void Add(InspectionHistoryEntry entry);

    void Clear();
}
