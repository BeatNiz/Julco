namespace Julco.UI;

public sealed record CaptureReportProfileTemplate(
    string Name,
    string Focus,
    IReadOnlyList<string> PrioritySignals,
    IReadOnlyList<string> ReviewChecklist,
    IReadOnlyList<string> RecommendedNextSteps,
    IReadOnlyList<string> SectionOrder)
{
    public static CaptureReportProfileTemplate FromReport(CaptureReport report)
    {
        var profile = report.UsageProfile.Trim();
        if (profile.Equals("Frontend", StringComparison.OrdinalIgnoreCase))
        {
            return Frontend;
        }

        if (profile.Equals("Design review", StringComparison.OrdinalIgnoreCase)
            || profile.Equals("DesignReview", StringComparison.OrdinalIgnoreCase))
        {
            return DesignReview;
        }

        if (profile.Equals("Accessibility", StringComparison.OrdinalIgnoreCase))
        {
            return Accessibility;
        }

        return QA;
    }

    public static CaptureReportProfileTemplate QA { get; } = new(
        "QA",
        "Reproducibility, visible behavior, regression risk, issue evidence, and validation status.",
        new[]
        {
            "Common issues and console messages",
            "Browser, URL, selector, and exact lens frame",
            "Notes status, severity, tags, and reproduction context",
            "Before/after comparison readiness"
        },
        new[]
        {
            "Can the issue be reproduced with the captured URL, browser, and selector?",
            "Does the screenshot show the problematic state clearly?",
            "Are console messages relevant to the defect?",
            "Is the severity/status current enough for triage?"
        },
        new[]
        {
            "Attach the safe report package to the ticket.",
            "Add missing reproduction steps in capture notes.",
            "Compare against a fixed capture after the change lands."
        },
        new[] { "Issues", "Notes", "Technical", "Console", "Attributes", "CSS", "DOM", "Images" });

    public static CaptureReportProfileTemplate Frontend { get; } = new(
        "Frontend",
        "DOM structure, selectors, computed CSS, CSS-rule debugging, and implementation handoff.",
        new[]
        {
            "Selector and element identity",
            "Computed CSS and matched visual/layout properties",
            "DOM context and attributes",
            "Console messages that may affect rendering"
        },
        new[]
        {
            "Is the selected selector stable enough for debugging or tests?",
            "Which computed declarations explain the current visual state?",
            "Are layout properties such as display, position, overflow, z-index, or object-fit suspicious?",
            "Does the DOM expose a simpler parent/child target for the fix?"
        },
        new[]
        {
            "Start with computed CSS and DOM before visual discussion.",
            "Copy the safe CSS/DOM snippets into the implementation task.",
            "Re-capture after the fix to compare technical deltas."
        },
        new[] { "Technical", "CSS", "DOM", "Attributes", "Issues", "Console", "Images", "Notes" });

    public static CaptureReportProfileTemplate DesignReview { get; } = new(
        "Design review",
        "Visual fidelity, screenshot evidence, image resources, spacing, sizing, and presentation quality.",
        new[]
        {
            "Screenshot and exact lens frame",
            "Image resources, natural/displayed sizes, and formats",
            "Computed visual properties and attributes",
            "Notes describing expected visual behavior"
        },
        new[]
        {
            "Does the framed screenshot communicate the visual mismatch?",
            "Are displayed and natural image sizes appropriate?",
            "Do object-fit, overflow, margins, dimensions, or background styles explain the issue?",
            "Are notes clear enough for design review without reopening the page?"
        },
        new[]
        {
            "Share HTML/Markdown report with the screenshot visible.",
            "Attach relevant image URLs or saved resources.",
            "Use comparison after visual adjustments are made."
        },
        new[] { "Images", "Technical", "CSS", "Attributes", "Issues", "Notes", "DOM", "Console" });

    public static CaptureReportProfileTemplate Accessibility { get; } = new(
        "Accessibility",
        "Accessible names, roles, keyboard affordances, contrast, visibility, labels, and semantic risk.",
        new[]
        {
            "Common accessibility issues and contrast warnings",
            "Attributes such as role, aria-*, alt, title, tabindex, type, and disabled",
            "DOM semantics around the selected element",
            "Computed visibility, display, opacity, pointer-events, and overflow"
        },
        new[]
        {
            "Does the element have a usable accessible label/name?",
            "Do roles and attributes match the visible control?",
            "Could keyboard users reach and understand this element?",
            "Are contrast, visibility, or disabled states creating an accessibility failure?"
        },
        new[]
        {
            "Copy the issue draft into the accessibility tracker.",
            "Document user impact in notes.",
            "Verify after remediation with a new capture."
        },
        new[] { "Issues", "Attributes", "DOM", "CSS", "Technical", "Notes", "Console", "Images" });
}
