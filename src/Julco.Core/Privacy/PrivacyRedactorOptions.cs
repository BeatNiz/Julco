using Julco.Core.Configuration;

namespace Julco.Core.Privacy;

public sealed record PrivacyRedactorOptions(
    bool Enabled,
    bool RedactEmails,
    bool RedactTokens,
    bool RedactCookies,
    bool RedactPrivateUrls,
    bool RedactSelectedText,
    IReadOnlyList<CustomRedactionRule>? CustomRules = null)
{
    public IReadOnlyList<CustomRedactionRule> EffectiveCustomRules => CustomRules ?? Array.Empty<CustomRedactionRule>();

    public static PrivacyRedactorOptions FromSettings(PrivacySettings settings)
    {
        settings = settings.Normalized();
        return new PrivacyRedactorOptions(
            settings.RedactOnExport,
            settings.RedactEmails,
            settings.RedactTokens,
            settings.RedactCookies,
            settings.RedactPrivateUrls,
            settings.RedactSelectedText,
            CustomRedactionRule.ParseMany(settings.CustomRedactionRules));
    }
}

public sealed record CustomRedactionRule(string Name, string Pattern)
{
    public static IReadOnlyList<CustomRedactionRule> ParseMany(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return Array.Empty<CustomRedactionRule>();
        }

        return value
            .Split(["\r\n", "\n"], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(ParseLine)
            .Where(rule => rule is not null)
            .Cast<CustomRedactionRule>()
            .ToArray();
    }

    private static CustomRedactionRule? ParseLine(string line)
    {
        if (line.StartsWith("#", StringComparison.Ordinal) || string.IsNullOrWhiteSpace(line))
        {
            return null;
        }

        var separator = line.IndexOf('=');
        if (separator <= 0 || separator >= line.Length - 1)
        {
            return null;
        }

        return new CustomRedactionRule(
            line[..separator].Trim(),
            line[(separator + 1)..].Trim());
    }
}
