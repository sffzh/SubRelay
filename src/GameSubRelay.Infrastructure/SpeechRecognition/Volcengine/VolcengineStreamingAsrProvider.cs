using System.Runtime.CompilerServices;
using GameSubRelay.Core.Audio;
using GameSubRelay.Core.Captions;
using GameSubRelay.Core.SpeechRecognition;
using GameSubRelay.Infrastructure.Volcengine;
using Microsoft.Extensions.Logging;

namespace GameSubRelay.Infrastructure.SpeechRecognition.Volcengine;

public sealed class VolcengineStreamingAsrProvider : ISpeechRecognitionProvider
{
    private readonly VolcengineStreamingAsrOptions _options;
    private readonly VolcengineStreamingAsrProtocolCodec _codec;
    private readonly Func<IStreamingAsrWebSocketTransport> _transportFactory;
    private readonly ILogger<VolcengineStreamingAsrProvider>? _logger;

    public VolcengineStreamingAsrProvider(VolcengineStreamingAsrOptions options)
        : this(options, new VolcengineStreamingAsrProtocolCodec(), () => new ClientWebSocketStreamingAsrTransport())
    {
    }

    public VolcengineStreamingAsrProvider(
        VolcengineStreamingAsrOptions options,
        VolcengineStreamingAsrProtocolCodec codec,
        Func<IStreamingAsrWebSocketTransport> transportFactory,
        ILogger<VolcengineStreamingAsrProvider>? logger = null)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _codec = codec ?? throw new ArgumentNullException(nameof(codec));
        _transportFactory = transportFactory ?? throw new ArgumentNullException(nameof(transportFactory));
        _logger = logger;
    }

    public async Task<ISpeechRecognitionSession> StartSessionAsync(
        AudioChannelId channelId,
        SpeechRecognitionSessionOptions options,
        CancellationToken cancellationToken = default)
    {
        _options.EnsureCredentialsPresent();

        var connectId = Guid.NewGuid().ToString("D");
        _logger?.LogInformation(
            "Starting streaming ASR session for {ChannelId}: endpoint={Endpoint}, resource={ResourceId}, connectId={ConnectId}, language={Language}.",
            channelId,
            _options.Endpoint,
            _options.ResourceId,
            connectId,
            options.Language);

        var transport = _transportFactory();
        await transport
            .ConnectAsync(_options.Endpoint, _options.CreateHeaders(connectId), cancellationToken)
            .ConfigureAwait(false);

        var session = new VolcengineStreamingAsrSession(channelId, options, _codec, transport, _logger);
        await session.StartAsync(cancellationToken).ConfigureAwait(false);
        return session;
    }
}

public sealed class VolcengineStreamingAsrSession : ISpeechRecognitionSession
{
    private const int AudioChunkBytes = 16_000 * 2 * 200 / 1_000;

    private readonly AudioChannelId _channelId;
    private readonly SpeechRecognitionSessionOptions _options;
    private readonly VolcengineStreamingAsrProtocolCodec _codec;
    private readonly IStreamingAsrWebSocketTransport _transport;
    private readonly ILogger? _logger;
    private readonly List<byte> _pendingAudio = [];
    private long _currentSequence = 1;
    private long _clientMessagesSent;
    private long _serverMessagesReceived;
    private long _audioBytesSent;
    private bool _started;
    private bool _completed;

    public VolcengineStreamingAsrSession(
        AudioChannelId channelId,
        SpeechRecognitionSessionOptions options,
        VolcengineStreamingAsrProtocolCodec codec,
        IStreamingAsrWebSocketTransport transport,
        ILogger? logger = null)
    {
        _channelId = channelId;
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _codec = codec ?? throw new ArgumentNullException(nameof(codec));
        _transport = transport ?? throw new ArgumentNullException(nameof(transport));
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var mappedLanguage = MapLanguage(_options.Language);
        _logger?.LogInformation(
            "Sending streaming ASR full client request: channel={ChannelId}, language={Language}, mappedLanguage={MappedLanguage}.",
            _channelId,
            _options.Language,
            mappedLanguage ?? "<auto>");
        var payload = _codec.EncodeFullClientRequest(new VolcengineStreamingAsrStartRequest(
            mappedLanguage));
        await _transport.SendAsync(payload, cancellationToken).ConfigureAwait(false);
        _clientMessagesSent++;
        _logger?.LogInformation(
            "Streaming ASR start request sent: channel={ChannelId}, payloadBytes={PayloadBytes}, mappedLanguage={MappedLanguage}.",
            _channelId,
            payload.Length,
            mappedLanguage ?? "<auto>");
        _started = true;
    }

    public async ValueTask SendAudioAsync(AudioFrame frame, CancellationToken cancellationToken)
    {
        EnsureStarted();
        if (_completed)
        {
            throw new InvalidOperationException("Cannot send audio after the ASR session has been completed.");
        }

        if (frame.ChannelId != _channelId)
        {
            throw new ArgumentException(
                $"Audio frame channel {frame.ChannelId} does not match ASR session channel {_channelId}.",
                nameof(frame));
        }

        _pendingAudio.AddRange(frame.Pcm16Mono16Khz);
        while (_pendingAudio.Count >= AudioChunkBytes)
        {
            var chunk = _pendingAudio.GetRange(0, AudioChunkBytes).ToArray();
            _pendingAudio.RemoveRange(0, AudioChunkBytes);
            await _transport
                .SendAsync(_codec.EncodeAudioOnlyRequest(chunk, isFinal: false), cancellationToken)
                .ConfigureAwait(false);
            _clientMessagesSent++;
            _audioBytesSent += chunk.Length;
            _logger?.LogDebug(
                "Streaming ASR sent audio chunk: channel={ChannelId}, bytes={ChunkBytes}, totalBytes={TotalBytes}.",
                _channelId,
                chunk.Length,
                _audioBytesSent);
        }
    }

