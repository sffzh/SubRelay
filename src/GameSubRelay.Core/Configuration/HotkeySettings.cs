namespace GameSubRelay.Core.Configuration;

public sealed record HotkeySettings(
    string ToggleOverlay,
    string ToggleEditMode,
    string ClearCaptions)
{
    public static HotkeySettings Default => new(
        ToggleOverlay: "Ctrl+Alt+S",
        ToggleEditMode: "Ctrl+Alt+E",
        ClearCaptions: "Ctrl+Alt+C");

    public HotkeySettings Normalize() => this with
    {
        ToggleOverlay = NormalizeHotkey(ToggleOverlay),
        ToggleEditMode = NormalizeHotkey(ToggleEditMode),
        ClearCaptions = NormalizeHotkey(ClearCaptions)
    };

    private static string NormalizeHotkey(string value)
    {
        if (GameSubRelay.Core.Hotkeys.HotkeyParser.TryParse(value, out var gesture, out _))
        {
            return gesture.ToCanonicalString();
        }

        return value?.Trim() ?? string.Empty;
    }
}
