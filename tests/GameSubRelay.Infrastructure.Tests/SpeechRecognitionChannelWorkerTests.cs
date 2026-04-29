using System.Collections.Generic;
using System.Threading.Channels;
using FluentAssertions;
using GameSubRelay.Core.Audio;
using GameSubRelay.Core.Captions;
using GameSubRelay.Core.SpeechRecognition;
using GameSubRelay.Infrastructure.Runtime;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace GameSubRelay.Infrastructure.Tests;

public sealed class SpeechRecognitionChannelWorkerTests
{
    [Fact]
    public async Task Worker_sends_audio_frames_and_applies_recognition_segments_to_caption_store()
    {
        var source = new StubAudioFrameSource(AudioChannelId.Monitor);
        var session = new StubSpeechRecognitionSession();
        var provider = new StubSpeechRecognitionProvider(session);
        var store = new CaptionStore();
        var worker = new SpeechRecognitionChannelWorker(
            AudioChannelId.Monitor,
            source,
            provider,
            new SpeechRecognitionSessionOptions("en", "cn-north-1"),
            store,
            NullLogger<SpeechRecognitionChannelWorker>.Instance);

        await worker.StartAsync();
        await source.EmitAsync(new AudioFrame(AudioChannelId.Monitor, [1, 2, 3], DateTimeOffset.UtcNow, TimeSpan.FromMilliseconds(100)));
        await session.EmitAsync(new SpeechRecognitionSegment(
            AudioChannelId.Monitor,
            1,
            "en",
            "enemy on the left",
            SegmentStability.Final,
            TimeSpan.Zero,
            TimeSpan.FromMilliseconds(100)));

        await EventuallyAsync(() => store.Count == 1 && session.SentFrames.Count == 1);
        await worker.StopAsync();

        session.SentFrames.Should().ContainSingle();
        store.GetSnapshot().Should().ContainSingle(line =>
            line.SourceText == "enemy on the left" &&
            line.TranslatedText == string.Empty &&
            line.Stability == SegmentStability.Final);
    }

    private static async Task EventuallyAsync(Func<bool> condition)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(3);
        while (DateTimeOffset.UtcNow < deadline)
        {
            if (condition())
            {
                return;
            }

            await Task.Delay(25);
        }

        condition().Should().BeTrue();
    }

    private sealed class StubAudioFrameSource : IAudioFrameSource
    {
        private readonly Channel<AudioFrame> _frames = Channel.CreateUnbounded<AudioFrame>();

        public StubAudioFrameSource(AudioChannelId channelId)
        {
            ChannelId = channelId;
        }

        public AudioChannelId ChannelId { get; }

        public IAsyncEnumerable<AudioFrame> GetFramesAsync(CancellationToken cancellationToken = default)
            => _frames.Reader.ReadAllAsync(cancellationToken);

        public ValueTask StartAsync(CancellationToken cancellationToken = default) => ValueTask.CompletedTask;

        public ValueTask StopAsync()
        {
            _frames.Writer.TryComplete();
            return ValueTask.CompletedTask;
        }

        public ValueTask DisposeAsync() => StopAsync();

        public ValueTask EmitAsync(AudioFrame frame) => _frames.Writer.WriteAsync(frame);
    }

    private sealed class StubSpeechRecognitionProvider : ISpeechRecognitionProvider
    {
        private readonly StubSpeechRecognitionSession _session;

        public StubSpeechRecognitionProvider(StubSpeechRecognitionSession session)
        {
            _session = session;
        }

        public Task<ISpeechRecognitionSession> StartSessionAsync(
            AudioChannelId channelId,
            SpeechRecognitionSessionOptions options,
            CancellationToken cancellationToken = default)
            => Task.FromResult<ISpeechRecognitionSession>(_session);
    }

    private sealed class StubSpeechRecognitionSession : ISpeechRecognitionSession
    {
        private readonly Channel<SpeechRecognitionSegment> _segments = Channel.CreateUnbounded<SpeechRecognitionSegment>();

        public List<AudioFrame> SentFrames { get; } = [];

        public ValueTask SendAudioAsync(AudioFrame frame, CancellationToken cancellationToken)
        {
            SentFrames.Add(frame);
            return ValueTask.CompletedTask;
        }

        public ValueTask CompleteAsync(CancellationToken cancellationToken)
        {
            _segments.Writer.TryComplete();
            return ValueTask.CompletedTask;
        }

        public IAsyncEnumerable<SpeechRecognitionSegment> ReadSegmentsAsync(CancellationToken cancellationToken)
            => _segments.Reader.ReadAllAsync(cancellationToken);

        public ValueTask DisposeAsync() => CompleteAsync(CancellationToken.None);

        public ValueTask EmitAsync(SpeechRecognitionSegment segment) => _segments.Writer.WriteAsync(segment);
    }
}
