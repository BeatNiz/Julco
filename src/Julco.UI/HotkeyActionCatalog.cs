using Julco.Core.Configuration;

namespace Julco.UI;

public static class HotkeyActionCatalog
{
    public static IReadOnlyList<HotkeyActionDescriptor> All { get; } = new[]
    {
        new HotkeyActionDescriptor(KeyboardShortcutSettings.ToggleLens, "Toggle lens", "Open or close the lens frame."),
        new HotkeyActionDescriptor(KeyboardShortcutSettings.CaptureLens, "Capture lens", "Save the framed evidence package."),
        new HotkeyActionDescriptor(KeyboardShortcutSettings.NextResultTab, "Next result tab", "Move through DOM, CSS, Console, Attributes, and Images."),
        new HotkeyActionDescriptor(KeyboardShortcutSettings.OpenDom, "Open DOM", "Open the DOM result window."),
        new HotkeyActionDescriptor(KeyboardShortcutSettings.OpenCss, "Open CSS", "Open the CSS result window."),
        new HotkeyActionDescriptor(KeyboardShortcutSettings.OpenImages, "Open images", "Open the images preview window.")
    };
}
