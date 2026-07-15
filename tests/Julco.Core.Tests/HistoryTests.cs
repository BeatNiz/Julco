using Julco.Core.History;
using Julco.Core.Inspection;
using Julco.History;
using Xunit;

namespace Julco.Core.Tests;

public sealed class HistoryTests
{
    [Fact]
    public void AddKeepsNewestEntriesWithinLimit()
    {
        var store = new InMemoryInspectionHistoryStore(maxEntries: 2);

        store.Add(Entry("first"));
        store.Add(Entry("second"));
        store.Add(Entry("third"));

        Assert.Equal(2, store.Entries.Count);
        Assert.Equal("third", store.Entries[0].Id);
        Assert.Equal("second", store.Entries[1].Id);
    }

    private static InspectionHistoryEntry Entry(string id)
    {
        return new InspectionHistoryEntry(
            id,
            DateTimeOffset.UtcNow,
            BrowserKind.Chrome,
            new Uri("https://example.com"),
            "div",
            ".sample",
            "/html/body/div");
    }
}
