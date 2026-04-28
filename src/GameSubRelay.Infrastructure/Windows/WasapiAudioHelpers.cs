using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace GameSubRelay.Infrastructure.Windows;

public static class WasapiAudioHelpers
{
    public static BufferedWaveProvider CreateMono16KhzPlaybackBuffer()
    {
        return new BufferedWaveProvider(Audio.AudioMappingHelpers.TargetWaveFormat)
        {
            BufferLength = Audio.AudioMappingHelpers.TargetWaveFormat.AverageBytesPerSecond * 2,
            BufferDuration = TimeSpan.FromSeconds(2),
            DiscardOnBufferOverflow = true
        };
    }

    public static string GetSafeDeviceId(MMDevice? device)
    {
        return device?.ID ?? string.Empty;
    }

    public static bool IsOutputFormatSupported(MMDevice device)
    {
        var mix = device.AudioClient?.MixFormat;
        return mix is not null && mix.Channels >= 1 && mix.SampleRate > 0;
    }
}
