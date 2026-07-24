using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;
using Julco.Cdp;

namespace Julco.UI;

public sealed record CommonIssue(
    string Severity,
    string Category,
    string Title,
    string Details,
    string Recommendation,
    string Evidence)
{
    public string DisplayText => $"{Severity} | {Category} | {Title}";
}

public static class CommonIssueDetector
{
    private static readonly Regex TextRegex = new(@"<[^>]+>", RegexOptions.Compiled);

    public static IReadOnlyList<CommonIssue> Detect(SelectorInspectionResult inspection)
    {
        var issues = new List<CommonIssue>();
        DetectLowContrast(inspection, issues);
        DetectInvisibleElement(inspection, issues);
        DetectOverflowRisk(inspection, issues);
        DetectZIndexRisk(inspection, issues);
        DetectImagesWithoutAlt(inspection, issues);
        DetectButtonsWithoutLabel(inspection, issues);

        return issues
            .OrderBy(issue => SeverityRank(issue.Severity))
            .ThenBy(issue => issue.Category)
            .ThenBy(issue => issue.Title)
            .ToArray();
    }

    public static string BuildReport(IReadOnlyList<CommonIssue> issues)
    {
        if (issues.Count == 0)
        {
            return "No common issues detected for the selected element.";
        }

        return string.Join(
            Environment.NewLine + Environment.NewLine,
            issues.Select((issue, index) => string.Join(
                Environment.NewLine,
                $"{index + 1}. {issue.Title}",
                $"Severity: {issue.Severity}",
                $"Category: {issue.Category}",
                $"Details: {issue.Details}",
                $"Recommendation: {issue.Recommendation}",
                $"Evidence: {issue.Evidence}")));
    }

    private static void DetectLowContrast(SelectorInspectionResult inspection, List<CommonIssue> issues)
    {
        if (!TryGetStyleColor(inspection.ComputedStyle, "color", out var foreground)
            || !TryGetStyleColor(inspection.ComputedStyle, "background-color", out var background)
            || background.Alpha < 0.95)
        {
            return;
        }

        var ratio = ContrastRatio(foreground, background);
        if (ratio < 3)
        {
            issues.Add(new CommonIssue(
                "High",
                "Accessibility",
                "Very low text contrast",
                $"The computed text/background contrast is about {ratio:0.00}:1.",
                "Use a darker foreground, lighter background, or stronger font treatment. WCAG usually expects at least 4.5:1 for normal text.",
                $"color={inspection.ComputedStyle["color"]}; background-color={inspection.ComputedStyle["background-color"]}"));
        }
        else if (ratio < 4.5)
        {
            issues.Add(new CommonIssue(
                "Medium",
                "Accessibility",
                "Potentially low text contrast",
                $"The computed text/background contrast is about {ratio:0.00}:1.",
                "Review the design contrast, especially for body text or small UI labels.",
                $"color={inspection.ComputedStyle["color"]}; background-color={inspection.ComputedStyle["background-color"]}"));
        }
    }

    private static void DetectInvisibleElement(SelectorInspectionResult inspection, List<CommonIssue> issues)
    {
        var display = GetStyle(inspection, "display");
        var visibility = GetStyle(inspection, "visibility");
        var opacity = GetStyle(inspection, "opacity");

        if (display.Equals("none", StringComparison.OrdinalIgnoreCase))
        {
            issues.Add(new CommonIssue(
                "High",
                "Visibility",
                "Element removed from layout",
                "The element uses display:none, so it is not visible and does not occupy layout space.",
                "Check whether this state is intentional. If the user should see it, remove display:none or inspect the state logic.",
                "display:none"));
        }

        if (visibility.Equals("hidden", StringComparison.OrdinalIgnoreCase)
            || visibility.Equals("collapse", StringComparison.OrdinalIgnoreCase))
        {
            issues.Add(new CommonIssue(
                "High",
                "Visibility",
                "Element visually hidden",
                $"The element uses visibility:{visibility}.",
                "Use visibility:visible when the element should be perceivable, or confirm this hidden state is expected.",
                $"visibility:{visibility}"));
        }

        if (double.TryParse(opacity, NumberStyles.Float, CultureInfo.InvariantCulture, out var opacityValue)
            && opacityValue <= 0.05)
        {
            issues.Add(new CommonIssue(
                "High",
                "Visibility",
                "Element is nearly transparent",
                $"The element opacity is {opacityValue:0.##}.",
                "Raise opacity or verify that the element is intentionally hidden/faded.",
                $"opacity:{opacity}"));
        }

        if (IsZeroSize(GetStyle(inspection, "width")) || IsZeroSize(GetStyle(inspection, "height")))
        {
            issues.Add(new CommonIssue(
                "Medium",
                "Visibility",
                "Element has zero-sized dimension",
                $"The computed box includes width={GetStyle(inspection, "width")} and height={GetStyle(inspection, "height")}.",
                "Check layout constraints, content loading, or parent sizing if the element should be visible.",
                $"width:{GetStyle(inspection, "width")}; height:{GetStyle(inspection, "height")}"));
        }
    }

