using System.Linq;
using Xunit;
using GameSubRelay.Core.Hotkeys;

namespace GameSubRelay.Core.Tests;

public class HotkeyParserTests
{
    [Fact]
    public void Parser_can_normalize_modifier_order()
    {
        var result = HotkeyParser.TryParse("Alt+Ctrl+S", out var gesture, out var error);

        Assert.True(result);
        Assert.Null(error);
        Assert.NotNull(gesture);
        Assert.Equal("Ctrl+Alt+S", gesture!.ToCanonicalString());
    }

    [Fact]
    public void Parser_preserves_canonical_compound_key_casing()
    {
        var result = HotkeyParser.TryParse("Ctrl+PgUp", out var gesture, out var error);

        Assert.True(result);
        Assert.Null(error);
        Assert.NotNull(gesture);
        Assert.Equal("Ctrl+PageUp", gesture!.ToCanonicalString());
    }

    [Fact]
    public void Duplicate_modifiers_are_rejected()
    {
        var result = HotkeyParser.TryParse("Ctrl+Ctrl+S", out var gesture, out var error);

        Assert.False(result);
        Assert.Null(gesture);
        Assert.Contains("duplicated", error);
    }

    [Fact]
    public void Invalid_key_name_is_rejected()
    {
        var result = HotkeyParser.TryParse("Ctrl+ThisDoesNotExist", out var gesture, out var error);

        Assert.False(result);
        Assert.Null(gesture);
        Assert.Contains("Unsupported", error);
    }

    [Fact]
    public void Duplicate_hotkeys_are_detected()
    {
        var first = HotkeyParser.ParseOrThrow("Ctrl+Alt+S");
        var second = HotkeyParser.ParseOrThrow("Alt+Ctrl+S");
        var third = HotkeyParser.ParseOrThrow("Ctrl+Shift+F1");

        var binding = new HotkeyBindings(first.Gesture!, second.Gesture!, third.Gesture!);

        Assert.Single(binding.Validate());
        Assert.Contains("duplicated", binding.Validate().Single());
    }
}
