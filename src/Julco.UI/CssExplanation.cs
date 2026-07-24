namespace Julco.UI;

public sealed record CssExplanation(
    string Property,
    string Value,
    string Category,
    string Explanation,
    string PracticalHint);

public static class CssExplanationBuilder
{
    private static readonly IReadOnlyDictionary<string, CssPropertyHelp> Help = new Dictionary<string, CssPropertyHelp>(StringComparer.OrdinalIgnoreCase)
    {
        ["display"] = new("Layout", "Defines how the element participates in layout.", "Block, flex, grid, inline, and none strongly affect size, flow, and visibility."),
        ["position"] = new("Layout", "Controls how the element is positioned in relation to normal flow or ancestors.", "Relative/absolute/fixed/sticky can explain unexpected offsets or overlap."),
        ["z-index"] = new("Layering", "Controls stacking order when the element is positioned or in a stacking context.", "A high or low value can explain why something appears above or behind another element."),
        ["overflow"] = new("Clipping", "Controls what happens when content exceeds the element box.", "Hidden/clip can cut content; auto/scroll can create scrollbars."),
        ["overflow-x"] = new("Clipping", "Controls horizontal overflow behavior.", "Useful when content is clipped sideways or creates horizontal scrolling."),
        ["overflow-y"] = new("Clipping", "Controls vertical overflow behavior.", "Useful when content is clipped vertically or creates internal scroll."),
        ["object-fit"] = new("Media", "Controls how replaced content such as images or videos fit inside their box.", "Cover can crop; contain can letterbox; fill can distort."),
        ["object-position"] = new("Media", "Controls the alignment of an image or video inside its content box.", "Explains why a cropped image shows the wrong area."),
        ["visibility"] = new("Visibility", "Controls whether the element is visible while still occupying layout space.", "Hidden elements keep their space but cannot be seen."),
        ["opacity"] = new("Visibility", "Controls transparency of the element and its children.", "A low value can make text or images look disabled or invisible."),
        ["color"] = new("Color", "Controls text color.", "Compare with background-color to detect low contrast."),
        ["background-color"] = new("Color", "Controls the background fill color.", "Important for contrast and visual grouping."),
        ["font-size"] = new("Typography", "Controls text size.", "Very small values can cause readability problems."),
        ["font-weight"] = new("Typography", "Controls text thickness.", "Low weight on low contrast backgrounds can reduce legibility."),
        ["line-height"] = new("Typography", "Controls vertical rhythm inside text lines.", "Too small can make text collide; too large can break compact layouts."),
        ["white-space"] = new("Typography", "Controls how spaces and line breaks behave.", "nowrap often explains text overflow."),
        ["text-overflow"] = new("Typography", "Controls how clipped inline text is signaled.", "ellipsis hides part of the text intentionally."),
        ["width"] = new("Box model", "Controls preferred content width.", "Fixed widths can break responsive layouts."),
        ["height"] = new("Box model", "Controls preferred content height.", "Fixed heights often cause clipping with dynamic content."),
        ["min-width"] = new("Box model", "Sets the smallest allowed width.", "Can prevent a component from shrinking on small screens."),
        ["min-height"] = new("Box model", "Sets the smallest allowed height.", "Can force extra vertical space."),
        ["max-width"] = new("Box model", "Sets the largest allowed width.", "Useful for responsive constraints."),
        ["max-height"] = new("Box model", "Sets the largest allowed height.", "Can create clipping or scroll areas."),
        ["box-sizing"] = new("Box model", "Defines whether width/height include padding and border.", "border-box is usually easier to reason about than content-box."),
        ["margin"] = new("Spacing", "Controls space outside the element.", "Unexpected margins often explain gaps or alignment issues."),
        ["padding"] = new("Spacing", "Controls space inside the element.", "Large padding can make buttons or cards oversized."),
        ["border"] = new("Box model", "Controls border width, style, and color.", "Borders change perceived size and visual grouping."),
        ["border-radius"] = new("Shape", "Controls corner rounding.", "Very large radius can make controls feel pill-shaped."),
        ["inset"] = new("Positioning", "Shorthand for top/right/bottom/left offsets.", "Important for absolute, fixed, or sticky elements."),
        ["top"] = new("Positioning", "Vertical offset for positioned elements.", "Can explain vertical displacement."),
        ["right"] = new("Positioning", "Right offset for positioned elements.", "Can explain horizontal anchoring."),
        ["bottom"] = new("Positioning", "Bottom offset for positioned elements.", "Can explain bottom anchoring."),
        ["left"] = new("Positioning", "Left offset for positioned elements.", "Can explain horizontal displacement."),
        ["transform"] = new("Transform", "Applies visual transforms such as translate, scale, rotate, or skew.", "Transforms can move an element without changing layout measurements."),
        ["flex-direction"] = new("Flexbox", "Controls the main direction in a flex container.", "Row/column changes how children line up."),
        ["align-items"] = new("Flexbox/Grid", "Aligns children across the cross axis.", "Often explains vertical centering or stretching."),
        ["justify-content"] = new("Flexbox/Grid", "Distributes children along the main axis.", "Often explains spacing between items."),
        ["gap"] = new("Flexbox/Grid", "Controls spacing between flex or grid children.", "Useful for diagnosing repeated item spacing."),
        ["grid-template-columns"] = new("Grid", "Defines grid column tracks.", "Explains column widths and responsive grid behavior."),
        ["grid-template-rows"] = new("Grid", "Defines grid row tracks.", "Explains row heights and repeated layout bands."),
        ["cursor"] = new("Interaction", "Controls the mouse cursor shown over the element.", "Helps identify interactive or disabled-looking controls."),
        ["pointer-events"] = new("Interaction", "Controls whether the element can receive pointer input.", "none can make visible controls unclickable.")
    };

