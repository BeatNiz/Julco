namespace Julco.Core.Inspection;

public sealed record ElementHandle(
    string AdapterNodeId,
    string? BackendNodeId,
    string? FrameId);
