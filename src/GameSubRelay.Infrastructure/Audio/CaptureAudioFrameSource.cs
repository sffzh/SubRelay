using System.Threading.Channels;
using GameSubRelay.Core.Audio;

namespace GameSubRelay.Infrastructure.Audio;

public sealed class CaptureAudioFrameSource : IAudioFrameSource
{
    private readonly IAudioCaptureService _captureService;
    private readonly Channel<AudioFrame> _frames;
    private bool _disposed;

    public CaptureAudioFrameSource(AudioChannelId channelId, IAudioCaptureService captureService)
    {
        ChannelId = channelId;
        _captureService = captureService ?? throw new ArgumentNullException(nameof(captureService));
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
        await _captureService.StartAsync(cancellationToken);
    }

    public async ValueTask StopAsync()
    {
        if (_disposed)
        {
            return;
        }

        await _captureService.StopAsync();
        _frames.Writer.TryComplete();
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

        _frames.Writer.TryWrite(new AudioFrame(
            ChannelId,
            frame.Pcm16Mono16Khz,
            frame.CapturedAt,
            frame.Duration));
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(CaptureAudioFrameSource));
        }
    }
}
