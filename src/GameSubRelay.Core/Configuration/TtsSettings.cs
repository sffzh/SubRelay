namespace GameSubRelay.Core.Configuration;

public sealed record TtsSettings(
    bool Enabled,
    bool UseForMicrophone,
    bool UseForMonitor,
    int MaxQueueLength)
{
    public static TtsSettings Default => new(
        Enabled: false,
        UseForMicrophone: false,
        UseForMonitor: false,
        MaxQueueLength: 3);

    public TtsSettings Normalize() => this with
    {
        UseForMicrophone = Enabled && UseForMicrophone,
        UseForMonitor = Enabled && UseForMonitor,
        MaxQueueLength = Math.Max(1, MaxQueueLength)
    };
}
