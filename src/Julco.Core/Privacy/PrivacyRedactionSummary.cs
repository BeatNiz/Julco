namespace Julco.Core.Privacy;

public sealed record PrivacyRedactionSummary(
    int EmailMatches,
    int TokenMatches,
    int CookieMatches,
    int PrivateUrlMatches,
    int HtmlTextNodeMatches)
{
    public static PrivacyRedactionSummary Empty { get; } = new(0, 0, 0, 0, 0);

    public int TotalMatches => EmailMatches + TokenMatches + CookieMatches + PrivateUrlMatches + HtmlTextNodeMatches;

    public bool HasChanges => TotalMatches > 0;

    public PrivacyRedactionSummary Add(PrivacyRedactionSummary other)
    {
        return new PrivacyRedactionSummary(
            EmailMatches + other.EmailMatches,
            TokenMatches + other.TokenMatches,
            CookieMatches + other.CookieMatches,
            PrivateUrlMatches + other.PrivateUrlMatches,
            HtmlTextNodeMatches + other.HtmlTextNodeMatches);
    }
}
