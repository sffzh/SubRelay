using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace GameSubRelay.Infrastructure.Audio;

public sealed class MicrophoneCaptureService : WasapiCaptureServiceBase
{
    public MicrophoneCaptureService(
        INaudioDeviceService deviceService,
        string? deviceId = null,
        Pcm16Mono16KhzConverter? converter = null)
        : base(deviceId, deviceService, converter ?? new Pcm16Mono16KhzConverter(), AudioCaptureMode.Microphone)
    {
    }

    protected override WasapiCapture CreateCapture(MMDevice device)
    {
        return new WasapiCapture(device);
    }
}
