using GameSubRelay.Core.Audio;
using GameSubRelay.Core.Translation;
using GameSubRelay.Core.Tts;
using GameSubRelay.Infrastructure.Audio;
using Microsoft.Extensions.Logging;

namespace GameSubRelay.Infrastructure.Tts;

public sealed class TtsOutputWorker
{
    private readonly ITtsProvider _ttsProvider;
    private readonly IAudioOutputPlayer _audioOutputPlayer;
    private readonly Func<AudioChannelId, bool> _shouldQueueChannel;
    private readonly TtsQueueOptions _queueOptions;
    private readonly ILogger<TtsOutputWorker> _logger;
    private readonly Queue<TtsRequest> _queue = new();
    private readonly SemaphoreSlim _drainLock = new(1, 1);

    public event EventHandler<TtsRequestFailedEventArgs>? SynthesisFailed;

    public TtsOutputWorker(
        ITtsProvider ttsProvider,
        IAudioOutputPlayer audioOutputPlayer,
        Func<AudioChannelId, bool> shouldQueueChannel,
        TtsQueueOptions queueOptions,
        ILogger<TtsOutputWorker> logger)
    {
        _ttsProvider = ttsProvider ?? throw new ArgumentNullException(nameof(ttsProvider));
        _audioOutputPlayer = audioOutputPlayer ?? throw new ArgumentNullException(nameof(audioOutputPlayer));
        _shouldQueueChannel = shouldQueueChannel ?? throw new ArgumentNullException(nameof(shouldQueueChannel));
        _queueOptions = queueOptions.Normalize();
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task HandleSegmentAsync(TranslationSegment segment, CancellationToken cancellationToken = default)
    {
        if (!TtsQueuePolicy.TryBuildTtsRequest(segment, _shouldQueueChannel, out var request))
        {
            return;
        }

        lock (_queue)
        {
            var dropped = TtsQueuePolicy.EnqueueRequest(_queue, request, _queueOptions);
            foreach (var droppedRequest in dropped)
            {
                _logger.LogInformation("Dropped stale TTS request for {ChannelId}.", droppedRequest.ChannelId);
            }
        }

        await DrainQueueAsync(cancellationToken);
    }

    private async Task DrainQueueAsync(CancellationToken cancellationToken)
    {
        await _drainLock.WaitAsync(cancellationToken);
        try
        {
            while (true)
            {
                TtsRequest? request;
                lock (_queue)
                {
                    request = _queue.Count > 0 ? _queue.Dequeue() : null;
                }

                if (request is null)
                {
                    return;
                }

                try
                {
                    var audio = await _ttsProvider.SynthesizeAsync(request, cancellationToken);
                    if (!_audioOutputPlayer.IsRunning)
                    {
                        await _audioOutputPlayer.StartAsync(cancellationToken: cancellationToken);
                    }

                    await _audioOutputPlayer.PlayAsync(audio, cancellationToken);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _logger.LogWarning(ex, "TTS failed for channel {ChannelId}.", request.ChannelId);
                    SynthesisFailed?.Invoke(this, new TtsRequestFailedEventArgs(request, ex));
                }
            }
        }
        finally
        {
            _drainLock.Release();
        }
    }
}

public sealed class TtsRequestFailedEventArgs : EventArgs
{
    public TtsRequestFailedEventArgs(TtsRequest request, Exception exception)
    {
        Request = request;
        Exception = exception;
    }

    public TtsRequest Request { get; }

    public Exception Exception { get; }
}
