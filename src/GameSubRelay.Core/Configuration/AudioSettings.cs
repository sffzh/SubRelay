namespace GameSubRelay.Core.Configuration;

public sealed record AudioSettings(
    string MicrophoneDeviceId,
    string MonitorRenderDeviceId,
    string TtsOutputDeviceId,
    bool MicrophoneEnabled,
    bool MonitorEnabled,
    bool TtsEnabled,
    bool TtsForMicrophone,
    bool TtsForMonitor)
{
    public static AudioSettings Default => new(
        string.Empty,
        string.Empty,
        string.Empty,
        MicrophoneEnabled: true,
        MonitorEnabled: true,
        TtsEnabled: false,
        TtsForMicrophone: false,
        TtsForMonitor: false);

    public AudioSettings Normalize(TtsSettings tts) => this with
    {
        MicrophoneDeviceId = MicrophoneDeviceId?.Trim() ?? string.Empty,
        MonitorRenderDeviceId = MonitorRenderDeviceId?.Trim() ?? string.Empty,
        TtsOutputDeviceId = TtsOutputDeviceId?.Trim() ?? string.Empty,
        TtsEnabled = tts.Enabled,
        TtsForMicrophone = tts.UseForMicrophone,
        TtsForMonitor = tts.UseForMonitor
    };
}
