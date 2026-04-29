using System;
using GameSubRelay.Core.Audio;
using GameSubRelay.Core.Captions;

namespace GameSubRelay.Core.SpeechRecognition;

public sealed record SpeechRecognitionSegment(
    AudioChannelId ChannelId,
    long ProviderSequence,
    string Language,
    string Text,
    SegmentStability Stability,
    TimeSpan BeginTime,
    TimeSpan EndTime);
