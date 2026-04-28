using System;
using GameSubRelay.Core.Audio;

namespace GameSubRelay.Core.Captions;

public sealed record CaptionLine(
    Guid Id,
    AudioChannelId ChannelId,
    string ChannelLabel,
    string SourceText,
    string TranslatedText,
    SegmentStability Stability,
    DateTimeOffset UpdatedAt);
