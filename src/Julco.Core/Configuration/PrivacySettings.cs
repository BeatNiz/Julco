namespace Julco.Core.Configuration;

public sealed record PrivacySettings(
    bool RedactOnExport,
    bool RedactEmails,
    bool RedactTokens,
    bool RedactCookies,
    bool RedactPrivateUrls,
    bool RedactSelectedText,
    bool IncludeScreenshotsInSafeExports,
    string CustomRedactionRules,
    bool BlurScreenshotsInSafeExports,
    string ScreenshotRedactionBoxes,
    bool SafeIssueTrackersByDefault,
    bool WarnBeforeSendingSensitiveScreenshots)
{
    public static PrivacySettings Default { get; } = new(
        RedactOnExport: true,
        RedactEmails: true,
        RedactTokens: true,
        RedactCookies: true,
        RedactPrivateUrls: true,
        RedactSelectedText: true,
        IncludeScreenshotsInSafeExports: false,
        CustomRedactionRules: string.Empty,
        BlurScreenshotsInSafeExports: true,
        ScreenshotRedactionBoxes: string.Empty,
        SafeIssueTrackersByDefault: true,
        WarnBeforeSendingSensitiveScreenshots: true);

    public PrivacySettings Normalized()
    {
        return this with
        {
            CustomRedactionRules = CustomRedactionRules ?? string.Empty,
            ScreenshotRedactionBoxes = ScreenshotRedactionBoxes ?? string.Empty
        };
    }
}
