using System;
using System.Threading;
using System.Threading.Tasks;
using GameSubRelay.Core.Audio;
using GameSubRelay.Core.Captions;
using GameSubRelay.Core.Translation;
using Microsoft.Extensions.Logging;

namespace GameSubRelay.Infrastructure.Runtime;

public sealed class TranslationChannelWorker : ITranslationChannelWorker, IAsyncDisposable
{
    private readonly IAudioFrameSource? _frameSource;
    private readonly ISpeechTranslationProvider? _translationProvider;
    private readonly SpeechTranslationSessionOptions? _sessionOptions;
    private readonly CaptionStore? _captionStore;
    private readonly ILogger _logger;
    private readonly object _sync = new();

    private CancellationTokenSource? _runCts;
    private ISpeechTranslationSession? _session;
    private Task? _sendLoop;
    private Task? _readLoop;
    private bool _isRunning;

    public AudioChannelId ChannelId { get; }

    public event EventHandler? StateChanged;

    public TranslationChannelWorker(NoopChannelExecutionStrategy strategy, ILogger<TranslationChannelWorker> logger)
    {
        strategy = strategy ?? throw new ArgumentNullException(nameof(strategy));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        ChannelId = strategy.ChannelName switch
        {
            "Monitor" => AudioChannelId.Monitor,
            _ => AudioChannelId.Microphone
        };
    }

    public TranslationChannelWorker(
        AudioChannelId channelId,
        IAudioFrameSource frameSource,
        ISpeechTranslationProvider translationProvider,
        SpeechTranslationSessionOptions sessionOptions,
        CaptionStore captionStore,
        ILogger<TranslationChannelWorker> logger)
    {
        ChannelId = channelId;
        _frameSource = frameSource ?? throw new ArgumentNullException(nameof(frameSource));
        _translationProvider = translationProvider ?? throw new ArgumentNullException(nameof(translationProvider));
        _sessionOptions = sessionOptions ?? throw new ArgumentNullException(nameof(sessionOptions));
        _captionStore = captionStore ?? throw new ArgumentNullException(nameof(captionStore));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        lock (_sync)
        {
            if (_isRunning)
            {
                return;
            }

            _isRunning = true;
            _runCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        }

        _logger.LogInformation(
            "Start channel {ChannelId}: mode={Mode}, language={SourceLanguage}->{TargetLanguage}.",
            ChannelId,
            _sessionOptions?.Mode ?? "<none>",
            _sessionOptions?.SourceLanguage ?? "<none>",
            _sessionOptions?.TargetLanguage ?? "<none>");
        StateChanged?.Invoke(this, EventArgs.Empty);

        if (_frameSource is null || _translationProvider is null || _sessionOptions is null || _captionStore is null)
        {
            return;
        }

        try
        {
            var runToken = _runCts!.Token;
            await _frameSource.StartAsync(runToken);
            _logger.LogInformation("Audio frame source started for channel {ChannelId}.", ChannelId);
            _session = await _translationProvider.StartSessionAsync(ChannelId, _sessionOptions, runToken);
            _logger.LogInformation("Translation session started for channel {ChannelId}.", ChannelId);

            _sendLoop = Task.Run(() => SendLoopAsync(runToken), CancellationToken.None);
            _readLoop = Task.Run(() => ReadLoopAsync(runToken), CancellationToken.None);
            _logger.LogInformation("Channel {ChannelId} send/read loops started.", ChannelId);
        }
        catch (Exception ex)
        {
            await StopAsync(CancellationToken.None);
            _logger.LogError(ex, "Failed while starting channel {ChannelId}.", ChannelId);
            throw;
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        CancellationTokenSource? runCts;
        ISpeechTranslationSession? session;
        IAudioFrameSource? frameSource;
        Task? sendLoop;
        Task? readLoop;

        lock (_sync)
        {
            if (!_isRunning)
            {
                return;
            }

            _isRunning = false;
            runCts = _runCts;
            session = _session;
            frameSource = _frameSource;
            sendLoop = _sendLoop;
            readLoop = _readLoop;
            _runCts = null;
            _session = null;
            _sendLoop = null;
            _readLoop = null;
        }

        _logger.LogInformation("Stop channel {ChannelId}.", ChannelId);
        runCts?.Cancel();

        if (frameSource is not null)
        {
            await frameSource.StopAsync();
        }

        if (session is not null)
        {
            _logger.LogInformation("Completing translation session for channel {ChannelId}.", ChannelId);
            await session.CompleteAsync(cancellationToken);
            await session.DisposeAsync();
        }

        await AwaitLoopAsync(sendLoop);
        await AwaitLoopAsync(readLoop);
        runCts?.Dispose();
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    private async Task SendLoopAsync(CancellationToken cancellationToken)
    {
        if (_frameSource is null || _session is null)
        {
            return;
        }

        long framesSent = 0;
        long bytesSent = 0;
        try
        {
            await foreach (var frame in _frameSource.GetFramesAsync(cancellationToken))
            {
                await _session.SendAudioAsync(frame, cancellationToken);
                framesSent++;
                bytesSent += frame.Pcm16Mono16Khz.Length;
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Audio send loop failed for channel {ChannelId}.", ChannelId);
        }
        finally
        {
            _logger.LogInformation(
                "Audio send loop ended for channel {ChannelId}: frames={Frames}, bytes={Bytes}.",
                ChannelId,
                framesSent,
                bytesSent);
        }
    }

    private async Task ReadLoopAsync(CancellationToken cancellationToken)
    {
        if (_captionStore is null || _session is null)
        {
            return;
        }

        long segmentsRead = 0;
        try
        {
            await foreach (var segment in _session.ReadSegmentsAsync(cancellationToken))
            {
                _captionStore.ApplySegment(segment);
                segmentsRead++;
                _logger.LogDebug(
                    "Applied caption segment for channel {ChannelId}: sequence={Sequence}, stability={Stability}, sourceChars={SourceChars}, translatedChars={TranslatedChars}.",
                    ChannelId,
                    segment.ProviderSequence,
                    segment.Stability,
                    segment.SourceText.Length,
                    segment.TranslatedText.Length);
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Caption read loop failed for channel {ChannelId}.", ChannelId);
        }
        finally
        {
            _logger.LogInformation(
                "Caption read loop ended for channel {ChannelId}: segments={Segments}.",
                ChannelId,
                segmentsRead);
        }
    }

    private static async Task AwaitLoopAsync(Task? task)
    {
        if (task is null)
        {
            return;
        }

        try
        {
            await task;
        }
        catch (OperationCanceledException)
        {
        }
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync(CancellationToken.None);
        if (_frameSource is not null)
        {
            await _frameSource.DisposeAsync();
        }
    }
}
