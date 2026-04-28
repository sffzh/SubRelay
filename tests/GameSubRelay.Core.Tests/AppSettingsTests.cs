using System.Linq;
using GameSubRelay.Core.Configuration;
using Xunit;

namespace GameSubRelay.Core.Tests;

public sealed class AppSettingsTests
{
    [Fact]
    public void Normalize_clamps_overlay_values_to_supported_ranges()
    {
        var settings = AppSettings.Default with
        {
            Overlay = AppSettings.Default.Overlay with
            {
                MaxLines = 99,
                Opacity = 0.01
            }
        };

        var normalized = settings.Normalize();

        Assert.Equal(12, normalized.Overlay.MaxLines);
        Assert.Equal(0.2, normalized.Overlay.Opacity);
    }

    [Fact]
    public void Validate_reports_invalid_hotkey_text()
    {
        var settings = AppSettings.Default with
        {
            Hotkeys = AppSettings.Default.Hotkeys with
            {
                ToggleOverlay = "Ctrl+NotARealKey"
            }
        };

        var result = settings.Validate();

        Assert.False(result.IsValid);
        Assert.Contains(result.Issues, issue =>
            issue.Path == "hotkeys.toggleOverlay" &&
            issue.Message.Contains("Unsupported", System.StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Validate_reports_duplicate_hotkey_bindings()
    {
        var settings = AppSettings.Default with
        {
            Hotkeys = AppSettings.Default.Hotkeys with
            {
                ToggleOverlay = "Ctrl+Alt+S",
                ToggleEditMode = "Alt+Ctrl+S"
            }
        };

        var result = settings.Validate();

        Assert.False(result.IsValid);
        Assert.Contains(result.Issues, issue =>
            issue.Path == "hotkeys" &&
            issue.Message.Contains("duplicated", System.StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Validate_reports_tts_channel_flags_when_tts_is_disabled()
    {
        var settings = AppSettings.Default with
        {
            Tts = AppSettings.Default.Tts with
            {
                Enabled = false,
                UseForMicrophone = true
            }
        };

        var result = settings.Validate();

        Assert.False(result.IsValid);
        Assert.Contains(result.Issues, issue =>
            issue.Path == "tts.useForMicrophone" &&
            issue.Message.Contains("requires tts.enabled", System.StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Normalize_canonicalizes_valid_hotkeys()
    {
        var settings = AppSettings.Default with
        {
            Hotkeys = AppSettings.Default.Hotkeys with
            {
                ToggleOverlay = "Alt+Ctrl+S"
            }
        };

        var normalized = settings.Normalize();

        Assert.Equal("Ctrl+Alt+S", normalized.Hotkeys.ToggleOverlay);
    }
}
