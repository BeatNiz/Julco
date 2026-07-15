namespace Julco.Core.Configuration;

public sealed record AppSettings(
    ThemeMode Theme,
    string Language,
    CaptureSettings Capture,
    ExportSettings Export,
    HistorySettings History,
    UiSettings Ui)
{
    public static AppSettings Default { get; } = new(
        ThemeMode.Dark,
        "en-US",
        CaptureSettings.Default,
        ExportSettings.Default,
        HistorySettings.Default,
        UiSettings.Default);
}
