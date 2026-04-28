using System;

namespace GameSubRelay.Infrastructure.Hotkeys;

public sealed record GlobalHotkeyBinding(string Name, string Gesture)
{
    public override string ToString() => $"{Name}:{Gesture}";
}

