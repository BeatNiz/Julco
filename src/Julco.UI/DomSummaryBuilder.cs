using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace Julco.UI;

public static class DomSummaryBuilder
{
    private static readonly Regex TagRegex = new(
        @"<(?<closing>/)?(?<tag>[a-zA-Z][a-zA-Z0-9:-]*)(?<attrs>[^>]*)>",
        RegexOptions.Compiled);

    private static readonly Regex AttributeRegex = new(
        @"(?<name>[^\s=\/<>]+)(?:\s*=\s*(?:""(?<value>[^""]*)""|'(?<value>[^']*)'|(?<value>[^\s""'=<>`]+)))?",
        RegexOptions.Compiled);

    private static readonly HashSet<string> VoidTags = new(StringComparer.OrdinalIgnoreCase)
    {
        "area", "base", "br", "col", "embed", "hr", "img", "input", "link", "meta",
        "param", "source", "track", "wbr"
    };

    private static readonly string[] ImportantAttributes =
    {
        "id",
        "class",
        "role",
        "aria-label",
        "aria-labelledby",
        "aria-hidden",
        "type",
        "name",
        "href",
        "src",
        "alt",
        "title",
        "data-testid",
        "data-test",
        "data-id"
    };

    public static DomSummary Build(string tagName, string selector, string html, IReadOnlyDictionary<string, string> attributes)
    {
        var root = ParseRoot(html);
        var rootAttributes = root?.Attributes.Count > 0
            ? root.Attributes
            : attributes;
        var rootTag = !string.IsNullOrWhiteSpace(root?.TagName)
            ? root!.TagName
            : tagName.ToLowerInvariant();

        return new DomSummary(
            rootTag,
            selector,
            rootAttributes,
            BuildIdentity(rootTag, rootAttributes),
            BuildImportantAttributes(rootAttributes),
            BuildTree(root),
            BuildNearby(root));
    }

    public static string ToDisplayText(DomSummary summary)
    {
        var builder = new StringBuilder();
        builder.AppendLine("SELECTED ELEMENT");
        builder.AppendLine("----------------");
        builder.AppendLine(summary.Identity);
        builder.AppendLine($"Selector: {summary.Selector}");

        var id = summary.Attributes.GetValueOrDefault("id");
        var classes = summary.Attributes.GetValueOrDefault("class");
        builder.AppendLine($"id: {ValueOrDash(id)}");
        builder.AppendLine($"classes: {ValueOrDash(classes)}");

        builder.AppendLine();
        builder.AppendLine("IMPORTANT ATTRIBUTES");
        builder.AppendLine("--------------------");
        if (summary.ImportantAttributes.Count == 0)
        {
            builder.AppendLine("No priority attributes found.");
        }
        else
        {
            foreach (var attribute in summary.ImportantAttributes)
            {
                builder.AppendLine($"{attribute.Name,-18} {attribute.Value}");
            }
        }

        builder.AppendLine();
        builder.AppendLine("NEARBY DOM TREE");
        builder.AppendLine("---------------");
        builder.AppendLine(summary.TreeText);

        builder.AppendLine();
        builder.AppendLine("CHILDREN SNAPSHOT");
        builder.AppendLine("-----------------");
        builder.AppendLine(summary.NearbyText);

        return builder.ToString().TrimEnd();
    }

