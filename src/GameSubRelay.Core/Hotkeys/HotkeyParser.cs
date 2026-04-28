using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace GameSubRelay.Core.Hotkeys;

public static class HotkeyParser
{
    private static readonly Dictionary<string, string> KeyNameMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["ENTER"] = "Enter",
        ["RETURN"] = "Enter",
        ["ESC"] = "Esc",
        ["ESCAPE"] = "Esc",
        ["SPACE"] = "Space",
        ["SPACEBAR"] = "Space",
        ["TAB"] = "Tab",
        ["BACKSPACE"] = "Backspace",
        ["BACK"] = "Backspace",
        ["UP"] = "Up",
        ["DOWN"] = "Down",
        ["LEFT"] = "Left",
        ["RIGHT"] = "Right",
        ["PGUP"] = "PageUp",
        ["PAGEUP"] = "PageUp",
        ["PGDN"] = "PageDown",
        ["PAGEDOWN"] = "PageDown"
    };

    private static readonly HashSet<string> ValidFunctionKeys = Enumerable.Range(1, 24)
        .Select(i => $"F{i}")
        .ToHashSet(StringComparer.OrdinalIgnoreCase);

    private static readonly HashSet<string> ValidSingleKeys = new(
        new[]
        {
            "Tab",
            "Space",
            "Enter",
            "Esc",
            "Backspace",
            "Insert",
            "Delete",
            "Home",
            "End",
            "PageUp",
            "PageDown",
            "Left",
            "Right",
            "Up",
            "Down",
            "A", "B", "C", "D", "E", "F", "G", "H", "I", "J", "K", "L", "M", "N", "O", "P",
            "Q", "R", "S", "T", "U", "V", "W", "X", "Y", "Z",
            "0", "1", "2", "3", "4", "5", "6", "7", "8", "9"
        },
        StringComparer.OrdinalIgnoreCase);

    public static bool TryParse(string? input, out HotkeyGesture gesture, out string? error)
    {
        gesture = null!;
        error = null;

        if (string.IsNullOrWhiteSpace(input))
        {
            error = "Input is empty.";
            return false;
        }

        var parts = input
            .Split('+', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

        if (parts.Length < 2)
        {
            error = "At least one modifier and one key are required.";
            return false;
        }

        HotkeyModifiers modifiers = 0;
        string? key = null;
        var consumedModifiers = new HashSet<HotkeyModifiers>(EnumComparer.Instance);

        foreach (var rawPart in parts)
        {
            if (!TryNormalizeToken(rawPart, out var token))
            {
                error = $"Unknown key '{rawPart}'.";
                return false;
            }

            if (TryParseModifier(token, out var modifier))
            {
                if (!consumedModifiers.Add(modifier))
                {
                    error = $"Modifier '{token}' is duplicated.";
                    return false;
                }

                modifiers |= modifier;
                continue;
            }

            if (key is not null)
            {
                error = $"Only one key is supported ('{key}' already set).";
                return false;
            }

            if (!IsValidKey(token))
            {
                error = $"Unsupported key '{token}'.";
                return false;
            }

            key = token;
        }

        if (key is null || modifiers == 0)
        {
            error = "A modifier and a key are required.";
            return false;
        }

        gesture = new HotkeyGesture(modifiers, key);
        return true;
    }

    public static HotkeyParseResult ParseOrThrow(string input)
    {
        return TryParse(input, out var gesture, out var error)
            ? new HotkeyParseResult(gesture, null)
            : new HotkeyParseResult(null, error!);
    }

    private static bool IsValidKey(string token)
    {
        if (ValidFunctionKeys.Contains(token))
        {
            return true;
        }

        if (ValidSingleKeys.Contains(token))
        {
            return true;
        }

        if (token.Length == 1 && char.IsLetterOrDigit(token[0]))
        {
            return true;
        }

        return false;
    }

    private static bool TryParseModifier(string token, out HotkeyModifiers modifier)
    {
        modifier = HotkeyModifiers.None;
        switch (token)
        {
            case "Ctrl":
            case "Control":
                modifier = HotkeyModifiers.Ctrl;
                return true;
            case "Alt":
                modifier = HotkeyModifiers.Alt;
                return true;
            case "Shift":
                modifier = HotkeyModifiers.Shift;
                return true;
            case "Win":
            case "Windows":
                modifier = HotkeyModifiers.Win;
                return true;
        }

        return false;
    }

    private static bool TryNormalizeToken(string raw, out string normalized)
    {
        normalized = raw.Trim();
        if (normalized.Length == 0)
        {
            return false;
        }

        if (KeyNameMap.TryGetValue(normalized.ToUpperInvariant(), out var mapped))
        {
            normalized = mapped;
            return true;
        }

        if (normalized.Length == 1)
        {
            normalized = normalized.ToUpperInvariant();
        }

        normalized = CultureInfo.InvariantCulture.TextInfo.ToTitleCase(normalized.ToLowerInvariant());
        return true;
    }

    private sealed class EnumComparer : IEqualityComparer<HotkeyModifiers>
    {
        public static readonly EnumComparer Instance = new();

        public bool Equals(HotkeyModifiers x, HotkeyModifiers y) => x == y;
        public int GetHashCode(HotkeyModifiers obj) => ((int)obj).GetHashCode();
    }
}

public sealed record HotkeyParseResult(HotkeyGesture? Gesture, string? Error)
{
    public bool IsSuccess => Gesture is not null && Error is null;
}
