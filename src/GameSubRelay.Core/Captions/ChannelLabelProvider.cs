using System.Collections.Generic;
using GameSubRelay.Core.Audio;

namespace GameSubRelay.Core.Captions;

public sealed class ChannelLabelProvider
{
    private readonly IReadOnlyDictionary<AudioChannelId, string> _labelByChannel;

    public static ChannelLabelProvider Default { get; } = new(new Dictionary<AudioChannelId, string>
    {
        [AudioChannelId.Microphone] = "Mic",
        [AudioChannelId.Monitor] = "Game"
    });

    public ChannelLabelProvider(IReadOnlyDictionary<AudioChannelId, string>? labelByChannel = null)
    {
        _labelByChannel = labelByChannel ?? Default._labelByChannel;
    }

    public string GetLabel(AudioChannelId channelId)
        => _labelByChannel.TryGetValue(channelId, out var label) ? label : channelId.ToString();
}