    private static DomNode? ParseRoot(string html)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return null;
        }

        DomNode? root = null;
        var stack = new Stack<DomNode>();
        foreach (Match match in TagRegex.Matches(html))
        {
            var isClosing = match.Groups["closing"].Success;
            var tag = match.Groups["tag"].Value;
            if (isClosing)
            {
                if (stack.Count > 0)
                {
                    stack.Pop();
                }

                continue;
            }

            var attrs = ParseAttributes(match.Groups["attrs"].Value);
            var node = new DomNode(tag.ToLowerInvariant(), attrs);
            if (stack.Count > 0)
            {
                stack.Peek().Children.Add(node);
            }
            else
            {
                root ??= node;
            }

            var raw = match.Value;
            var isSelfClosing = raw.EndsWith("/>", StringComparison.Ordinal) || VoidTags.Contains(tag);
            if (!isSelfClosing)
            {
                stack.Push(node);
            }
        }

        return root;
    }

    private static IReadOnlyDictionary<string, string> ParseAttributes(string rawAttributes)
    {
        return AttributeRegex.Matches(rawAttributes)
            .Select(match => new
            {
                Name = match.Groups["name"].Value,
                Value = WebUtility.HtmlDecode(match.Groups["value"].Value)
            })
            .Where(item => !string.IsNullOrWhiteSpace(item.Name))
            .GroupBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => group.First().Value,
                StringComparer.OrdinalIgnoreCase);
    }

    private static string BuildIdentity(string tagName, IReadOnlyDictionary<string, string> attributes)
    {
        var builder = new StringBuilder(tagName.ToUpperInvariant());
        if (attributes.TryGetValue("id", out var id) && !string.IsNullOrWhiteSpace(id))
        {
            builder.Append("  #").Append(id);
        }

        if (attributes.TryGetValue("class", out var classes) && !string.IsNullOrWhiteSpace(classes))
        {
            foreach (var className in classes.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Take(4))
            {
                builder.Append("  .").Append(className);
            }
        }

        return builder.ToString();
    }

    private static IReadOnlyList<DomAttributeView> BuildImportantAttributes(IReadOnlyDictionary<string, string> attributes)
    {
        return ImportantAttributes
            .Where(attributes.ContainsKey)
            .Select(name => new DomAttributeView(name, attributes[name], DescribeImportantAttribute(name)))
            .Concat(attributes
                .Where(item => item.Key.StartsWith("aria-", StringComparison.OrdinalIgnoreCase)
                    || item.Key.StartsWith("data-", StringComparison.OrdinalIgnoreCase))
                .Where(item => !ImportantAttributes.Contains(item.Key, StringComparer.OrdinalIgnoreCase))
                .Take(8)
                .Select(item => new DomAttributeView(item.Key, item.Value, DescribeImportantAttribute(item.Key))))
            .ToArray();
    }

    private static string BuildTree(DomNode? root)
    {
        if (root is null)
        {
            return "No DOM tree available.";
        }

        var builder = new StringBuilder();
        AppendNode(builder, root, 0, maxDepth: 2, maxChildren: 6);
        return builder.ToString().TrimEnd();
    }

    private static string BuildNearby(DomNode? root)
    {
        if (root is null)
        {
            return "No nearby nodes available.";
        }

        if (root.Children.Count == 0)
        {
            return "The selected element has no captured child elements.";
        }

        var builder = new StringBuilder();
        foreach (var child in root.Children.Take(10))
        {
            builder.AppendLine($"- {SummarizeNode(child)}");
        }

        if (root.Children.Count > 10)
        {
            builder.AppendLine($"- ... {root.Children.Count - 10} more child elements");
        }

        return builder.ToString().TrimEnd();
    }

    private static void AppendNode(StringBuilder builder, DomNode node, int depth, int maxDepth, int maxChildren)
    {
        builder.Append(new string(' ', depth * 2));
        builder.AppendLine(SummarizeNode(node));

        if (depth >= maxDepth)
        {
            if (node.Children.Count > 0)
            {
                builder.Append(new string(' ', (depth + 1) * 2));
                builder.AppendLine($"... {node.Children.Count} nested elements");
            }

            return;
        }

        foreach (var child in node.Children.Take(maxChildren))
        {
            AppendNode(builder, child, depth + 1, maxDepth, maxChildren);
        }

        if (node.Children.Count > maxChildren)
        {
            builder.Append(new string(' ', (depth + 1) * 2));
            builder.AppendLine($"... {node.Children.Count - maxChildren} more elements");
        }
    }

    private static string SummarizeNode(DomNode node)
    {
        var summary = new StringBuilder("<").Append(node.TagName);
        if (node.Attributes.TryGetValue("id", out var id) && !string.IsNullOrWhiteSpace(id))
        {
            summary.Append(" #").Append(id);
        }

        if (node.Attributes.TryGetValue("class", out var classes) && !string.IsNullOrWhiteSpace(classes))
        {
            foreach (var className in classes.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Take(3))
            {
                summary.Append(" .").Append(className);
            }
        }

        if (node.Attributes.TryGetValue("role", out var role) && !string.IsNullOrWhiteSpace(role))
        {
            summary.Append(" role=").Append(role);
        }

        summary.Append('>');
        return summary.ToString();
    }

    private static string DescribeImportantAttribute(string name)
    {
        return name.ToLowerInvariant() switch
        {
            "id" => "Unique identifier for this element.",
            "class" => "CSS class list used for styling and behavior hooks.",
            "role" => "Accessibility role exposed to assistive technologies.",
            "aria-label" => "Accessible name for screen readers.",
            "aria-labelledby" => "References another element that names this element.",
            "aria-hidden" => "Controls whether assistive technologies should ignore the element.",
            "type" => "Element subtype, especially useful for inputs and buttons.",
            "name" => "Form or scripting name.",
            "href" => "Link destination.",
            "src" => "Embedded resource source.",
            "alt" => "Alternative text for an image.",
            "title" => "Additional advisory text.",
            _ when name.StartsWith("data-", StringComparison.OrdinalIgnoreCase) => "Custom application data attribute.",
            _ when name.StartsWith("aria-", StringComparison.OrdinalIgnoreCase) => "Accessibility-related ARIA attribute.",
            _ => "Important element attribute."
        };
    }

    private static string ValueOrDash(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? "-" : value;
    }

    private sealed record DomNode(
        string TagName,
        IReadOnlyDictionary<string, string> Attributes)
    {
        public List<DomNode> Children { get; } = new();
    }
}

public sealed record DomSummary(
    string TagName,
    string Selector,
    IReadOnlyDictionary<string, string> Attributes,
    string Identity,
    IReadOnlyList<DomAttributeView> ImportantAttributes,
    string TreeText,
    string NearbyText);
