using Julco.Core.Configuration;

namespace Julco.Core.Privacy;

public sealed record PrivacyRedactorOptions(
    bool Enabled,
    bool RedactEmails,
    bool RedactTokens,
    bool RedactCookies,
    bool RedactPrivateUrls,
    bool RedactSelectedText)
{
    public static PrivacyRedactorOptions FromSettings(PrivacySettings settings)
    {
        return new PrivacyRedactorOptions(
            settings.RedactOnExport,
            settings.RedactEmails,
            settings.RedactTokens,
            settings.RedactCookies,
            settings.RedactPrivateUrls,
            settings.RedactSelectedText);
    }
}
