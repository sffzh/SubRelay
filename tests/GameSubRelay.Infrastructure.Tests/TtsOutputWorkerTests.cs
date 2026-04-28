using System.Linq;
using FluentAssertions;
using GameSubRelay.Core.Audio;
using GameSubRelay.Core.Captions;
using GameSubRelay.Core.Translation;
using GameSubRelay.Core.Tts;
using GameSubRelay.Infrastructure.Audio;
using GameSubRelay.Infrastructure.Tts;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace GameSubRelay.Infrastructure.Tests;

public class TtsOutputWorkerTests
{
    [Fact]
    public async Task Worker_synthesizes_and_plays_final_segments_for_enabled_channels()
    {
        var provider = new StubTtsProvider([9, 8, 7]);
        var player = new StubAudioOutputPlayer();
        var worker = new TtsOutputWorker(provider, player, channel => channel == AudioChannelId.Monitor, TtsQueueOptions.Default, NullLogger<TtsOutputWorker>.Instance);

        await worker.HandleSegmentAsync(new TranslationSegment(
            AudioChannelId.Monitor,
            1,
            "en",
            "zh",
            "source",
            "最终译文",
            SegmentStability.Final,
            TimeSpan.Zero,
            TimeSpan.FromMilliseconds(100)));

        provider.Requests.Should().ContainSingle(request => request.Text == "最终译文");
        player.Played.Should().ContainSingle(bytes => bytes.SequenceEqual(new byte[] { 9, 8, 7 }));
    }

    [Fact]
    public async Task Worker_ignores_interim_or_disabled_channel_segments()
    {
        var provider = new StubTtsProvider([1]);
        var player = new StubAudioOutputPlayer();
        var worker = new TtsOutputWorker(provider, player, channel => channel == AudioChannelId.Microphone, TtsQueueOptions.Default, NullLogger<TtsOutputWorker>.Instance);

        await worker.HandleSegmentAsync(new TranslationSegment(AudioChannelId.Monitor, 1, "en", "zh", "source", "译文", SegmentStability.Final, TimeSpan.Zero, TimeSpan.Zero));
        await worker.HandleSegmentAsync(new TranslationSegment(AudioChannelId.Microphone, 2, "en", "zh", "source", "临时", SegmentStability.Interim, TimeSpan.Zero, TimeSpan.Zero));

        provider.Requests.Should().BeEmpty();
        player.Played.Should().BeEmpty();
    }

    [Fact]
    public async Task Worker_reports_tts_failures_without_throwing()
    {
        var provider = new StubTtsProvider([1]) { ThrowOnSynthesize = true };
        var player = new StubAudioOutputPlayer();
        var failures = 0;
        var worker = new TtsOutputWorker(provider, player, _ => true, TtsQueueOptions.Default, NullLogger<TtsOutputWorker>.Instance);
        worker.SynthesisFailed += (_, _) => failures++;

        var act = () => worker.HandleSegmentAsync(new TranslationSegment(AudioChannelId.Microphone, 1, "en", "zh", "source", "译文", SegmentStability.Final, TimeSpan.Zero, TimeSpan.Zero));

        await act.Should().NotThrowAsync();
        failures.Should().Be(1);
        player.Played.Should().BeEmpty();
    }

    private sealed class StubTtsProvider : ITtsProvider
    {
        private readonly byte[] _audio;

        public StubTtsProvider(byte[] audio)
        {
            _audio = audio;
        }

        public bool ThrowOnSynthesize { get; set; }
        public List<TtsRequest> Requests { get; } = [];

        public Task<byte[]> SynthesizeAsync(TtsRequest request, CancellationToken cancellationToken = default)
        {
            Requests.Add(request);
            if (ThrowOnSynthesize)
            {
                throw new InvalidOperationException("tts failed");
            }

            return Task.FromResult(_audio);
        }
    }

    private sealed class StubAudioOutputPlayer : IAudioOutputPlayer
    {
        public bool IsRunning { get; private set; }
        public List<byte[]> Played { get; } = [];

        public Task StartAsync(string? renderDeviceId = null, CancellationToken cancellationToken = default)
        {
            IsRunning = true;
            return Task.CompletedTask;
        }

        public Task PlayAsync(byte[] pcm16Mono16Khz, CancellationToken cancellationToken = default)
        {
            Played.Add(pcm16Mono16Khz);
            return Task.CompletedTask;
        }

        public Task StopAsync()
        {
            IsRunning = false;
            return Task.CompletedTask;
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
