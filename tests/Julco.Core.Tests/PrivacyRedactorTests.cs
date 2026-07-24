using Julco.Core.Privacy;
using Xunit;

namespace Julco.Core.Tests;

public sealed class PrivacyRedactorTests
{
    private static readonly PrivacyRedactorOptions Options = new(
        Enabled: true,
        RedactEmails: true,
        RedactTokens: true,
        RedactCookies: true,
        RedactPrivateUrls: true,
        RedactSelectedText: true);

    [Fact]
    public void RedactsCommonSensitiveText()
    {
        var input = "email ana@example.com Authorization: Bearer abcdefghijk token=secret-token-123 cookie=sessionid123";

        var result = PrivacyRedactor.RedactText(input, Options);

        Assert.DoesNotContain("ana@example.com", result);
        Assert.DoesNotContain("abcdefghijk", result);
        Assert.DoesNotContain("secret-token-123", result);
        Assert.DoesNotContain("sessionid123", result);
        Assert.Contains("[REDACTED_EMAIL]", result);
        Assert.Contains("[REDACTED_TOKEN]", result);
        Assert.Contains("[REDACTED_COOKIE]", result);
    }

    [Fact]
    public void RedactsPrivateUrlPartsButKeepsOrigin()
    {
        var input = "https://example.com/users/123456?token=abc#private";

        var result = PrivacyRedactor.RedactText(input, Options);

        Assert.Contains("https://example.com/", result);
        Assert.DoesNotContain("123456", result);
        Assert.DoesNotContain("token=abc", result);
        Assert.DoesNotContain("#private", result);
    }

    [Fact]
    public void RedactsHtmlTextNodesWhenEnabled()
    {
        var input = "<button data-id=\"safe\">Pay $100 for ana@example.com</button>";

        var result = PrivacyRedactor.RedactHtml(input, Options);

        Assert.Contains("data-id=\"safe\"", result);
        Assert.Contains("[REDACTED_TEXT]", result);
        Assert.DoesNotContain("ana@example.com", result);
    }

    [Fact]
    public void AnalyzeTextCountsConfiguredSensitivePatterns()
    {
        var input = "ana@example.com token=secret-token-123 Cookie: sessionid123 https://example.com/users/123456?token=abc";

        var summary = PrivacyRedactor.AnalyzeText(input, Options);

        Assert.True(summary.HasChanges);
        Assert.Equal(1, summary.EmailMatches);
        Assert.True(summary.TokenMatches >= 1);
        Assert.True(summary.CookieMatches >= 1);
        Assert.Equal(1, summary.PrivateUrlMatches);
    }

    [Fact]
    public void AnalyzeHtmlCountsVisibleTextNodesWhenEnabled()
    {
        var input = "<section><button>Private visible label</button><span>ana@example.com</span></section>";

        var summary = PrivacyRedactor.AnalyzeHtml(input, Options);

        Assert.True(summary.HtmlTextNodeMatches >= 2);
        Assert.Equal(1, summary.EmailMatches);
    }
}