    private static void DetectOverflowRisk(SelectorInspectionResult inspection, List<CommonIssue> issues)
    {
        var overflow = GetStyle(inspection, "overflow");
        var overflowX = GetStyle(inspection, "overflow-x");
        var overflowY = GetStyle(inspection, "overflow-y");
        var whiteSpace = GetStyle(inspection, "white-space");
        var textOverflow = GetStyle(inspection, "text-overflow");
        var width = GetStyle(inspection, "width");
        var height = GetStyle(inspection, "height");

        if (IsClippingOverflow(overflow) || IsClippingOverflow(overflowX) || IsClippingOverflow(overflowY))
        {
            issues.Add(new CommonIssue(
                "Medium",
                "Layout",
                "Content may be clipped by overflow",
                "The element uses an overflow mode that can hide content outside the box.",
                "Inspect whether expected text, images, or child controls are cut off at the frame edges.",
                $"overflow:{overflow}; overflow-x:{overflowX}; overflow-y:{overflowY}; width:{width}; height:{height}"));
        }

        if (whiteSpace.Contains("nowrap", StringComparison.OrdinalIgnoreCase))
        {
            issues.Add(new CommonIssue(
                "Low",
                "Layout",
                "Text may overflow horizontally",
                "white-space prevents normal line wrapping.",
                "If text can vary in length, consider wrapping, responsive sizing, or deliberate ellipsis.",
                $"white-space:{whiteSpace}; text-overflow:{textOverflow}"));
        }
    }

