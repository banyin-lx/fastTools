using System.Windows.Input;

namespace FastTools.App.Models;

public sealed class HotKeyGesture
{
    public required ModifierKeys Modifiers { get; init; }

    public required Key Key { get; init; }

    public override string ToString()
    {
        var parts = new List<string>();
        if (Modifiers.HasFlag(ModifierKeys.Control))
        {
            parts.Add("Ctrl");
        }

        if (Modifiers.HasFlag(ModifierKeys.Alt))
        {
            parts.Add("Alt");
        }

        if (Modifiers.HasFlag(ModifierKeys.Shift))
        {
            parts.Add("Shift");
        }

        if (Modifiers.HasFlag(ModifierKeys.Windows))
        {
            parts.Add("Win");
        }

        parts.Add(Key.ToString());
        return string.Join("+", parts);
    }

    public static bool TryParse(string? value, out HotKeyGesture? gesture)
    {
        gesture = null;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var tokens = value.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (tokens.Length == 0)
        {
            return false;
        }

        var modifiers = ModifierKeys.None;
        Key? key = null;

        foreach (var token in tokens)
        {
            switch (token.ToLowerInvariant())
            {
                case "ctrl":
                case "control":
                    modifiers |= ModifierKeys.Control;
                    break;
                case "alt":
                    modifiers |= ModifierKeys.Alt;
                    break;
                case "shift":
                    modifiers |= ModifierKeys.Shift;
                    break;
                case "win":
                case "windows":
                    modifiers |= ModifierKeys.Windows;
                    break;
                default:
                    var converter = new KeyConverter();
                    if (converter.ConvertFromString(token) is not Key parsedKey)
                    {
                        return false;
                    }

                    key = parsedKey;
                    break;
            }
        }

        if (key is null)
        {
            return false;
        }

        if (IsModifierKey(key.Value))
        {
            return false;
        }

        gesture = new HotKeyGesture
        {
            Modifiers = modifiers,
            Key = key.Value,
        };
        return true;
    }

    private static bool IsModifierKey(Key key)
    {
        return key is Key.LeftCtrl or Key.RightCtrl or
            Key.LeftAlt or Key.RightAlt or
            Key.LeftShift or Key.RightShift or
            Key.LWin or Key.RWin;
    }
}
