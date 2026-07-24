using System.Globalization;
using System.Windows.Input;

namespace Julco.UI;

public static class HotkeyTextParser
{
    public static HotkeyParseResult Parse(string? text)
    {
        var normalizedText = Normalize(text);
        if (string.IsNullOrWhiteSpace(normalizedText))
        {
            return HotkeyParseResult.Disabled;
        }

        var parts = normalizedText
            .Split('+', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
        {
            return HotkeyParseResult.Disabled;
        }

        var modifiers = ModifierKeys.None;
        Key? key = null;
        foreach (var part in parts)
        {
            if (TryParseModifier(part, out var modifier))
            {
                modifiers |= modifier;
                continue;
            }

            if (key is not null)
            {
                return Invalid(normalizedText, "Use only one final key.");
            }

            if (!TryParseKey(part, out var parsedKey))
            {
                return Invalid(normalizedText, $"Unknown key '{part}'.");
            }

            key = parsedKey;
        }

        if (key is null)
        {
            return Invalid(normalizedText, "Add a final key after the modifiers.");
        }

        if (modifiers == ModifierKeys.None)
        {
            return Invalid(normalizedText, "Add at least one modifier such as Ctrl, Alt, Shift, or Win.");
        }

        return new HotkeyParseResult(
            true,
            modifiers,
            key.Value,
            ToDisplayText(modifiers, key.Value),
            null);
    }

    public static string Normalize(string? text)
    {
        return string.Join(
            "+",
            (text ?? string.Empty)
                .Replace("Control", "Ctrl", StringComparison.OrdinalIgnoreCase)
                .Split('+', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries));
    }

    public static string ToDisplayText(ModifierKeys modifiers, Key key)
    {
        var parts = new List<string>();
        if (modifiers.HasFlag(ModifierKeys.Control))
        {
            parts.Add("Ctrl");
        }

        if (modifiers.HasFlag(ModifierKeys.Alt))
        {
            parts.Add("Alt");
        }

        if (modifiers.HasFlag(ModifierKeys.Shift))
        {
            parts.Add("Shift");
        }

        if (modifiers.HasFlag(ModifierKeys.Windows))
        {
            parts.Add("Win");
        }

        parts.Add(KeyInterop.VirtualKeyFromKey(key) == 0 ? key.ToString() : FormatKey(key));
        return string.Join("+", parts);
    }

    private static HotkeyParseResult Invalid(string displayText, string message)
    {
        return new HotkeyParseResult(
            false,
            ModifierKeys.None,
            Key.None,
            displayText,
            message);
    }

    private static bool TryParseModifier(string value, out ModifierKeys modifier)
    {
        modifier = value.Trim().ToLowerInvariant() switch
        {
            "ctrl" => ModifierKeys.Control,
            "control" => ModifierKeys.Control,
            "alt" => ModifierKeys.Alt,
            "shift" => ModifierKeys.Shift,
            "win" => ModifierKeys.Windows,
            "windows" => ModifierKeys.Windows,
            "meta" => ModifierKeys.Windows,
            _ => ModifierKeys.None
        };

        return modifier != ModifierKeys.None;
    }

    private static bool TryParseKey(string value, out Key key)
    {
        var aliases = new Dictionary<string, Key>(StringComparer.OrdinalIgnoreCase)
        {
            ["Esc"] = Key.Escape,
            ["Escape"] = Key.Escape,
            ["Del"] = Key.Delete,
            ["Delete"] = Key.Delete,
            ["Ins"] = Key.Insert,
            ["Insert"] = Key.Insert,
            ["Left"] = Key.Left,
            ["Right"] = Key.Right,
            ["Up"] = Key.Up,
            ["Down"] = Key.Down,
            ["Space"] = Key.Space,
            ["Tab"] = Key.Tab,
            ["Enter"] = Key.Enter,
            ["Return"] = Key.Return
        };

        if (aliases.TryGetValue(value, out key))
        {
            return true;
        }

        if (value.Length == 1 && char.IsLetterOrDigit(value[0]))
        {
            var keyName = char.IsDigit(value[0])
                ? $"D{value[0]}"
                : value.ToUpper(CultureInfo.InvariantCulture);
            return Enum.TryParse(keyName, ignoreCase: true, out key);
        }

        return Enum.TryParse(value, ignoreCase: true, out key)
            && key != Key.None
            && key != Key.System
            && key != Key.ImeProcessed
            && key != Key.DeadCharProcessed;
    }

    private static string FormatKey(Key key)
    {
        return key switch
        {
            >= Key.D0 and <= Key.D9 => key.ToString()[1..],
            Key.Return => "Enter",
            _ => key.ToString()
        };
    }
}
