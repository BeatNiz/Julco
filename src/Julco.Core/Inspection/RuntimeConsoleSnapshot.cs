namespace Julco.Core.Inspection;

public sealed record RuntimeConsoleSnapshot(
    IReadOnlyList<RuntimeConsoleMessage> Messages,
    IReadOnlyList<RuntimeExceptionInfo> Exceptions,
    IReadOnlyList<RuntimeScriptInfo> Scripts,
    bool WasRuntimeEvaluationUsed);
