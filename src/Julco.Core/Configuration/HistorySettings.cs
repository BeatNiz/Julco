namespace Julco.Core.Configuration;

public sealed record HistorySettings(int MaxEntries)
{
    public static HistorySettings Default { get; } = new(MaxEntries: 50);
}
