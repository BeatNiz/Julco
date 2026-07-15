namespace Julco.Core.Logging;

public sealed class NullJulcoLogger : IJulcoLogger
{
    public static NullJulcoLogger Instance { get; } = new();

    private NullJulcoLogger()
    {
    }

    public void Write(LogLevel level, string message, IReadOnlyDictionary<string, string>? properties = null)
    {
    }
}
