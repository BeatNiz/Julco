namespace Julco.Core.Configuration;

public sealed record UiSettings(
    int CdpPort,
    int LensInspectionDelayMs,
    bool KeepResultWindowsTopmost,
    UsageProfile Profile,
    bool EnableLensSnapToElement,
    bool EnableLensZoomPreview,
    double LensZoomFactor,
    bool EnableLensCaptureOnChange)
{
    public static UiSettings Default { get; } = new(
        CdpPort: 9222,
        LensInspectionDelayMs: 220,
        KeepResultWindowsTopmost: true,
        Profile: UsageProfile.QA,
        EnableLensSnapToElement: false,
        EnableLensZoomPreview: false,
        LensZoomFactor: 1.45,
        EnableLensCaptureOnChange: false);

    public UiSettings Normalized()
    {
        return this with
        {
            CdpPort = CdpPort <= 0 || CdpPort > 65535 ? Default.CdpPort : CdpPort,
            LensInspectionDelayMs = LensInspectionDelayMs < 150 ? Default.LensInspectionDelayMs : LensInspectionDelayMs,
            LensZoomFactor = LensZoomFactor < 1.1 || LensZoomFactor > 3 ? Default.LensZoomFactor : LensZoomFactor
        };
    }
}
