using System.Windows.Input;

namespace Julco.UI;

public sealed record HotkeyDefinition(
    int Id,
    string Name,
    ModifierKeys Modifiers,
    Key Key,
    string DisplayText,
    Action Action);
