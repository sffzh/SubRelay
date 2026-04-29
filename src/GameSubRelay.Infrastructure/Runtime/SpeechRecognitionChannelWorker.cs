using System;
using System.Threading;
using System.Threading.Tasks;
using GameSubRelay.Core.Audio;
using GameSubRelay.Core.Captions;
using GameSubRelay.Core.SpeechRecognition;
using Microsoft.Extensions.Logging;

namespace GameSubRelay.Infrastructure.Runtime;

public sealed class SpeechRecognitionChannelWorker : IAudioChannelWorker, IAsyncDisposable
{
    private readonly IAudioFrameSource _frameSource;
    private readonly ISpeechRecognitionProvider _recognitionProvider;
    private readonly SpeechRecognitionSessionOptions _sessionOptions;
    private readonly CaptionStore _captionStore;
    private readonly ILogger _logger;
    private readonly object _sync = new();

    private CancellationTokenSource? _runCts;
    private ISpeechRecognitionSession? _session;
    private Task? _sendLoop;
    private Task? _readLoop;
    private bool _isRunning;

    public SpeechRecognitionChannelWorker(
        AudioChannelId channelId,
        IAudioFrameSource frameSource,
        ISpeechRecognitionProvider recognitionProvider,
        SpeechRecognitionSessionOptions sessionOptions,
        CaptionStore captionStore,
        ILogger<SpeechRecognitionChannelWorker> logger)
    {
        ChannelId = channelId;
        _frameSource = frameSource ?? throw new ArgumentNullException(nameof(frameSource));
        _recognitionProvider = recognitionProvider ?? throw new ArgumentNullException(nameof(recognitionProvider));
        _sessionOptions = sessionOptions ?? throw new ArgumentNullException(nameof(sessionOptions));
        _captionStore = captionStore ?? throw new ArgumentNullException(nameof(captionStore));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public AudioChannelId ChannelId { get; }

    public event EventHandler? StateChanged;

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
            "Start speech recognition channel {ChannelId}: language={Language}, region={Region}.",
            ChannelId,
            _sessionOptions.Language,
            _sessionOptions.Region);
        StateChanged?.Invoke(this, EventArgs.Empty);

        try
        {
            var runToken = _runCts!.Token;
            await _frameSource.StartAsync(runToken);
            _logger.LogInformation("Audio frame source started for speech recognition channel {ChannelId}.", ChannelId);
            _session = await _recognitionProvider.StartSessionAsync(ChannelId, _sessionOptions, runToken);
            _logger.LogInformation("Speech recognition session started for channel {ChannelId}.", ChannelId);

            _sendLoop = Task.Run(() => SendLoopAsync(runToken), CancellationToken.None);
            _readLoop = Task.Run(() => ReadLoopAsync(runToken), CancellationToken.None);
            _logger.LogInformation("Speech recognition channel {ChannelId} send/read loops started.", ChannelId);
        }
        catch (Exception ex)
        {
            await StopAsync(CancellationToken.None);
            _logger.LogError(ex, "Failed while starting speech recognition channel {ChannelId}.", ChannelId);
            throw;
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        CancellationTokenSource? runCts;
        ISpeechRecognitionSession? session;
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
            sendLoop = _sendLoop;
            readLoop = _readLoop;
            _runCts = null;
            _session = null;
            _sendLoop = null;
            _readLoop = null;
        }

        _logger.LogInformation("Stop speech recognition channel {ChannelId}.", ChannelId);
        runCts?.Cancel();
        await _frameSource.StopAsync();

        if (session is not null)
        {
            _logger.LogInformation("Completing speech recognition session for channel {ChannelId}.", ChannelId);
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
        if (_session is null)
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
            _logger.LogError(ex, "Speech recognition audio send loop failed for channel {ChannelId}.", ChannelId);
        }
        finally
        {
            _logger.LogInformation(
                "Speech recognition audio send loop ended for channel {ChannelId}: frames={Frames}, bytes={Bytes}.",
                ChannelId,
                framesSent,
                bytesSent);
        }
    }

    private async Task ReadLoopAsync(CancellationToken cancellationToken)
    {
        if (_session is null)
        {
            return;
        }

        long segmentsRead = 0;
        try
        {
            await foreach (var segment in _session.ReadSegmentsAsync(cancellationToken))
            {
                _captionStore.ApplyRecognitionSegment(segment);
                segmentsRead++;
                _logger.LogDebug(
                    "Applied recognition caption segment for channel {ChannelId}: sequence={Sequence}, stability={Stability}, textChars={TextChars}.",
                    ChannelId,
                    segment.ProviderSequence,
                    segment.Stability,
                    segment.Text.Length);
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Speech recognition caption read loop failed for channel {ChannelId}.", ChannelId);
        }
        finally
        {
            _logger.LogInformation(
                "Speech recognition caption read loop ended for channel {ChannelId}: segments={Segments}.",
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
        await _frameSource.DisposeAsync();
    }
}