    private static void DetectZIndexRisk(SelectorInspectionResult inspection, List<CommonIssue> issues)
    {
        var position = GetStyle(inspection, "position");
        var zIndex = GetStyle(inspection, "z-index");
        if (zIndex.Equals("auto", StringComparison.OrdinalIgnoreCase)
            || !int.TryParse(zIndex, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
        {
            return;
        }

        if (Math.Abs(value) >= 1000)
        {
            issues.Add(new CommonIssue(
                "Medium",
                "Layering",
                "Suspiciously high z-index",
                $"The element has z-index {value} with position {position}.",
                "Large z-index values can create hard-to-debug stacking problems. Prefer local stacking contexts and smaller scales.",
                $"position:{position}; z-index:{zIndex}"));
        }
        else if (value < 0)
        {
            issues.Add(new CommonIssue(
                "Medium",
                "Layering",
                "Negative z-index",
                $"The element has z-index {value}.",
                "Negative stacking can place content behind backgrounds or make it hard to interact with.",
                $"position:{position}; z-index:{zIndex}"));
        }
    }

    private static void DetectImagesWithoutAlt(SelectorInspectionResult inspection, List<CommonIssue> issues)
    {
        var tag = inspection.TagName.ToLowerInvariant();
        var isImageElement = tag is "img" or "image";
        var elementAltMissing = isImageElement
            && (!inspection.Attributes.TryGetValue("alt", out var alt) || string.IsNullOrWhiteSpace(alt));
        var imageResourcesWithoutAlt = inspection.Images
            .Where(image => image.Kind is "img" or "image" or "srcset" or "source" or "source-srcset")
            .Count(image => string.IsNullOrWhiteSpace(image.Alt));

        if (elementAltMissing)
        {
            issues.Add(new CommonIssue(
                "High",
                "Accessibility",
                "Image element has no alt text",
                "The selected image does not expose an alt attribute.",
                "Add meaningful alt text for informative images, or alt=\"\" for decorative images.",
                "tag=img; alt missing"));
        }

        if (imageResourcesWithoutAlt > 0)
        {
            issues.Add(new CommonIssue(
                "Medium",
                "Accessibility",
                "Image resources may lack alternative text",
                $"{imageResourcesWithoutAlt} detected image resource(s) have no alt/label metadata.",
                "Review image purpose and add alt text or accessible labels where appropriate.",
                $"images without alt={imageResourcesWithoutAlt}"));
        }
    }

    private static void DetectButtonsWithoutLabel(SelectorInspectionResult inspection, List<CommonIssue> issues)
    {
        if (!IsButtonLike(inspection))
        {
            return;
        }

        var accessibleName = GetFirstAttribute(
            inspection.Attributes,
            "aria-label",
            "aria-labelledby",
            "title",
            "value",
            "alt");
        var text = ExtractVisibleText(inspection.OuterHtml);
        if (!string.IsNullOrWhiteSpace(accessibleName) || !string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        issues.Add(new CommonIssue(
            "High",
            "Accessibility",
            "Button-like element has no accessible label",
            "The selected element behaves like a button but no visible text or accessible label was detected.",
            "Add text content, aria-label, aria-labelledby, title, or an appropriate accessible name.",
            $"tag={inspection.TagName}; role={GetAttribute(inspection.Attributes, "role")}"));
    }

    private static bool IsButtonLike(SelectorInspectionResult inspection)
    {
        var tag = inspection.TagName.ToLowerInvariant();
        var role = GetAttribute(inspection.Attributes, "role").ToLowerInvariant();
        var type = GetAttribute(inspection.Attributes, "type").ToLowerInvariant();
        return tag == "button"
            || role == "button"
            || tag == "summary"
            || tag == "input" && type is "button" or "submit" or "reset" or "image";
    }

    private static string ExtractVisibleText(string html)
    {
        var text = TextRegex.Replace(html, " ");
        return WebUtility.HtmlDecode(text).Trim();
    }

    private static string GetFirstAttribute(IReadOnlyDictionary<string, string> attributes, params string[] names)
    {
        return names
            .Select(name => GetAttribute(attributes, name))
            .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))
            ?? string.Empty;
    }

    private static string GetAttribute(IReadOnlyDictionary<string, string> attributes, string name)
    {
        return attributes.TryGetValue(name, out var value) ? value : string.Empty;
    }

    private static string GetStyle(SelectorInspectionResult inspection, string name)
    {
        return inspection.ComputedStyle.TryGetValue(name, out var value) ? value : string.Empty;
    }

    private static bool IsZeroSize(string value)
    {
        return value.Equals("0", StringComparison.OrdinalIgnoreCase)
            || value.Equals("0px", StringComparison.OrdinalIgnoreCase)
            || value.Equals("0.00px", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsClippingOverflow(string value)
    {
        return value.Equals("hidden", StringComparison.OrdinalIgnoreCase)
            || value.Equals("clip", StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryGetStyleColor(
        IReadOnlyDictionary<string, string> computedStyle,
        string property,
        out CssColor color)
    {
        color = default;
        return computedStyle.TryGetValue(property, out var value) && TryParseColor(value, out color);
    }

    private static bool TryParseColor(string value, out CssColor color)
    {
        color = default;
        var text = value.Trim();
        if (text.Equals("transparent", StringComparison.OrdinalIgnoreCase))
        {
            color = new CssColor(0, 0, 0, 0);
            return true;
        }

        var rgbMatch = Regex.Match(
            text,
            @"rgba?\(\s*(?<r>[\d.]+)\s*,\s*(?<g>[\d.]+)\s*,\s*(?<b>[\d.]+)(?:\s*,\s*(?<a>[\d.]+))?\s*\)",
            RegexOptions.IgnoreCase);
        if (rgbMatch.Success)
        {
            color = new CssColor(
                ParseByte(rgbMatch.Groups["r"].Value),
                ParseByte(rgbMatch.Groups["g"].Value),
                ParseByte(rgbMatch.Groups["b"].Value),
                rgbMatch.Groups["a"].Success
                    ? Math.Clamp(double.Parse(rgbMatch.Groups["a"].Value, CultureInfo.InvariantCulture), 0, 1)
                    : 1);
            return true;
        }

        if (text.StartsWith('#') && (text.Length == 7 || text.Length == 4))
        {
            if (text.Length == 7)
            {
                color = new CssColor(
                    Convert.ToInt32(text.Substring(1, 2), 16),
                    Convert.ToInt32(text.Substring(3, 2), 16),
                    Convert.ToInt32(text.Substring(5, 2), 16),
                    1);
                return true;
            }

            color = new CssColor(
                Convert.ToInt32(new string(text[1], 2), 16),
                Convert.ToInt32(new string(text[2], 2), 16),
                Convert.ToInt32(new string(text[3], 2), 16),
                1);
            return true;
        }

        return false;
    }

    private static int ParseByte(string value)
    {
        return (int)Math.Clamp(double.Parse(value, CultureInfo.InvariantCulture), 0, 255);
    }

    private static double ContrastRatio(CssColor a, CssColor b)
    {
        var l1 = RelativeLuminance(a);
        var l2 = RelativeLuminance(b);
        var lighter = Math.Max(l1, l2);
        var darker = Math.Min(l1, l2);
        return (lighter + 0.05) / (darker + 0.05);
    }

    private static double RelativeLuminance(CssColor color)
    {
        static double ConvertChannel(double value)
        {
            value /= 255.0;
            return value <= 0.03928
                ? value / 12.92
                : Math.Pow((value + 0.055) / 1.055, 2.4);
        }

        return 0.2126 * ConvertChannel(color.R)
            + 0.7152 * ConvertChannel(color.G)
            + 0.0722 * ConvertChannel(color.B);
    }

    private static int SeverityRank(string severity)
    {
        return severity.ToLowerInvariant() switch
        {
            "high" => 0,
            "medium" => 1,
            "low" => 2,
            _ => 3
        };
    }

    private readonly record struct CssColor(
        int R,
        int G,
        int B,
        double Alpha);
}
