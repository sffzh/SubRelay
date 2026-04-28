using System;
using System.Collections.Generic;
using System.Linq;

namespace GameSubRelay.Core.Hotkeys;

public sealed record HotkeyBindings(
    HotkeyGesture ToggleOverlay,
    HotkeyGesture ToggleEditMode,
    HotkeyGesture ClearCaptions)
{
    public IReadOnlyList<string> Validate()
    {
        var conflicts = new List<string>();
        var all = new[]
        {
            (Binding: nameof(ToggleOverlay), Gesture: ToggleOverlay),
            (Binding: nameof(ToggleEditMode), Gesture: ToggleEditMode),
            (Binding: nameof(ClearCaptions), Gesture: ClearCaptions)
        };

        var groups = all
            .GroupBy(item => item.Gesture, new HotkeyGestureComparer())
            .Select(group => new { Gesture = group.Key, Bindings = group.Select(item => item.Binding).ToList() })
            .Where(group => group.Bindings.Count > 1);

        foreach (var group in groups)
        {
            conflicts.Add($"{group.Gesture} duplicated on {string.Join(", ", group.Bindings)}");
        }

        return conflicts;
    }
}

public sealed class HotkeyGestureComparer : IEqualityComparer<HotkeyGesture>
{
    public bool Equals(HotkeyGesture? x, HotkeyGesture? y)
    {
        if (ReferenceEquals(x, y))
        {
            return true;
        }

        if (x is null || y is null)
        {
            return false;
        }

        return x.Modifiers == y.Modifiers && string.Equals(x.Key, y.Key, StringComparison.OrdinalIgnoreCase);
    }

    public int GetHashCode(HotkeyGesture obj)
        => HashCode.Combine(obj.Modifiers, obj.Key.ToUpperInvariant());
}
