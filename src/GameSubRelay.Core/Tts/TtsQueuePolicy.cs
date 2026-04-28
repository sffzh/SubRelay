using System;
using System.Collections.Generic;
using GameSubRelay.Core.Captions;
using GameSubRelay.Core.Translation;
using GameSubRelay.Core.Audio;

namespace GameSubRelay.Core.Tts;

public sealed record TtsQueueOptions(int MaxPendingRequests)
{
    public const int DefaultMaxPendingRequests = 3;

    public static TtsQueueOptions Default { get; } = new(DefaultMaxPendingRequests);

    public TtsQueueOptions Normalize()
    {
        return this with
        {
            MaxPendingRequests = Math.Max(1, MaxPendingRequests)
        };
    }
}

public static class TtsQueuePolicy
{
    public static bool TryBuildTtsRequest(
        TranslationSegment segment,
        Func<AudioChannelId, bool> shouldQueueChannel,
        out TtsRequest request)
    {
        request = null!;

        if (segment.Stability != SegmentStability.Final || !shouldQueueChannel(segment.ChannelId))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(segment.TranslatedText))
        {
            return false;
        }

        request = new TtsRequest(
            segment.ChannelId,
            segment.TranslatedText,
            segment.SourceLanguage,
            segment.TargetLanguage,
            segment.Stability);
        return true;
    }

    public static IReadOnlyList<TtsRequest> EnqueueRequest(
        Queue<TtsRequest> queue,
        TtsRequest request,
        TtsQueueOptions options)
    {
        options = options.Normalize();
        var dropped = new List<TtsRequest>();
        queue.Enqueue(request);

        while (queue.Count > options.MaxPendingRequests)
        {
            dropped.Add(queue.Dequeue());
        }

        return dropped;
    }
}
