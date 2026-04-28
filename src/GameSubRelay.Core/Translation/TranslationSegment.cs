using System;
using GameSubRelay.Core.Audio;
using GameSubRelay.Core.Captions;

namespace GameSubRelay.Core.Translation;

public sealed record TranslationSegment(
    AudioChannelId ChannelId,
    long ProviderSequence,
    string SourceLanguage,
    string TargetLanguage,
    string SourceText,
    string TranslatedText,
    SegmentStability Stability,
    TimeSpan BeginTime,
    TimeSpan EndTime);
