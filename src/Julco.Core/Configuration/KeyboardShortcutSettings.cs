namespace Julco.Core.Configuration;

public sealed record KeyboardShortcutSettings(
    bool EnableGlobalShortcuts,
    bool EnableLocalShortcuts,
    Dictionary<string, string> GlobalShortcuts,
    Dictionary<string, string> LocalShortcuts)
{
    public const string ToggleLens = "toggle-lens";
    public const string CaptureLens = "capture-lens";
    public const string NextResultTab = "next-result-tab";
    public const string OpenDom = "open-dom";
    public const string OpenCss = "open-css";
    public const string OpenImages = "open-images";

    public static KeyboardShortcutSettings Default { get; } = new(
        EnableGlobalShortcuts: true,
        EnableLocalShortcuts: true,
        GlobalShortcuts: new Dictionary<string, string>
        {
            [ToggleLens] = "Ctrl+Alt+Shift+L",
            [CaptureLens] = "Ctrl+Alt+Shift+C",
            [NextResultTab] = "Ctrl+Alt+Shift+Right",
            [OpenDom] = "Ctrl+Alt+Shift+D",
            [OpenCss] = "Ctrl+Alt+Shift+S",
            [OpenImages] = "Ctrl+Alt+Shift+I"
        },
        LocalShortcuts: new Dictionary<string, string>
        {
            [ToggleLens] = "Ctrl+Shift+L",
            [CaptureLens] = "Ctrl+Shift+C",
            [NextResultTab] = "Ctrl+Shift+Tab",
            [OpenDom] = "Ctrl+Shift+D",
            [OpenCss] = "Ctrl+Shift+S",
            [OpenImages] = "Ctrl+Shift+I"
        });

    public KeyboardShortcutSettings Normalized()
    {
        return this with
        {
            GlobalShortcuts = MergeWithDefaults(GlobalShortcuts, Default.GlobalShortcuts),
            LocalShortcuts = MergeWithDefaults(LocalShortcuts, Default.LocalShortcuts)
        };
    }

    private static Dictionary<string, string> MergeWithDefaults(
        Dictionary<string, string>? configured,
        Dictionary<string, string> defaults)
    {
        var merged = defaults.ToDictionary(
            pair => pair.Key,
            pair => pair.Value,
            StringComparer.OrdinalIgnoreCase);

        if (configured is null)
        {
            return merged;
        }

        foreach (var pair in configured)
        {
            if (merged.ContainsKey(pair.Key))
            {
                merged[pair.Key] = pair.Value ?? string.Empty;
            }
        }

        return merged;
    }
}
