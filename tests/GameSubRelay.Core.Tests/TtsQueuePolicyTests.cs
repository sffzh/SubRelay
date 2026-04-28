using System.Collections.Generic;
using Xunit;
using GameSubRelay.Core.Audio;
using GameSubRelay.Core.Captions;
using GameSubRelay.Core.Tts;
using GameSubRelay.Core.Translation;

namespace GameSubRelay.Core.Tests;

public class TtsQueuePolicyTests
{
    [Fact]
    public void Queue_drops_oldest_request_when_full()
    {
        var queue = new Queue<TtsRequest>();
        var options = TtsQueueOptions.Default with { MaxPendingRequests = 2 };
        var dropped = TtsQueuePolicy.EnqueueRequest(queue, new TtsRequest(AudioChannelId.Microphone, "first"), options);

        Assert.Empty(dropped);
        dropped = TtsQueuePolicy.EnqueueRequest(queue, new TtsRequest(AudioChannelId.Monitor, "second"), options);

        Assert.Empty(dropped);
        dropped = TtsQueuePolicy.EnqueueRequest(queue, new TtsRequest(AudioChannelId.Microphone, "third"), options);

        Assert.Single(dropped);
        Assert.Equal("first", dropped[0].Text);
        Assert.Equal(2, queue.Count);
    }

    [Fact]
    public void Queue_normalizes_invalid_max_pending_requests()
    {
        var queue = new Queue<TtsRequest>();
        var options = TtsQueueOptions.Default with { MaxPendingRequests = 0 };

        var dropped = TtsQueuePolicy.EnqueueRequest(queue, new TtsRequest(AudioChannelId.Microphone, "first"), options);

        Assert.Empty(dropped);
        Assert.Single(queue);
        Assert.Equal("first", queue.Peek().Text);
    }

    [Fact]
    public void Only_final_segments_can_be_enqueued()
    {
        var segment = new TranslationSegment(AudioChannelId.Monitor, 1, "en", "zh", "source", "中间", SegmentStability.Interim, System.TimeSpan.Zero, System.TimeSpan.Zero);

        var canQueue = TtsQueuePolicy.TryBuildTtsRequest(segment, _ => true, out var request);

        Assert.False(canQueue);
        Assert.Null(request);
    }

    [Fact]
    public void Disabled_channel_does_not_create_tts_request()
    {
        var segment = new TranslationSegment(AudioChannelId.Monitor, 1, "en", "zh", "source", "最终", SegmentStability.Final, System.TimeSpan.Zero, System.TimeSpan.Zero);

        var canQueue = TtsQueuePolicy.TryBuildTtsRequest(segment, channel => channel == AudioChannelId.Microphone, out var request);

        Assert.False(canQueue);
        Assert.Null(request);
    }

    [Fact]
    public void Final_segment_from_enabled_channel_creates_request()
    {
        var segment = new TranslationSegment(AudioChannelId.Monitor, 1, "en", "zh", "source", "最终", SegmentStability.Final, System.TimeSpan.Zero, System.TimeSpan.Zero);

        var canQueue = TtsQueuePolicy.TryBuildTtsRequest(segment, _ => true, out var request);

        Assert.True(canQueue);
        Assert.NotNull(request);
        Assert.Equal("最终", request.Text);
    }
}
