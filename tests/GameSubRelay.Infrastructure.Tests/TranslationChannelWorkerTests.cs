using System.Collections.Generic;
using System.Threading.Channels;
using FluentAssertions;
using GameSubRelay.Core.Audio;
using GameSubRelay.Core.Captions;
using GameSubRelay.Core.Translation;
using GameSubRelay.Infrastructure.Runtime;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace GameSubRelay.Infrastructure.Tests;

public class TranslationChannelWorkerTests
{
    [Fact]
    public async Task Worker_sends_audio_frames_and_applies_segments_to_caption_store()
    {
        var source = new StubAudioFrameSource(AudioChannelId.Microphone);
        var session = new StubSpeechTranslationSession();
        var provider = new StubSpeechTranslationProvider(session);
        var store = new CaptionStore();
        var worker = new TranslationChannelWorker(
            AudioChannelId.Microphone,
            source,
            provider,
            new SpeechTranslationSessionOptions("en", "zh", "cn-north-1"),
            store,
            NullLogger<TranslationChannelWorker>.Instance);

        await worker.StartAsync();
        await source.EmitAsync(new AudioFrame(AudioChannelId.Microphone, [1, 2, 3], DateTimeOffset.UtcNow, TimeSpan.FromMilliseconds(100)));
        await session.EmitAsync(new TranslationSegment(
            AudioChannelId.Microphone,
            1,
            "en",
            "zh",
            "hello",
            "你好",
            SegmentStability.Final,
            TimeSpan.Zero,
            TimeSpan.FromMilliseconds(100)));

        await EventuallyAsync(() => store.Count == 1 && session.SentFrames.Count == 1);
        await worker.StopAsync();

        session.SentFrames.Should().ContainSingle();
        session.ReadCancellationWasRequestedWhenCompleted.Should().BeFalse();
        store.GetSnapshot().Should().ContainSingle(line =>
            line.SourceText == "hello" &&
            line.TranslatedText == "你好" &&
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

    private sealed class StubSpeechTranslationProvider : ISpeechTranslationProvider
    {
        private readonly StubSpeechTranslationSession _session;

        public StubSpeechTranslationProvider(StubSpeechTranslationSession session)
        {
            _session = session;
        }

        public Task<ISpeechTranslationSession> StartSessionAsync(
            AudioChannelId channelId,
            SpeechTranslationSessionOptions options,
            CancellationToken cancellationToken = default)
            => Task.FromResult<ISpeechTranslationSession>(_session);
    }

    private sealed class StubSpeechTranslationSession : ISpeechTranslationSession
    {
        private readonly Channel<TranslationSegment> _segments = Channel.CreateUnbounded<TranslationSegment>();
        private CancellationToken _readCancellationToken;

        public List<AudioFrame> SentFrames { get; } = [];

        public bool? ReadCancellationWasRequestedWhenCompleted { get; private set; }

        public ValueTask SendAudioAsync(AudioFrame frame, CancellationToken cancellationToken)
        {
            SentFrames.Add(frame);
            return ValueTask.CompletedTask;
        }

        public ValueTask CompleteAsync(CancellationToken cancellationToken)
        {
            ReadCancellationWasRequestedWhenCompleted ??= _readCancellationToken.IsCancellationRequested;
            _segments.Writer.TryComplete();
            return ValueTask.CompletedTask;
        }

        public IAsyncEnumerable<TranslationSegment> ReadSegmentsAsync(CancellationToken cancellationToken)
        {
            _readCancellationToken = cancellationToken;
            return _segments.Reader.ReadAllAsync(cancellationToken);
        }

        public ValueTask DisposeAsync() => CompleteAsync(CancellationToken.None);

        public ValueTask EmitAsync(TranslationSegment segment) => _segments.Writer.WriteAsync(segment);
    }
}
