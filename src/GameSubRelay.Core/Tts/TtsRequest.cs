using GameSubRelay.Core.Audio;
using GameSubRelay.Core.Captions;

namespace GameSubRelay.Core.Tts;

public sealed record TtsRequest(
    AudioChannelId ChannelId,
    string Text,
    string? SourceLanguage = null,
    string? TargetLanguage = null,
    SegmentStability Stability = SegmentStability.Final)
{
    public static TtsRequest FromTranslationSegment(string text, AudioChannelId channelId, string sourceLanguage, string targetLanguage)
        => new(channelId, text, sourceLanguage, targetLanguage, SegmentStability.Final);
}
