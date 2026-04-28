using GameSubRelay.Core.Hotkeys;

namespace GameSubRelay.Infrastructure.Hotkeys;

public sealed record Win32HotkeyMapping(uint Modifiers, uint VirtualKey);

public static class GlobalHotkeyMapping
{
    private const uint ModAlt = 0x0001;
    private const uint ModControl = 0x0002;
    private const uint ModShift = 0x0004;
    private const uint ModWin = 0x0008;

    private static readonly Dictionary<string, uint> NamedKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Tab"] = 0x09,
        ["Enter"] = 0x0D,
        ["Esc"] = 0x1B,
        ["Space"] = 0x20,
        ["PageUp"] = 0x21,
        ["PageDown"] = 0x22,
        ["End"] = 0x23,
        ["Home"] = 0x24,
        ["Left"] = 0x25,
        ["Up"] = 0x26,
        ["Right"] = 0x27,
        ["Down"] = 0x28,
        ["Insert"] = 0x2D,
        ["Delete"] = 0x2E,
        ["Backspace"] = 0x08
    };

    public static bool TryCreate(string gestureText, out Win32HotkeyMapping? mapping, out string? error)
    {
        mapping = null;
        if (!HotkeyParser.TryParse(gestureText, out var gesture, out error))
        {
            return false;
        }

        if (!TryGetVirtualKey(gesture.Key, out var virtualKey))
        {
            error = $"Unsupported key '{gesture.Key}'.";
            return false;
        }

        mapping = new Win32HotkeyMapping(ToWin32Modifiers(gesture.Modifiers), virtualKey);
        error = null;
        return true;
    }

    private static uint ToWin32Modifiers(HotkeyModifiers modifiers)
    {
        uint result = 0;
        if (modifiers.HasFlag(HotkeyModifiers.Alt))
        {
            result |= ModAlt;
        }

        if (modifiers.HasFlag(HotkeyModifiers.Ctrl))
        {
            result |= ModControl;
        }

        if (modifiers.HasFlag(HotkeyModifiers.Shift))
        {
            result |= ModShift;
        }

        if (modifiers.HasFlag(HotkeyModifiers.Win))
        {
            result |= ModWin;
        }

        return result;
    }

    private static bool TryGetVirtualKey(string key, out uint virtualKey)
    {
        if (key.Length == 1 && char.IsLetterOrDigit(key[0]))
        {
            virtualKey = char.ToUpperInvariant(key[0]);
            return true;
        }

        if (key.Length is 2 or 3 &&
            key[0] is 'F' or 'f' &&
            int.TryParse(key[1..], out var functionNumber) &&
            functionNumber is >= 1 and <= 24)
        {
            virtualKey = (uint)(0x70 + functionNumber - 1);
            return true;
        }

        return NamedKeys.TryGetValue(key, out virtualKey);
    }
}
