using Julco.Capture;

namespace Julco.UI;

public sealed class LensInspectionCoordinator
{
    public LensFrameState? LastState { get; private set; }

    public bool IsFrozen { get; private set; }

    public string DetectedType { get; private set; } = "-";

    public string HistoryKey { get; set; } = string.Empty;

    public void UpdateState(LensFrameState state)
    {
        LastState = state;
    }

    public void SetFrozen(bool isFrozen)
    {
        IsFrozen = isFrozen;
    }

    public void SetDetectedType(string detectedType)
    {
        DetectedType = string.IsNullOrWhiteSpace(detectedType) ? "-" : detectedType;
    }

    public void Reset()
    {
        LastState = null;
        IsFrozen = false;
        DetectedType = "-";
        HistoryKey = string.Empty;
    }

    public bool CanScheduleAutoInspection()
    {
        return LastState is not null && !IsFrozen;
    }

    public string FormatStateText()
    {
        return LastState is null
            ? "Inactive"
            : $"Center {LastState.CenterPoint.X:0},{LastState.CenterPoint.Y:0} | Frame {LastState.Bounds.Width:0}x{LastState.Bounds.Height:0} | {DetectedType}";
    }
}