    private static readonly string[] Priority =
    {
        "display",
        "position",
        "z-index",
        "overflow",
        "overflow-x",
        "overflow-y",
        "object-fit",
        "object-position",
        "visibility",
        "opacity",
        "color",
        "background-color",
        "font-size",
        "font-weight",
        "line-height",
        "white-space",
        "text-overflow",
        "width",
        "height",
        "min-width",
        "min-height",
        "max-width",
        "max-height",
        "box-sizing",
        "margin",
        "padding",
        "border",
        "border-radius",
        "inset",
        "top",
        "right",
        "bottom",
        "left",
        "transform",
        "flex-direction",
        "align-items",
        "justify-content",
        "gap",
        "grid-template-columns",
        "grid-template-rows",
        "cursor",
        "pointer-events"
    };

    public static IReadOnlyList<CssExplanation> Build(IReadOnlyDictionary<string, string> computedStyle)
    {
        return Priority
            .Where(computedStyle.ContainsKey)
            .Select(property =>
            {
                var help = Help[property];
                return new CssExplanation(
                    property,
                    computedStyle[property],
                    help.Category,
                    help.Explanation,
                    BuildValueHint(property, computedStyle[property], help.PracticalHint));
            })
            .ToArray();
    }

    public static string BuildSummaryText(IReadOnlyDictionary<string, string> computedStyle)
    {
        var explanations = Build(computedStyle);
        if (explanations.Count == 0)
        {
            return "No explained CSS properties were found in the current computed style.";
        }

        return string.Join(
            Environment.NewLine,
            explanations.Select(item =>
                $"{item.Property}: {item.Value}{Environment.NewLine}  {item.Explanation}{Environment.NewLine}  {item.PracticalHint}"));
    }

    private static string BuildValueHint(string property, string value, string fallback)
    {
        var normalized = value.Trim().ToLowerInvariant();
        return property.ToLowerInvariant() switch
        {
            "display" when normalized == "none" => "The element is removed from layout and will not be visible.",
            "display" when normalized == "flex" => "Children are arranged by flexbox; check direction, alignment, justification, and gap.",
            "display" when normalized == "grid" => "Children are arranged by CSS grid; template rows/columns explain placement.",
            "position" when normalized is "absolute" or "fixed" => "The element may overlap normal content because it is taken out of regular flow.",
            "position" when normalized == "sticky" => "The element can stick while scrolling; top/bottom offsets matter.",
            "z-index" when int.TryParse(normalized, out var z) && z != 0 => $"Stacking is explicitly shifted with z-index {z}.",
            "overflow" or "overflow-x" or "overflow-y" when normalized is "hidden" or "clip" => "Content outside the box can be clipped.",
            "object-fit" when normalized == "cover" => "The media fills the box and may be cropped.",
            "object-fit" when normalized == "contain" => "The whole media is visible, possibly with empty space around it.",
            "visibility" when normalized == "hidden" => "The element occupies space but is visually hidden.",
            "opacity" when double.TryParse(normalized, out var opacity) && opacity < 1 => $"The element is transparent at {opacity:P0}.",
            "white-space" when normalized.Contains("nowrap", StringComparison.Ordinal) => "Text will not wrap and may overflow horizontally.",
            "pointer-events" when normalized == "none" => "The element will ignore mouse/touch interaction.",
            _ => fallback
        };
    }

    private sealed record CssPropertyHelp(
        string Category,
        string Explanation,
        string PracticalHint);
}
