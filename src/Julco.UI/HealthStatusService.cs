using System.IO;
using Julco.Core.Configuration;

namespace Julco.UI;

public sealed class HealthStatusService
{
    public IReadOnlyList<HealthStatusItem> Build(HealthStatusContext context)
    {
        return new[]
        {
            BuildBrowserHealth(context),
            BuildPortHealth(context),
            BuildTabsHealth(context),
            BuildInspectionHealth(context),
            BuildLensHealth(context),
            BuildCaptureFolderHealth(context),
            BuildCaptureHistoryHealth(context),
            BuildPrivacyHealth(context),
            BuildIssueTrackerHealth(context),
            BuildShortcutHealth(context),
            BuildProfileHealth(context)
        };
    }

    private static HealthStatusItem BuildBrowserHealth(HealthStatusContext context)
    {
        return string.IsNullOrWhiteSpace(context.ActiveBrowserName)
            ? Warn("Browser", "Not started", "Open Chrome, Edge, Firefox, or Opera from Julco before inspecting.")
            : Ok("Browser", context.ActiveBrowserName, $"{context.ActiveBrowserName} remote session is the active target family.");
    }

    private static HealthStatusItem BuildPortHealth(HealthStatusContext context)
    {
        return context.IsPortValid
            ? Ok("Remote port", context.PortText, $"{context.PortLabel} endpoint configured on localhost:{context.PortText}.")
            : Warn("Remote port", "Invalid", "Port must be a number between 1 and 65535.");
    }

    private static HealthStatusItem BuildTabsHealth(HealthStatusContext context)
    {
        if (context.TabCount <= 0)
        {
            return Warn("Inspectable tabs", "None", "Open browser tabs and press Tabs to refresh target discovery.");
        }

        return Ok("Inspectable tabs", context.TabCount.ToString(), context.SelectedTargetDescription);
    }

    private static HealthStatusItem BuildInspectionHealth(HealthStatusContext context)
    {
        return string.IsNullOrWhiteSpace(context.CurrentInspectionTag)
            ? Warn("Inspection", "Idle", "Inspect a selector or use Lens to populate DOM, CSS, console, attributes, images, and issues.")
            : Ok("Inspection", context.CurrentInspectionTag, $"{context.CurrentInspectionSelector} on {context.SelectedTargetUrl}");
    }

    private static HealthStatusItem BuildLensHealth(HealthStatusContext context)
    {
        if (!context.IsLensActive)
        {
            return Warn("Lens", "Inactive", "Open Lens before capturing a framed evidence package.");
        }

        return Ok(
            "Lens",
            context.LensState,
            $"Frame {context.LensFrameWidth:0}x{context.LensFrameHeight:0}, center {context.LensCenterX:0},{context.LensCenterY:0}. Type: {context.LensDetectedType}.");
    }

    private static HealthStatusItem BuildCaptureFolderHealth(HealthStatusContext context)
    {
        try
        {
            Directory.CreateDirectory(context.CaptureRootDirectory);
            return Ok("Capture folder", "Ready", context.CaptureRootDirectory);
        }
        catch (Exception exception)
        {
            return Warn("Capture folder", "Blocked", exception.Message);
        }
    }

    private static HealthStatusItem BuildCaptureHistoryHealth(HealthStatusContext context)
    {
        var detail = string.IsNullOrWhiteSpace(context.SelectedCaptureTitle)
            ? $"{context.CaptureCount} capture(s) loaded. No capture selected."
            : $"{context.FilteredCaptureCount}/{context.CaptureCount} visible. Selected: {context.SelectedCaptureTitle}";
        return context.CaptureCount == 0
            ? Warn("Capture history", "Empty", "Create a lens capture to start the evidence history.")
            : Ok("Capture history", context.CaptureCount.ToString(), detail);
    }

