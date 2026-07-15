namespace Julco.Core.Logging;

public interface IJulcoLogger
{
    void Write(LogLevel level, string message, IReadOnlyDictionary<string, string>? properties = null);
}
