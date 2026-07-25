namespace Julco.Core.Configuration;

public sealed record AppSettings(
    ThemeMode Theme,
    string Language,
    CaptureSettings Capture,
    ExportSettings Export,
    HistorySettings History,
    PrivacySettings Privacy,
    KeyboardShortcutSettings Keyboard,
    IssueTrackerSettings IssueTrackers,
    UiSettings Ui)
{
    public static AppSettings Default { get; } = new(
        ThemeMode.Dark,
        "en-US",
        CaptureSettings.Default,
        ExportSettings.Default,
        HistorySettings.Default,
        PrivacySettings.Default,
        KeyboardShortcutSettings.Default,
        IssueTrackerSettings.Default,
        UiSettings.Default);

    public AppSettings Normalized()
    {
        return this with
        {
            Language = string.IsNullOrWhiteSpace(Language)
                ? Default.Language
                : Language,
            Capture = Capture ?? CaptureSettings.Default,
            Export = Export ?? ExportSettings.Default,
            History = History ?? HistorySettings.Default,
            Privacy = Privacy ?? PrivacySettings.Default,
            Keyboard = (Keyboard ?? KeyboardShortcutSettings.Default).Normalized(),
            IssueTrackers = (IssueTrackers ?? IssueTrackerSettings.Default).Normalized(),
            Ui = Ui ?? UiSettings.Default
        };
    }

    public AppSettings WithProtectedSecrets()
    {
        return Normalized() with
        {
            IssueTrackers = IssueTrackers.WithProtectedSecrets()
        };
    }
}
