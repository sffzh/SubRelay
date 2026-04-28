using FluentAssertions;
using GameSubRelay.Infrastructure.Hotkeys;
using Xunit;

namespace GameSubRelay.Infrastructure.Tests;

public class GlobalHotkeyMappingTests
{
    [Theory]
    [InlineData("Ctrl+Alt+S", 0x0002u | 0x0001u, 0x53u)]
    [InlineData("Shift+Win+F1", 0x0004u | 0x0008u, 0x70u)]
    [InlineData("Ctrl+PageUp", 0x0002u, 0x21u)]
    public void TryCreateWin32Hotkey_maps_supported_gestures(string gesture, uint expectedModifiers, uint expectedVirtualKey)
    {
        var success = GlobalHotkeyMapping.TryCreate(gesture, out var mapping, out var error);

        success.Should().BeTrue(error);
        mapping!.Modifiers.Should().Be(expectedModifiers);
        mapping.VirtualKey.Should().Be(expectedVirtualKey);
    }

    [Fact]
    public void TryCreateWin32Hotkey_rejects_invalid_gestures()
    {
        var success = GlobalHotkeyMapping.TryCreate("Ctrl+NoSuchKey", out var mapping, out var error);

        success.Should().BeFalse();
        mapping.Should().BeNull();
        error.Should().NotBeNullOrWhiteSpace();
    }
}
