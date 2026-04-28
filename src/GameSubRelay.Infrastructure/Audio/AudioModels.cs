using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace GameSubRelay.Infrastructure.Audio;

public sealed record AudioDeviceInfo(
    string DeviceId,
    string Name,
    string? Manufacturer,
    DataFlow DataFlow,
    bool IsEnabled,
    int ChannelCount,
    int SampleRate,
    int BitsPerSample,
    bool IsDefault);

public sealed record CapturedAudioFrame(
    string DeviceId,
    byte[] Pcm16Mono16Khz,
    DateTimeOffset CapturedAt,
    TimeSpan Duration);

public interface IAudioCaptureService : IAsyncDisposable
{
    event EventHandler<CapturedAudioFrame>? FrameCaptured;
    Task StartAsync(CancellationToken cancellationToken = default);
    Task StopAsync();
    bool IsRunning { get; }
}

public interface IAudioOutputPlayer : IAsyncDisposable
{
    Task StartAsync(string? renderDeviceId = null, CancellationToken cancellationToken = default);
    Task PlayAsync(byte[] pcm16Mono16Khz, CancellationToken cancellationToken = default);
    Task StopAsync();
    bool IsRunning { get; }
}

public static class AudioMappingHelpers
{
    public const int TargetSampleRate = 16000;
    public const int TargetChannels = 1;
    public const int TargetBitsPerSample = 16;
    public static readonly WaveFormat TargetWaveFormat = new WaveFormat(TargetSampleRate, TargetBitsPerSample, TargetChannels);

    public static AudioDeviceInfo ToDeviceInfo(
        MMDevice device,
        DataFlow dataFlow,
        bool isDefault)
    {
        return ToDeviceInfo(
            device.ID,
            string.IsNullOrWhiteSpace(device.FriendlyName) ? device.DeviceFriendlyName : device.FriendlyName,
            device.DeviceFriendlyName,
            dataFlow,
            device.State == DeviceState.Active,
            device.AudioClient?.MixFormat?.Channels ?? 2,
            device.AudioClient?.MixFormat?.SampleRate ?? 44100,
            device.AudioClient?.MixFormat?.BitsPerSample ?? 16,
            isDefault);
    }

    public static AudioDeviceInfo ToDeviceInfo(
        string deviceId,
        string name,
        string? manufacturer,
        DataFlow dataFlow,
        bool isEnabled,
        int channels,
        int sampleRate,
        int bitsPerSample,
        bool isDefault)
    {
        var normalizedName = string.IsNullOrWhiteSpace(name) ? $"Device {deviceId}" : name.Trim();

        return new AudioDeviceInfo(
            deviceId,
            normalizedName,
            string.IsNullOrWhiteSpace(manufacturer) ? null : manufacturer.Trim(),
            dataFlow,
            isEnabled,
            Math.Max(1, channels),
            Math.Max(1, sampleRate),
            Math.Max(1, bitsPerSample),
            isDefault);
    }

    public static TimeSpan GetDurationFromBytes(int byteCount, int sampleRate, int channels, int bitsPerSample)
    {
        if (sampleRate <= 0 || channels <= 0 || bitsPerSample <= 0)
        {
            return TimeSpan.Zero;
        }

        var bytesPerSample = bitsPerSample / 8;
        var frameBytes = Math.Max(1, bytesPerSample * channels);
        var frameCount = byteCount / frameBytes;
        return TimeSpan.FromSeconds((double)frameCount / sampleRate);
    }

    public static TimeSpan GetTargetDurationFromOutputBytes(int byteCount)
    {
        var frameBytes = TargetBitsPerSample / 8 * TargetChannels;
        var frameCount = byteCount / frameBytes;
        return TimeSpan.FromSeconds((double)frameCount / TargetSampleRate);
    }
}
