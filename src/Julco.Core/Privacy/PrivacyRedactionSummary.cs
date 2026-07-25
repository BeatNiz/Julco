namespace Julco.Core.Privacy;

public sealed record PrivacyRedactionSummary(
    int EmailMatches,
    int TokenMatches,
    int CookieMatches,
    int PrivateUrlMatches,
    int HtmlTextNodeMatches,
    int CustomRuleMatches = 0)
{
    public static PrivacyRedactionSummary Empty { get; } = new(0, 0, 0, 0, 0);

    public int TotalMatches => EmailMatches + TokenMatches + CookieMatches + PrivateUrlMatches + HtmlTextNodeMatches + CustomRuleMatches;

    public bool HasChanges => TotalMatches > 0;

    public PrivacyRedactionSummary Add(PrivacyRedactionSummary other)
    {
        return new PrivacyRedactionSummary(
            EmailMatches + other.EmailMatches,
            TokenMatches + other.TokenMatches,
            CookieMatches + other.CookieMatches,
            PrivateUrlMatches + other.PrivateUrlMatches,
            HtmlTextNodeMatches + other.HtmlTextNodeMatches,
            CustomRuleMatches + other.CustomRuleMatches);
    }
}

public sealed record PrivacyRedactionFieldPreview(
    string Field,
    PrivacyRedactionSummary Summary,
    string Before,
    string After)
{
    public bool HasChanges => Summary.HasChanges || !string.Equals(Before, After, StringComparison.Ordinal);

    public string SummaryText => $"{Field}: {Summary.TotalMatches} finding(s)";
}
