using Julco.Core.Configuration;

namespace Julco.UI;

public static class OnboardingStepIds
{
    public const string ChooseProfile = "choose-profile";
    public const string OpenBrowser = "open-browser";
    public const string LoadTabs = "load-tabs";
    public const string Inspect = "inspect";
    public const string OpenLens = "open-lens";
    public const string CaptureEvidence = "capture-evidence";
    public const string ReviewLibrary = "review-library";
    public const string Privacy = "privacy";

    public static IReadOnlyList<string> Ordered { get; } = new[]
    {
        ChooseProfile,
        OpenBrowser,
        LoadTabs,
        Inspect,
        OpenLens,
        CaptureEvidence,
        ReviewLibrary,
        Privacy
    };
}

public sealed record OnboardingContext(
    bool HasProfile,
    bool HasActiveBrowser,
    int TabCount,
    bool HasSelectedTab,
    bool HasInspection,
    bool IsLensOpen,
    int CaptureCount,
    bool HasSelectedCapture,
    bool PrivacyEnabled,
    string CompletedSteps);

public sealed record OnboardingCard(
    string StepId,
    string Title,
    string Body,
    string PrimaryAction,
    string SecondaryAction,
    int CompletedCount,
    int TotalCount)
{
    public string ProgressText => $"{CompletedCount}/{TotalCount} complete";
}

public static class OnboardingAdvisor
{
    public static OnboardingCard Build(OnboardingContext context)
    {
        var completed = Parse(context.CompletedSteps);
        AddAutomaticCompletion(context, completed);
        var completedCount = Math.Min(completed.Count(step => OnboardingStepIds.Ordered.Contains(step)), OnboardingStepIds.Ordered.Count);
        var stepId = OnboardingStepIds.Ordered.FirstOrDefault(step => !completed.Contains(step));
        if (stepId is null)
        {
            return Card("complete", "Julco is ready", "The core inspection flow is ready. Use Help anytime for ports, shortcuts, privacy, evidence, and browser reminders.", "Help", "Hide", completedCount);
        }

        return stepId switch
        {
            OnboardingStepIds.ChooseProfile => Card(stepId, "Choose your review mode", "Start by picking QA, Frontend, Design review, or Accessibility. Julco will reorder details and reports around that goal.", "Use current profile", "Help", completedCount),
            OnboardingStepIds.OpenBrowser => Card(stepId, "Open a browser from Julco", "Use one of the browser logo buttons so Julco starts the browser with a local inspection port ready.", "Open Chrome", "Help", completedCount),
            OnboardingStepIds.LoadTabs => Card(stepId, "Load inspectable tabs", "After a browser opens, press Tabs and choose the page you want to inspect.", "Tabs", "Skip", completedCount),
            OnboardingStepIds.Inspect => Card(stepId, "Inspect a selector", "Keep `body` for a broad first read, or paste a selector such as `button`, `#app`, or `.card`.", "Inspect", "Skip", completedCount),
            OnboardingStepIds.OpenLens => Card(stepId, "Try the lens", "The lens lets you frame a visual region, snap to elements, preview images, and collect evidence from what is inside.", "Lens", "Skip", completedCount),
            OnboardingStepIds.CaptureEvidence => Card(stepId, "Create evidence", "Capture the lens to save screenshot, DOM, CSS, console, attributes, images, notes, and reports as a clean package.", "Capture", "Skip", completedCount),
            OnboardingStepIds.ReviewLibrary => Card(stepId, "Review your capture library", "Use Gallery, Table, Groups, Timeline, favorites, tags, and saved filters to manage captured evidence.", "Library", "Skip", completedCount),
            OnboardingStepIds.Privacy => Card(stepId, "Check privacy before sharing", "Open Privacy Preview to see exactly what Julco will redact before reports or issue tracker handoffs.", "Privacy", "Done", completedCount),
            _ => Card(OnboardingStepIds.Privacy, "Julco is ready", "The core flow is available. Use Help anytime for ports, shortcuts, and evidence workflow reminders.", "Help", "Hide", completedCount)
        };
    }

    public static string MarkCompleted(string completedSteps, string stepId)
    {
        var completed = Parse(completedSteps);
        completed.Add(stepId);
        return string.Join(",", OnboardingStepIds.Ordered.Where(completed.Contains));
    }

    public static string Reset() => string.Empty;

    private static OnboardingCard Card(string id, string title, string body, string primary, string secondary, int completed)
    {
        return new OnboardingCard(id, title, body, primary, secondary, completed, OnboardingStepIds.Ordered.Count);
    }

    private static HashSet<string> Parse(string completedSteps)
    {
        return string.IsNullOrWhiteSpace(completedSteps)
            ? new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            : completedSteps.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private static void AddAutomaticCompletion(OnboardingContext context, HashSet<string> completed)
    {
        if (context.HasProfile) completed.Add(OnboardingStepIds.ChooseProfile);
        if (context.HasActiveBrowser) completed.Add(OnboardingStepIds.OpenBrowser);
        if (context.TabCount > 0) completed.Add(OnboardingStepIds.LoadTabs);
        if (context.HasInspection) completed.Add(OnboardingStepIds.Inspect);
        if (context.IsLensOpen) completed.Add(OnboardingStepIds.OpenLens);
        if (context.CaptureCount > 0) completed.Add(OnboardingStepIds.CaptureEvidence);
        if (context.HasSelectedCapture || context.CaptureCount > 0) completed.Add(OnboardingStepIds.ReviewLibrary);
    }
}
