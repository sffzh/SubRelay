using GameSubRelay.Core.Audio;
using GameSubRelay.Infrastructure.Audio;
using Xunit;

namespace GameSubRelay.Infrastructure.Tests;

public sealed class CaptureAudioFrameSourceTests
{
    [Fact]
    public async Task CaptureAudioFrameSource_emits_core_audio_frames_from_capture_service()
    {
        var capture = new StubAudioCaptureService();
        await using var source = new CaptureAudioFrameSource(AudioChannelId.Microphone, capture);

        await source.StartAsync();
        await capture.EmitAsync(new CapturedAudioFrame(
            "device-id",
            [1, 2, 3],
            DateTimeOffset.UtcNow,
            TimeSpan.FromMilliseconds(50)));

        var frames = source.GetFramesAsync(CancellationToken.None);
        await using var enumerator = frames.GetAsyncEnumerator();

        Assert.True(await enumerator.MoveNextAsync());
        Assert.Equal(AudioChannelId.Microphone, enumerator.Current.ChannelId);
        Assert.Equal([1, 2, 3], enumerator.Current.Pcm16Mono16Khz);
        Assert.Equal(TimeSpan.FromMilliseconds(50), enumerator.Current.Duration);
    }

    [Fact]
    public async Task CaptureAudioFrameSource_start_and_stop_control_underlying_capture()
    {
        var capture = new StubAudioCaptureService();
        await using var source = new CaptureAudioFrameSource(AudioChannelId.Monitor, capture);

        await source.StartAsync();
        await source.StopAsync();

        Assert.Equal(1, capture.StartCount);
        Assert.Equal(1, capture.StopCount);
    }

    private sealed class StubAudioCaptureService : IAudioCaptureService
    {
        public event EventHandler<CapturedAudioFrame>? FrameCaptured;

        public int StartCount { get; private set; }

        public int StopCount { get; private set; }

        public bool IsRunning { get; private set; }

        public Task StartAsync(CancellationToken cancellationToken = default)
        {
            StartCount++;
            IsRunning = true;
            return Task.CompletedTask;
        }

        public Task StopAsync()
        {
            StopCount++;
            IsRunning = false;
            return Task.CompletedTask;
        }

        public ValueTask DisposeAsync()
        {
            return ValueTask.CompletedTask;
        }

        public ValueTask EmitAsync(CapturedAudioFrame frame)
        {
            FrameCaptured?.Invoke(this, frame);
            return ValueTask.CompletedTask;
        }
    }
}
