namespace Julco.Core.Configuration;

public sealed record PrivacySettings(
    bool RedactOnExport,
    bool RedactEmails,
    bool RedactTokens,
    bool RedactCookies,
    bool RedactPrivateUrls,
    bool RedactSelectedText,
    bool IncludeScreenshotsInSafeExports)
{
    public static PrivacySettings Default { get; } = new(
        RedactOnExport: true,
        RedactEmails: true,
        RedactTokens: true,
        RedactCookies: true,
        RedactPrivateUrls: true,
        RedactSelectedText: true,
        IncludeScreenshotsInSafeExports: false);
}
