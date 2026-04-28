using System;

namespace GameSubRelay.Core.Audio;

public sealed record AudioFrame(
    AudioChannelId ChannelId,
    byte[] Pcm16Mono16Khz,
    DateTimeOffset CapturedAt,
    TimeSpan Duration);

