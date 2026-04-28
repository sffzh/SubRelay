using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace GameSubRelay.Infrastructure.Audio;

public sealed class LoopbackCaptureService : WasapiCaptureServiceBase
{
    public LoopbackCaptureService(
        INaudioDeviceService deviceService,
        string? loopbackDeviceId = null,
        Pcm16Mono16KhzConverter? converter = null)
        : base(loopbackDeviceId, deviceService, converter ?? new Pcm16Mono16KhzConverter(), AudioCaptureMode.Loopback)
    {
    }

    protected override WasapiCapture CreateCapture(MMDevice device)
    {
        return new WasapiLoopbackCapture(device);
    }
}
