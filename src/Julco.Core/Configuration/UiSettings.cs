namespace Julco.Core.Configuration;

public sealed record UiSettings(
    int CdpPort,
    int LensInspectionDelayMs,
    bool KeepResultWindowsTopmost)
{
    public static UiSettings Default { get; } = new(
        CdpPort: 9222,
        LensInspectionDelayMs: 220,
        KeepResultWindowsTopmost: true);
}
