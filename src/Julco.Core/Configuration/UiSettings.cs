namespace Julco.Core.Configuration;

public sealed record UiSettings(
    int CdpPort,
    int LensInspectionDelayMs,
    bool KeepResultWindowsTopmost,
    UsageProfile Profile)
{
    public static UiSettings Default { get; } = new(
        CdpPort: 9222,
        LensInspectionDelayMs: 220,
        KeepResultWindowsTopmost: true,
        Profile: UsageProfile.QA);
}
