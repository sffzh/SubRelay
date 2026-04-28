using System;
using GameSubRelay.Core.Hotkeys;

namespace GameSubRelay.Core.Configuration;

public sealed record AppSettings(
    AudioSettings Audio,
    TranslationSettings Translation,
    OverlaySettings Overlay,
    HotkeySettings Hotkeys,
    TtsSettings Tts,
    DateTimeOffset? LastUpdatedUtc = null)
{
    public static AppSettings Default => new(
        Audio: AudioSettings.Default,
        Translation: TranslationSettings.Default,
        Overlay: OverlaySettings.Default,
        Hotkeys: HotkeySettings.Default,
        Tts: TtsSettings.Default,
        LastUpdatedUtc: DateTimeOffset.UtcNow);

    public AppSettings Normalize()
    {
        var audio = Audio ?? AudioSettings.Default;
        var tts = Tts ?? new TtsSettings(
            audio.TtsEnabled,
            audio.TtsForMicrophone,
            audio.TtsForMonitor,
            TtsSettings.Default.MaxQueueLength);
        if (Tts is not null &&
            tts.MaxQueueLength == 0 &&
            (audio.TtsEnabled || audio.TtsForMicrophone || audio.TtsForMonitor))
        {
            tts = new TtsSettings(
                audio.TtsEnabled,
                audio.TtsForMicrophone,
                audio.TtsForMonitor,
                TtsSettings.Default.MaxQueueLength);
        }
        var normalizedTts = tts.Normalize();

        return this with
        {
            Audio = audio.Normalize(normalizedTts),
            Translation = (Translation ?? TranslationSettings.Default).Normalize(),
            Overlay = (Overlay ?? OverlaySettings.Default).Normalize(),
            Hotkeys = (Hotkeys ?? HotkeySettings.Default).Normalize(),
            Tts = normalizedTts
        };
    }

    public AppSettingsValidationResult Validate()
    {
        var issues = new List<AppSettingsValidationIssue>();
        var hotkeys = Hotkeys ?? HotkeySettings.Default;
        var parsedHotkeys = new List<(string Name, string Path, HotkeyGesture Gesture)>();

        AddParsedHotkey(hotkeys.ToggleOverlay, "ToggleOverlay", "hotkeys.toggleOverlay");
        AddParsedHotkey(hotkeys.ToggleEditMode, "ToggleEditMode", "hotkeys.toggleEditMode");
        AddParsedHotkey(hotkeys.ClearCaptions, "ClearCaptions", "hotkeys.clearCaptions");

        var duplicateGroups = parsedHotkeys
            .GroupBy(item => item.Gesture, new HotkeyGestureComparer())
            .Where(group => group.Count() > 1);

        foreach (var group in duplicateGroups)
        {
            issues.Add(new AppSettingsValidationIssue(
                "hotkeys",
                $"{group.Key} duplicated on {string.Join(", ", group.Select(item => item.Name))}."));
        }

        var tts = Tts ?? TtsSettings.Default;
        if (!tts.Enabled && tts.UseForMicrophone)
        {
            issues.Add(new AppSettingsValidationIssue(
                "tts.useForMicrophone",
                "tts.useForMicrophone requires tts.enabled to be true."));
        }

        if (!tts.Enabled && tts.UseForMonitor)
        {
            issues.Add(new AppSettingsValidationIssue(
                "tts.useForMonitor",
                "tts.useForMonitor requires tts.enabled to be true."));
        }

        if (tts.Enabled && !tts.UseForMicrophone && !tts.UseForMonitor)
        {
            issues.Add(new AppSettingsValidationIssue(
                "tts",
                "tts.enabled requires at least one TTS channel to be enabled."));
        }

        if (tts.MaxQueueLength < 1)
        {
            issues.Add(new AppSettingsValidationIssue(
                "tts.maxQueueLength",
                "tts.maxQueueLength must be at least 1."));
        }

        return new AppSettingsValidationResult(issues);

        void AddParsedHotkey(string value, string name, string path)
        {
            if (HotkeyParser.TryParse(value, out var gesture, out var error))
            {
                parsedHotkeys.Add((name, path, gesture));
                return;
            }

            issues.Add(new AppSettingsValidationIssue(path, error ?? "Hotkey is invalid."));
        }
    }
}
