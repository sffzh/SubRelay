using FluentAssertions;
using NAudio.Wave;
using GameSubRelay.Infrastructure.Audio;
using Xunit;

namespace GameSubRelay.Infrastructure.Tests;

public class Pcm16Mono16KhzConverterTests
{
    [Fact]
    public void Convert_keeps_pcm16_mono_16khz_data_as_is()
    {
        var converter = new Pcm16Mono16KhzConverter();
        var samples = new short[] { 0, 1024, -1024, short.MinValue, short.MaxValue };
        var bytes = new byte[samples.Length * 2];
        for (var i = 0; i < samples.Length; i++)
        {
            var b = BitConverter.GetBytes(samples[i]);
            bytes[i * 2] = b[0];
            bytes[i * 2 + 1] = b[1];
        }

        var sourceFormat = new WaveFormat(16000, 16, 1);
        var converted = converter.Convert(bytes, sourceFormat);

        converted.Should().Equal(bytes);
    }

    [Fact]
    public void Convert_downmixes_stereo_to_mono()
    {
        var converter = new Pcm16Mono16KhzConverter();
        var sourceFormat = new WaveFormat(16000, 16, 2);
        var stereoBytes = new byte[]
        {
            0x00, 0x00, 0x00, 0x02, // 0, 512
            0x00, 0x80, 0x00, 0xFE, // -32768, -512
            0x00, 0x01, 0xFF, 0x7F  // 256, 32767
        };

        var converted = converter.Convert(stereoBytes, sourceFormat);

        var expectedSamples = new short[] { 256, -16639, 16511 };
        var expected = new byte[expectedSamples.Length * 2];
        for (var i = 0; i < expectedSamples.Length; i++)
        {
            var b = BitConverter.GetBytes(expectedSamples[i]);
            expected[i * 2] = b[0];
            expected[i * 2 + 1] = b[1];
        }

        converted.Should().Equal(expected);
    }

    [Fact]
    public void Convert_resamples_8000hz_to_16000hz()
    {
        var converter = new Pcm16Mono16KhzConverter();
        var sourceFormat = new WaveFormat(8000, 16, 1);
        var samples = new short[] { 0, short.MaxValue, 0 };
        var bytes = new byte[samples.Length * 2];

        for (var i = 0; i < samples.Length; i++)
        {
            var b = BitConverter.GetBytes(samples[i]);
            bytes[i * 2] = b[0];
            bytes[i * 2 + 1] = b[1];
        }

        var converted = converter.Convert(bytes, sourceFormat);

        converted.Length.Should().Be(12);
    }

    [Fact]
    public void Convert_handles_float_source_format()
    {
        var converter = new Pcm16Mono16KhzConverter();
        var sourceFormat = WaveFormat.CreateIeeeFloatWaveFormat(16000, 1);
        var sourceSamples = new float[] { -1f, 0f, 1f };
        var sourceBytes = new byte[sourceSamples.Length * 4];

        for (var i = 0; i < sourceSamples.Length; i++)
        {
            var b = BitConverter.GetBytes(sourceSamples[i]);
            Buffer.BlockCopy(b, 0, sourceBytes, i * 4, 4);
        }

        var converted = converter.Convert(sourceBytes, sourceFormat);

        converted.Should().HaveCount(6);
        BitConverter.ToInt16(converted, 0).Should().Be(-32767);
        BitConverter.ToInt16(converted, 2).Should().Be(0);
        BitConverter.ToInt16(converted, 4).Should().Be(32767);
    }

    [Fact]
    public void Convert_handles_extensible_float_source_format()
    {
        var converter = new Pcm16Mono16KhzConverter();
        var sourceFormat = new WaveFormatExtensible(16000, 32, 1);
        var sourceSamples = new float[] { -1f, 0f, 1f };
        var sourceBytes = new byte[sourceSamples.Length * 4];

        for (var i = 0; i < sourceSamples.Length; i++)
        {
            var b = BitConverter.GetBytes(sourceSamples[i]);
            Buffer.BlockCopy(b, 0, sourceBytes, i * 4, 4);
        }

        var converted = converter.Convert(sourceBytes, sourceFormat);

        converted.Should().HaveCount(6);
        BitConverter.ToInt16(converted, 0).Should().Be(-32767);
        BitConverter.ToInt16(converted, 2).Should().Be(0);
        BitConverter.ToInt16(converted, 4).Should().Be(32767);
    }
}