    public async ValueTask CompleteAsync(CancellationToken cancellationToken)
    {
        EnsureStarted();
        if (_completed)
        {
            return;
        }

        var chunk = _pendingAudio.ToArray();
        _pendingAudio.Clear();
        await _transport
            .SendAsync(_codec.EncodeAudioOnlyRequest(chunk, isFinal: true), cancellationToken)
            .ConfigureAwait(false);
        _clientMessagesSent++;
        _audioBytesSent += chunk.Length;
        _logger?.LogInformation(
            "Streaming ASR final audio sent: channel={ChannelId}, finalChunkBytes={ChunkBytes}, totalBytes={AudioBytes}, clientMessages={ClientMessages}.",
            _channelId,
            chunk.Length,
            _audioBytesSent,
            _clientMessagesSent);
        _completed = true;
    }

    public async IAsyncEnumerable<SpeechRecognitionSegment> ReadSegmentsAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        EnsureStarted();

        while (true)
        {
            var payload = await _transport.ReceiveAsync(cancellationToken).ConfigureAwait(false);
            if (payload is null)
            {
                _logger?.LogInformation(
                    "Streaming ASR WebSocket closed: channel={ChannelId}, serverMessages={ServerMessages}, audioBytes={AudioBytes}.",
                    _channelId,
                    _serverMessagesReceived,
                    _audioBytesSent);
                yield break;
            }

            var frameDescription = VolcengineStreamingAsrProtocolCodec.DescribeServerResponseFrame(payload);
            _logger?.LogDebug(
                "Streaming ASR server frame: channel={ChannelId}, {FrameDescription}.",
                _channelId,
                frameDescription);

            VolcengineStreamingAsrServerMessage message;
            try
            {
                message = _codec.DecodeServerResponse(payload);
            }
            catch (Exception ex)
            {
                _logger?.LogError(
                    ex,
                    "Failed to decode streaming ASR server frame: channel={ChannelId}, {FrameDescription}.",
                    _channelId,
                    frameDescription);
                throw;
            }

            _serverMessagesReceived++;
            if (message.ErrorCode is not null)
            {
                _logger?.LogWarning(
                    "Streaming ASR server error: channel={ChannelId}, errorCode={ErrorCode}, message={ErrorMessage}.",
                    _channelId,
                    message.ErrorCode,
                    message.ErrorMessage);
                throw new InvalidOperationException(
                    $"Volcengine streaming ASR failed: {message.ErrorCode} {message.ErrorMessage}");
            }

            var utterance = message.Utterances.LastOrDefault();
            var text = !string.IsNullOrWhiteSpace(utterance?.Text)
                ? utterance.Text
                : message.Text;
            if (string.IsNullOrWhiteSpace(text))
            {
                _logger?.LogDebug(
                    "Streaming ASR response without text: channel={ChannelId}, sequence={Sequence}, final={Final}.",
                    _channelId,
                    message.Sequence,
                    message.IsFinal);
                continue;
            }

            var isFinal = message.IsFinal || utterance?.Definite == true;
            var sequence = _currentSequence;
            _logger?.LogInformation(
                "Streaming ASR segment: channel={ChannelId}, providerSequence={ProviderSequence}, serverSequence={ServerSequence}, final={Final}, text={Text}, begin={BeginTimeMs}, end={EndTimeMs}, utterances={UtteranceCount}.",
                _channelId,
                sequence,
                message.Sequence,
                isFinal,
                VolcengineLogFormatter.FormatText(text),
                utterance?.StartTimeMs ?? 0,
                utterance?.EndTimeMs ?? 0,
                message.Utterances.Count);
            yield return new SpeechRecognitionSegment(
                _channelId,
                sequence,
                _options.Language,
                text.Trim(),
                isFinal ? SegmentStability.Final : SegmentStability.Interim,
                TimeSpan.FromMilliseconds(utterance?.StartTimeMs ?? 0),
                TimeSpan.FromMilliseconds(utterance?.EndTimeMs ?? 0));

            if (isFinal)
            {
                _currentSequence++;
            }
        }
    }

    public ValueTask DisposeAsync()
    {
        return _transport.DisposeAsync();
    }

    private void EnsureStarted()
    {
        if (!_started)
        {
            throw new InvalidOperationException("Volcengine streaming ASR session has not started.");
        }
    }

    private static string? MapLanguage(string sourceLanguage)
    {
        return sourceLanguage.Trim().ToLowerInvariant() switch
        {
            "zh" or "zhen" => "zh-CN",
            "en" => "en-US",
            "ja" => "ja-JP",
            "de" => "de-DE",
            "fr" => "fr-FR",
            "es" => "es-ES",
            "pt" => "pt-PT",
            "id" => "id-ID",
            _ => null
        };
    }
}
