namespace Julco.Core.Configuration;

public sealed record CaptureSettings(
    string GlobalShortcut,
    string ScreenshotDirectory,
    string FileNamePattern)
{
    public static CaptureSettings Default { get; } = new(
        "Win+Shift+D",
        "",
        "julco-{date}-{time}-{tag}");
}