    private static HealthStatusItem BuildPrivacyHealth(HealthStatusContext context)
    {
        var privacy = context.Settings.Privacy;
        if (!privacy.RedactOnExport)
        {
            return Warn("Privacy", "Off", "Redaction is disabled for exports and issue handoff drafts.");
        }

        var screenshotPolicy = privacy.IncludeScreenshotsInSafeExports
            ? "Safe exports may include unredacted screenshots."
            : "Safe exports omit screenshots by default.";
        return Ok("Privacy", "Protected", $"Redaction is enabled. {screenshotPolicy}");
    }

    private static HealthStatusItem BuildIssueTrackerHealth(HealthStatusContext context)
    {
        var issueTrackers = (context.Settings.IssueTrackers ?? IssueTrackerSettings.Default).Normalized();
        var ready = new List<string>();
        var enabledButMissing = new List<string>();
        if (issueTrackers.EnableGitHub)
        {
            if (issueTrackers.IsGitHubConfigured)
            {
                ready.Add($"GitHub {issueTrackers.GitHubOwner}/{issueTrackers.GitHubRepository}");
            }
            else
            {
                enabledButMissing.Add("GitHub");
            }
        }

        if (issueTrackers.EnableJira)
        {
            if (issueTrackers.IsJiraConfigured)
            {
                ready.Add($"Jira {issueTrackers.JiraProjectKey}");
            }
            else
            {
                enabledButMissing.Add("Jira");
            }
        }

        if (enabledButMissing.Count > 0)
        {
            return Warn(
                "Issue trackers",
                "Needs setup",
                $"{string.Join(", ", enabledButMissing)} enabled but missing required settings or token.");
        }

        return ready.Count == 0
            ? Ok("Issue trackers", "Local drafts", "GitHub/Jira submission is optional and currently disabled.")
            : Ok("Issue trackers", "Connected", string.Join("; ", ready));
    }

    private static HealthStatusItem BuildShortcutHealth(HealthStatusContext context)
    {
        var global = CountEnabledShortcuts(context.Settings.Keyboard.GlobalShortcuts);
        var local = CountEnabledShortcuts(context.Settings.Keyboard.LocalShortcuts);
        if (!context.Settings.Keyboard.EnableGlobalShortcuts && !context.Settings.Keyboard.EnableLocalShortcuts)
        {
            return Warn("Shortcuts", "Off", "Global and local shortcuts are disabled in Settings.");
        }

        return Ok(
            "Shortcuts",
            $"{global}/{local}",
            $"Global enabled: {context.Settings.Keyboard.EnableGlobalShortcuts}. Local enabled: {context.Settings.Keyboard.EnableLocalShortcuts}.");
    }

    private static HealthStatusItem BuildProfileHealth(HealthStatusContext context)
    {
        return string.IsNullOrWhiteSpace(context.ProfileName)
            ? Warn("Profile", "Loading", "Usage profiles are still being initialized.")
            : Ok("Profile", context.ProfileName, context.ProfileGuidance);
    }

    private static int CountEnabledShortcuts(IReadOnlyDictionary<string, string> shortcuts)
    {
        return shortcuts.Values.Count(value => HotkeyTextParser.Parse(value).IsEnabled);
    }

    private static HealthStatusItem Ok(string name, string state, string detail)
    {
        return new HealthStatusItem(name, state, detail, "OK");
    }

    private static HealthStatusItem Warn(string name, string state, string detail)
    {
        return new HealthStatusItem(name, state, detail, "Warning");
    }
}

public sealed record HealthStatusContext(
    AppSettings Settings,
    string? ActiveBrowserName,
    bool IsPortValid,
    string PortText,
    string PortLabel,
    int TabCount,
    string SelectedTargetDescription,
    string SelectedTargetUrl,
    string? CurrentInspectionTag,
    string CurrentInspectionSelector,
    bool IsLensActive,
    string LensState,
    double LensFrameWidth,
    double LensFrameHeight,
    double LensCenterX,
    double LensCenterY,
    string LensDetectedType,
    string CaptureRootDirectory,
    int CaptureCount,
    int FilteredCaptureCount,
    string? SelectedCaptureTitle,
    string ProfileName,
    string ProfileGuidance);
