using System.Windows.Input;

namespace Julco.UI;

public sealed record HotkeyParseResult(
    bool IsEnabled,
    ModifierKeys Modifiers,
    Key Key,
    string DisplayText,
    string? Error)
{
    public static HotkeyParseResult Disabled { get; } = new(
        false,
        ModifierKeys.None,
        Key.None,
        string.Empty,
        null);
}
