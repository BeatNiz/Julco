namespace Julco.Core.Inspection;

public sealed record RuntimeScriptInfo(
    string ScriptId,
    string? Url,
    int StartLine,
    int StartColumn,
    int EndLine,
    int EndColumn,
    bool IsContentScript);
