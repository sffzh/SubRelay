using NAudio.Wave;
using NAudio.Dmo;

namespace GameSubRelay.Infrastructure.Audio;

public sealed class Pcm16Mono16KhzConverter
{
    public int TargetSampleRate { get; } = 16_000;
    public int TargetChannels { get; } = 1;
    public int TargetBitsPerSample { get; } = 16;

    public WaveFormat TargetWaveFormat => AudioMappingHelpers.TargetWaveFormat;

    public byte[] Convert(ReadOnlySpan<byte> input, WaveFormat sourceFormat)
    {
        if (sourceFormat is null)
        {
            throw new ArgumentNullException(nameof(sourceFormat));
        }

        if (sourceFormat.Channels <= 0 || sourceFormat.SampleRate <= 0)
        {
            return Array.Empty<byte>();
        }

        if (sourceFormat.Encoding == WaveFormatEncoding.Pcm &&
            sourceFormat.SampleRate == TargetSampleRate &&
            sourceFormat.Channels == TargetChannels &&
            sourceFormat.BitsPerSample == TargetBitsPerSample)
        {
            return input.ToArray();
        }

        var sourceSamples = ConvertToMonoFloatSamples(input, sourceFormat);
        var resampled = ResampleLinear(sourceSamples, sourceFormat.SampleRate, TargetSampleRate);
        return ConvertFloatMonoToPcm16Bytes(resampled);
    }

    private static float[] ConvertToMonoFloatSamples(ReadOnlySpan<byte> input, WaveFormat sourceFormat)
    {
        var channels = sourceFormat.Channels;
        var blockAlign = sourceFormat.BlockAlign;
        if (blockAlign <= 0 || channels <= 0)
        {
            return Array.Empty<float>();
        }

        var sampleCount = input.Length / blockAlign;
        var mono = new float[sampleCount];

        for (var frame = 0; frame < sampleCount; frame++)
        {
            var frameOffset = frame * blockAlign;
            float sum = 0f;

            for (var channel = 0; channel < channels; channel++)
            {
                var sampleOffset = frameOffset + channel * sourceFormat.BitsPerSample / 8;
                sum += ReadSampleAsFloat(sourceFormat, input, sampleOffset);
            }

            mono[frame] = Math.Clamp(sum / channels, -1f, 1f);
        }

        return mono;
    }

    private static float ReadSampleAsFloat(WaveFormat sourceFormat, ReadOnlySpan<byte> buffer, int sampleOffset)
    {
        if (sourceFormat is WaveFormatExtensible extensible)
        {
            if (extensible.SubFormat == AudioMediaSubtypes.MEDIASUBTYPE_IEEE_FLOAT &&
                sourceFormat.BitsPerSample == 32)
            {
                return BitConverter.ToSingle(buffer.Slice(sampleOffset, 4));
            }

            if (extensible.SubFormat == AudioMediaSubtypes.MEDIASUBTYPE_PCM ||
                extensible.SubFormat == AudioMediaSubtypes.WMMEDIASUBTYPE_PCM)
            {
                return ReadPcmSampleAsFloat(sourceFormat.BitsPerSample, buffer, sampleOffset);
            }
        }

        return sourceFormat.Encoding switch
        {
            WaveFormatEncoding.Pcm => ReadPcmSampleAsFloat(sourceFormat.BitsPerSample, buffer, sampleOffset),
            WaveFormatEncoding.IeeeFloat when sourceFormat.BitsPerSample == 32 => BitConverter.ToSingle(buffer.Slice(sampleOffset, 4)),
            _ => 0f
        };
    }

    private static float ReadPcmSampleAsFloat(int bitsPerSample, ReadOnlySpan<byte> buffer, int sampleOffset)
    {
        return bitsPerSample switch
        {
            8 => (buffer[sampleOffset] - 128) / 128f,
            16 => BitConverter.ToInt16(buffer.Slice(sampleOffset, 2)) / 32768f,
            24 => ReadInt24AsFloat(buffer.Slice(sampleOffset, 3)),
            32 => BitConverter.ToInt32(buffer.Slice(sampleOffset, 4)) / 2147483648f,
            _ => 0f
        };
    }

    private static float ReadInt24AsFloat(ReadOnlySpan<byte> bytes)
    {
        int unsigned = bytes[0] | (bytes[1] << 8) | (bytes[2] << 16);
        int signed = (unsigned ^ 0x800000) - 0x800000;
        return signed / 8388608f;
    }

    private static float[] ResampleLinear(float[] sourceSamples, int sourceSampleRate, int targetSampleRate)
    {
        if (sourceSamples.Length == 0 || sourceSampleRate <= 0 || targetSampleRate <= 0)
        {
            return Array.Empty<float>();
        }

        if (sourceSampleRate == targetSampleRate)
        {
            var identical = new float[sourceSamples.Length];
            Array.Copy(sourceSamples, identical, sourceSamples.Length);
            return identical;
        }

        var targetLength = Math.Max(1, (int)Math.Round((double)sourceSamples.Length * targetSampleRate / sourceSampleRate));
        var output = new float[targetLength];

        if (sourceSamples.Length == 1)
        {
            for (var i = 0; i < output.Length; i++)
            {
                output[i] = sourceSamples[0];
            }

            return output;
        }

        for (var index = 0; index < output.Length; index++)
        {
            var position = (double)index * sourceSampleRate / targetSampleRate;
            var leftIndex = (int)Math.Floor(position);
            var rightIndex = Math.Min(leftIndex + 1, sourceSamples.Length - 1);
            var interpolation = (float)(position - leftIndex);
            var left = sourceSamples[leftIndex];
            var right = sourceSamples[rightIndex];
            output[index] = Math.Clamp(left + (right - left) * interpolation, -1f, 1f);
        }

        return output;
    }

    private static byte[] ConvertFloatMonoToPcm16Bytes(float[] source)
    {
        var bytes = new byte[source.Length * 2];
        for (var i = 0; i < source.Length; i++)
        {
            var clamped = (short)Math.Round(Math.Clamp(source[i], -1f, 1f) * short.MaxValue);
            var sampleBytes = BitConverter.GetBytes(clamped);
            bytes[i * 2] = sampleBytes[0];
            bytes[i * 2 + 1] = sampleBytes[1];
        }

        return bytes;
    }
}
