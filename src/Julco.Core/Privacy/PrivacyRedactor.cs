using System.Text.RegularExpressions;

namespace Julco.Core.Privacy;

public static class PrivacyRedactor
{
    private static readonly Regex EmailRegex = new(
        @"\b[A-Z0-9._%+\-]+@[A-Z0-9.\-]+\.[A-Z]{2,}\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex CookieHeaderRegex = new(
        @"(?<name>\b(cookie|set-cookie)\b\s*[:=]\s*)(?<value>[^\r\n;""}]+)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex CookieAttributeRegex = new(
        @"(?<name>\b(cookie|cookies|session|sessionid|sid|csrf|xsrf)\b\s*=\s*)(?<value>[^;""'\s<>]+)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex TokenKeyValueRegex = new(
        @"(?<name>\b(access[_-]?token|refresh[_-]?token|id[_-]?token|auth[_-]?token|api[_-]?key|apikey|authorization|bearer|jwt|secret|password|passwd|pwd|token)\b\s*[:=]\s*[""']?)(?<value>[A-Za-z0-9_\-\.=:/+]{8,})",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex BearerRegex = new(
        @"\bBearer\s+[A-Za-z0-9_\-\.=+/]{10,}",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex UrlRegex = new(
        @"https?://[^\s""'<>\\)]+",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex HtmlTextNodeRegex = new(
        @">(?!\s*<)(?<text>[^<]{3,})<",
        RegexOptions.Compiled);

    public static string RedactText(string? value, PrivacyRedactorOptions options)
    {
        if (!options.Enabled || string.IsNullOrEmpty(value))
        {
            return value ?? string.Empty;
        }

        var result = value;
        if (options.RedactEmails)
        {
            result = EmailRegex.Replace(result, "[REDACTED_EMAIL]");
        }

        if (options.RedactCookies)
        {
            result = CookieHeaderRegex.Replace(result, "${name}[REDACTED_COOKIE]");
            result = CookieAttributeRegex.Replace(result, "${name}[REDACTED_COOKIE]");
        }

        if (options.RedactTokens)
        {
            result = BearerRegex.Replace(result, "Bearer [REDACTED_TOKEN]");
            result = TokenKeyValueRegex.Replace(result, "${name}[REDACTED_TOKEN]");
        }

        if (options.RedactPrivateUrls)
        {
            result = UrlRegex.Replace(result, RedactUrl);
        }

        return result;
    }

    public static string RedactHtml(string? html, PrivacyRedactorOptions options)
    {
        var result = RedactText(html, options);
        if (!options.Enabled || !options.RedactSelectedText || string.IsNullOrWhiteSpace(result))
        {
            return result;
        }

        return HtmlTextNodeRegex.Replace(result, match =>
        {
            var text = match.Groups["text"].Value;
            return string.IsNullOrWhiteSpace(text)
                ? match.Value
                : ">[REDACTED_TEXT]<";
        });
    }

    private static string RedactUrl(Match match)
    {
        if (!Uri.TryCreate(match.Value, UriKind.Absolute, out var uri))
            return "[REDACTED_URL]";

        var builder = new UriBuilder(uri)
        {
            UserName = string.Empty,
            Password = string.Empty,
            Query = string.IsNullOrEmpty(uri.Query) ? string.Empty : "redacted=1",
            Fragment = string.IsNullOrEmpty(uri.Fragment) ? string.Empty : "redacted"
        };

        var path = uri.AbsolutePath;
        if (LooksPrivatePath(path))
        {
            builder.Path = "/[REDACTED_PATH]";
        }

        return builder.Uri.ToString();
    }

    private static bool LooksPrivatePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || path == "/")
        {
            return false;
        }

        return Regex.IsMatch(
            path,
            @"(/user/|/users/|/account|/profile|/checkout|/cart|/order|/invoice|/admin|/private|/[A-F0-9]{16,}|/[0-9]{6,})",
            RegexOptions.IgnoreCase);
    }
}
