using FluentAssertions;
using GameSubRelay.Infrastructure.Audio;
using NAudio.CoreAudioApi;
using Xunit;

namespace GameSubRelay.Infrastructure.Tests;

public class AudioMappingHelpersTests
{
    [Fact]
    public void Duration_mapping_works_for_pcm_frames()
    {
        var duration = AudioMappingHelpers.GetDurationFromBytes(
            byteCount: 320,
            sampleRate: 16000,
            channels: 1,
            bitsPerSample: 16);

        duration.Should().Be(TimeSpan.FromMilliseconds(10));
    }

    [Fact]
    public void Target_duration_uses_16000hz_mono_16bit_rules()
    {
        var duration = AudioMappingHelpers.GetTargetDurationFromOutputBytes(320);
        duration.Should().Be(TimeSpan.FromMilliseconds(10));
    }

    [Fact]
    public void Device_info_mapping_is_deterministic()
    {
        var info = AudioMappingHelpers.ToDeviceInfo(
            deviceId: "device-1",
            name: "  USB Audio Device  ",
            manufacturer: "Mocked Manufacturer",
            dataFlow: DataFlow.Capture,
            isEnabled: true,
            channels: 2,
            sampleRate: 48000,
            bitsPerSample: 24,
            isDefault: false);

        info.DeviceId.Should().Be("device-1");
        info.Name.Should().Be("USB Audio Device");
        info.Manufacturer.Should().Be("Mocked Manufacturer");
        info.ChannelCount.Should().Be(2);
        info.SampleRate.Should().Be(48000);
        info.IsEnabled.Should().BeTrue();
        info.IsDefault.Should().BeFalse();
    }
}
