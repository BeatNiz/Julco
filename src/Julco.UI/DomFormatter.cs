using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace Julco.UI;

public static class DomFormatter
{
    private static readonly Regex AttributeRegex = new(
        @"(?<name>[^\s=\/<>]+)(?:\s*=\s*(?:""(?<value>[^""]*)""|'(?<value>[^']*)'|(?<value>[^\s""'=<>`]+)))?",
        RegexOptions.Compiled);

    public static string PrettyPrint(string html)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return string.Empty;
        }

        var tokens = Regex.Matches(html, @"<[^>]+>|[^<]+")
            .Select(match => match.Value.Trim())
            .Where(value => !string.IsNullOrWhiteSpace(value));

        var builder = new StringBuilder();
        var indent = 0;

        foreach (var token in tokens)
        {
            var isClosing = token.StartsWith("</", StringComparison.Ordinal);
            var isSelfClosing = token.EndsWith("/>", StringComparison.Ordinal)
                || Regex.IsMatch(token, @"^<(area|base|br|col|embed|hr|img|input|link|meta|param|source|track|wbr)\b", RegexOptions.IgnoreCase);

            if (isClosing)
            {
                indent = Math.Max(0, indent - 1);
            }

            builder.Append(new string(' ', indent * 2));
            builder.AppendLine(WrapOpeningTag(token, indent));

            if (!isClosing && !isSelfClosing && token.StartsWith('<') && !token.StartsWith("<!", StringComparison.Ordinal))
            {
                indent++;
            }
        }

        return WebUtility.HtmlDecode(builder.ToString()).TrimEnd();
    }

    public static IReadOnlyList<DomAttributeView> ExtractAttributes(string html)
    {
        var openingTag = Regex.Match(html, @"<(?<tag>[a-zA-Z][^\s/>]*)(?<attrs>[^>]*)>");
        if (!openingTag.Success)
        {
            return Array.Empty<DomAttributeView>();
        }

        var attributes = openingTag.Groups["attrs"].Value;
        return AttributeRegex.Matches(attributes)
            .Select(match => new DomAttributeView(
                match.Groups["name"].Value,
                WebUtility.HtmlDecode(match.Groups["value"].Value),
                DescribeAttribute(match.Groups["name"].Value)))
            .Where(attribute => !string.IsNullOrWhiteSpace(attribute.Name))
            .ToArray();
    }

    private static string WrapOpeningTag(string token, int indent)
    {
        if (!token.StartsWith('<') || token.StartsWith("</") || token.Length < 100)
        {
            return token;
        }

        var match = Regex.Match(token, @"^<(?<tag>[^\s/>]+)(?<attrs>.*?)(?<end>/?)>$");
        if (!match.Success)
        {
            return token;
        }

        var tag = match.Groups["tag"].Value;
        var end = match.Groups["end"].Value == "/" ? " />" : ">";
        var attributes = ExtractAttributes(token);
        if (attributes.Count == 0)
        {
            return token;
        }

        var pad = new string(' ', (indent + 1) * 2);
        var builder = new StringBuilder();
        builder.Append('<').Append(tag);
        foreach (var attribute in attributes)
        {
            builder.AppendLine();
            builder.Append(pad)
                .Append(attribute.Name)
                .Append("=\"")
                .Append(attribute.Value)
                .Append('"');
        }

        builder.Append(end);
        return builder.ToString();
    }

    private static string DescribeAttribute(string name)
    {
        return name.ToLowerInvariant() switch
        {
            "id" => "Unique element identifier inside the document. It is often used for styling, internal links, and quick selection.",
            "class" => "List of CSS classes applied to the element. Classes group visual styles and behavior hooks.",
            "style" => "Inline styles applied directly to the element. They usually have high priority over many external CSS rules.",
            "href" => "Destination of a link or related resource.",
            "src" => "Source URL for an image, script, iframe, or another embedded resource.",
            "alt" => "Alternative text for images. Important for accessibility and visual fallback.",
            "role" => "Semantic role used by assistive technologies when native HTML semantics are not enough.",
            "aria-label" => "Explicit accessible name for screen readers.",
            "aria-hidden" => "Indicates whether the element should be hidden from assistive technologies.",
            "data-dt" => "Custom data-* attribute. It is defined by the page or library and has no standard HTML meaning.",
            _ when name.StartsWith("data-", StringComparison.OrdinalIgnoreCase) => "Custom application attribute. It often stores state, identifiers, or JavaScript data.",
            _ when name.StartsWith("aria-", StringComparison.OrdinalIgnoreCase) => "ARIA attribute related to accessibility and assistive technologies.",
            _ => "HTML attribute for the element. Its meaning depends on the tag and how the page uses it."
        };
    }
}
