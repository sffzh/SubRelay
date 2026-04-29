using System.Threading.Channels;
using GameSubRelay.Core.Audio;
using Microsoft.Extensions.Logging;

namespace GameSubRelay.Infrastructure.Audio;

public sealed class CaptureAudioFrameSource : IAudioFrameSource
{
    private readonly IAudioCaptureService _captureService;
    private readonly ILogger<CaptureAudioFrameSource>? _logger;
    private readonly Channel<AudioFrame> _frames;
    private long _framesCaptured;
    private long _bytesCaptured;
    private bool _disposed;

    public CaptureAudioFrameSource(
        AudioChannelId channelId,
        IAudioCaptureService captureService,
        ILogger<CaptureAudioFrameSource>? logger = null)
    {
        ChannelId = channelId;
        _captureService = captureService ?? throw new ArgumentNullException(nameof(captureService));
        _logger = logger;
        _captureService.FrameCaptured += OnFrameCaptured;
        _frames = Channel.CreateUnbounded<AudioFrame>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false
        });
    }

    public AudioChannelId ChannelId { get; }

    public IAsyncEnumerable<AudioFrame> GetFramesAsync(CancellationToken cancellationToken = default)
    {
        return _frames.Reader.ReadAllAsync(cancellationToken);
    }

    public async ValueTask StartAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        _logger?.LogInformation("Starting audio frame source for {ChannelId}.", ChannelId);
        await _captureService.StartAsync(cancellationToken);
        _logger?.LogInformation("Audio frame source started for {ChannelId}.", ChannelId);
    }

    public async ValueTask StopAsync()
    {
        if (_disposed)
        {
            return;
        }

        await _captureService.StopAsync();
        _frames.Writer.TryComplete();
        _logger?.LogInformation(
            "Audio frame source stopped for {ChannelId}: frames={Frames}, bytes={Bytes}.",
            ChannelId,
            Interlocked.Read(ref _framesCaptured),
            Interlocked.Read(ref _bytesCaptured));
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _captureService.FrameCaptured -= OnFrameCaptured;
        await _captureService.DisposeAsync();
        _frames.Writer.TryComplete();
    }

    private void OnFrameCaptured(object? sender, CapturedAudioFrame frame)
    {
        if (_disposed)
        {
            return;
        }

        var audioFrame = new AudioFrame(
            ChannelId,
            frame.Pcm16Mono16Khz,
            frame.CapturedAt,
            frame.Duration);

        if (!_frames.Writer.TryWrite(audioFrame))
        {
            _logger?.LogWarning(
                "Dropped captured audio frame for {ChannelId}: bytes={Bytes}, durationMs={DurationMs}.",
                ChannelId,
                frame.Pcm16Mono16Khz.Length,
                frame.Duration.TotalMilliseconds);
            return;
        }

        var frames = Interlocked.Increment(ref _framesCaptured);
        var bytes = Interlocked.Add(ref _bytesCaptured, frame.Pcm16Mono16Khz.Length);
        if (frames == 1 || frames % 50 == 0)
        {
            _logger?.LogInformation(
                "Captured audio for {ChannelId}: frames={Frames}, bytes={Bytes}, lastFrameBytes={LastFrameBytes}, lastDurationMs={DurationMs}.",
                ChannelId,
                frames,
                bytes,
                frame.Pcm16Mono16Khz.Length,
                frame.Duration.TotalMilliseconds);
        }
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(CaptureAudioFrameSource));
        }
    }
}
