using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using GameSubRelay.Core.Audio;
using GameSubRelay.Core.Translation;
using GameSubRelay.Infrastructure.Audio;
using GameSubRelay.Infrastructure.Volcengine;
using Microsoft.Extensions.Logging;

namespace GameSubRelay.Infrastructure.Translation.Volcengine;

public sealed class VolcengineAstSpeechTranslationProvider : ISpeechTranslationProvider
{
    private readonly VolcengineAstProviderOptions _options;
    private readonly IAstProtocolCodec _codec;
    private readonly Func<IAstWebSocketTransport> _transportFactory;
    private readonly IAudioOutputPlayer? _audioOutputPlayer;
    private readonly string? _renderDeviceId;
    private readonly ILogger<VolcengineAstSpeechTranslationProvider>? _logger;

    public VolcengineAstSpeechTranslationProvider(VolcengineAstProviderOptions options)
        : this(options, new VolcengineAstProtobufProtocolCodec(), () => new ClientWebSocketAstTransport())
    {
    }

    public VolcengineAstSpeechTranslationProvider(
        VolcengineAstProviderOptions options,
        IAstProtocolCodec codec,
        Func<IAstWebSocketTransport> transportFactory,
        IAudioOutputPlayer? audioOutputPlayer = null,
        string? renderDeviceId = null,
        ILogger<VolcengineAstSpeechTranslationProvider>? logger = null)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _codec = codec ?? throw new ArgumentNullException(nameof(codec));
        _transportFactory = transportFactory ?? throw new ArgumentNullException(nameof(transportFactory));
        _audioOutputPlayer = audioOutputPlayer;
        _renderDeviceId = renderDeviceId;
        _logger = logger;
    }

    public async Task<ISpeechTranslationSession> StartSessionAsync(
        AudioChannelId channelId,
        SpeechTranslationSessionOptions options,
        CancellationToken cancellationToken = default)
    {
        var connectionId = Guid.NewGuid().ToString("D");
        _logger?.LogInformation(
            "Starting AST session for {ChannelId}: endpoint={Endpoint}, resource={ResourceId}, connectId={ConnectId}, mode={Mode}, language={SourceLanguage}->{TargetLanguage}, outputDevice={OutputDevice}",
            channelId,
            _options.Endpoint,
            _options.ResourceId,
            connectionId,
            options.Mode,
            options.SourceLanguage,
            options.TargetLanguage,
            string.IsNullOrWhiteSpace(_renderDeviceId) ? "<default>" : _renderDeviceId);

        var transport = _transportFactory();
        await transport.ConnectAsync(
                _options.Endpoint,
                _options.CreateHeaders(connectionId),
                cancellationToken)
            .ConfigureAwait(false);

        var session = new VolcengineAstSpeechTranslationSession(
            channelId,
            options,
            _options,
            _codec,
            transport,
            connectionId,
            _audioOutputPlayer,
            _renderDeviceId,
            _logger);

        await session.StartAsync(cancellationToken).ConfigureAwait(false);
        return session;
    }
}

public sealed class VolcengineAstSpeechTranslationSession : ISpeechTranslationSession
{
    private const int AudioChunkBytes = 16_000 * 2 * 80 / 1_000;

    private readonly AudioChannelId _channelId;
    private readonly SpeechTranslationSessionOptions _options;
    private readonly VolcengineAstProviderOptions _providerOptions;
    private readonly IAstProtocolCodec _codec;
    private readonly IAstWebSocketTransport _transport;
    private readonly string _connectionId;
    private readonly IAudioOutputPlayer? _audioOutputPlayer;
    private readonly string? _renderDeviceId;
    private readonly ILogger? _logger;
    private readonly VolcengineAstSubtitleMapper _mapper;
    private readonly List<byte> _pendingAudio = [];
    private readonly string _sessionId = Guid.NewGuid().ToString("D");
    private int _sequence;
    private long _clientMessagesSent;
    private long _serverMessagesReceived;
    private long _audioBytesSent;
    private bool _started;
    private bool _completed;

    public VolcengineAstSpeechTranslationSession(
        AudioChannelId channelId,
        SpeechTranslationSessionOptions options,
        VolcengineAstProviderOptions providerOptions,
        IAstProtocolCodec codec,
        IAstWebSocketTransport transport,
        string connectionId,
        IAudioOutputPlayer? audioOutputPlayer = null,
        string? renderDeviceId = null,
        ILogger? logger = null)
    {
        _channelId = channelId;
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _providerOptions = providerOptions ?? throw new ArgumentNullException(nameof(providerOptions));
        _codec = codec ?? throw new ArgumentNullException(nameof(codec));
        _transport = transport ?? throw new ArgumentNullException(nameof(transport));
        _connectionId = string.IsNullOrWhiteSpace(connectionId)
            ? throw new ArgumentException("Connection id is required.", nameof(connectionId))
            : connectionId;
        _audioOutputPlayer = audioOutputPlayer;
        _renderDeviceId = renderDeviceId;
        _logger = logger;
        _mapper = new VolcengineAstSubtitleMapper(
            channelId,
            options.SourceLanguage,
            options.TargetLanguage);
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var mode = string.IsNullOrWhiteSpace(_options.Mode)
            ? SpeechTranslationSessionOptions.DefaultMode
            : _options.Mode.Trim();
        var config = new AstSessionConfig(
            _options.SourceLanguage,
            _options.TargetLanguage,
            Mode: mode,
            HotWords: _options.HotWords,
            SourceAudio: AstAudioConfig.SourceWavPcm16Mono16Khz,
            TargetAudio: string.Equals(mode, "s2s", StringComparison.OrdinalIgnoreCase)
                ? AstAudioConfig.TargetPcm16Mono16Khz
                : null);

        if (_audioOutputPlayer is not null &&
            string.Equals(mode, "s2s", StringComparison.OrdinalIgnoreCase))
        {
            _logger?.LogInformation(
                "Starting AST returned-audio output for {ChannelId} on device {OutputDevice}.",
                _channelId,
                string.IsNullOrWhiteSpace(_renderDeviceId) ? "<default>" : _renderDeviceId);
            await _audioOutputPlayer.StartAsync(_renderDeviceId, cancellationToken).ConfigureAwait(false);
        }

        _logger?.LogInformation(
            "Sending AST StartSession: channel={ChannelId}, sessionId={SessionId}, connectId={ConnectId}, mode={Mode}, language={SourceLanguage}->{TargetLanguage}.",
            _channelId,
            _sessionId,
            _connectionId,
            mode,
            _options.SourceLanguage,
            _options.TargetLanguage);
        await SendMessageAsync(AstClientMessage.StartSession(config, NextMeta()), cancellationToken)
            .ConfigureAwait(false);

        var payload = await _transport.ReceiveAsync(cancellationToken).ConfigureAwait(false);
        if (payload is null)
        {
            throw VolcengineAstProviderException.Protocol(
                "Volcengine AST WebSocket closed before SessionStarted.");
        }

        var response = _codec.Decode(payload);
        LogServerMessage(response);
        if (response.Event == AstServerEventType.SessionFailed)
        {
            throw VolcengineAstProviderException.FromSessionFailed(response.ResponseMeta);
        }

        if (response.Event != AstServerEventType.SessionStarted)
        {
            throw VolcengineAstProviderException.Protocol(
                $"Volcengine AST expected SessionStarted but received {response.Event}.");
        }

        _started = true;
        _logger?.LogInformation(
            "AST session started: channel={ChannelId}, sessionId={SessionId}, connectId={ConnectId}.",
            _channelId,
            _sessionId,
            _connectionId);
    }

    public async ValueTask SendAudioAsync(AudioFrame frame, CancellationToken cancellationToken)
    {
        EnsureStarted();

        if (_completed)
        {
            throw new InvalidOperationException("Cannot send audio after the AST session has been completed.");
        }

        if (frame.ChannelId != _channelId)
        {
            throw new ArgumentException(
                $"Audio frame channel {frame.ChannelId} does not match AST session channel {_channelId}.",
                nameof(frame));
        }

        _pendingAudio.AddRange(frame.Pcm16Mono16Khz);
        while (_pendingAudio.Count >= AudioChunkBytes)
        {
            var chunk = _pendingAudio.GetRange(0, AudioChunkBytes).ToArray();
            _pendingAudio.RemoveRange(0, AudioChunkBytes);
            await SendMessageAsync(AstClientMessage.TaskRequest(chunk, NextMeta()), cancellationToken)
                .ConfigureAwait(false);
            _audioBytesSent += chunk.Length;
            _logger?.LogDebug(
                "AST sent audio chunk: channel={ChannelId}, bytes={ChunkBytes}, totalBytes={TotalBytes}.",
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

        if (_pendingAudio.Count > 0)
        {
            var chunk = _pendingAudio.ToArray();
            _pendingAudio.Clear();
            await SendMessageAsync(AstClientMessage.TaskRequest(chunk, NextMeta()), cancellationToken)
                .ConfigureAwait(false);
            _audioBytesSent += chunk.Length;
            _logger?.LogInformation(
                "AST flushed pending audio before finish: channel={ChannelId}, bytes={ChunkBytes}, totalBytes={TotalBytes}.",
                _channelId,
                chunk.Length,
                _audioBytesSent);
        }

        _logger?.LogInformation(
            "Sending AST FinishSession: channel={ChannelId}, sessionId={SessionId}, audioBytes={AudioBytes}.",
            _channelId,
            _sessionId,
            _audioBytesSent);
        await SendMessageAsync(AstClientMessage.FinishSession(NextMeta()), cancellationToken)
            .ConfigureAwait(false);
        _completed = true;
    }

    public async IAsyncEnumerable<TranslationSegment> ReadSegmentsAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        EnsureStarted();

        while (true)
        {
            var payload = await _transport.ReceiveAsync(cancellationToken).ConfigureAwait(false);
            if (payload is null)
            {
                yield break;
            }

            var message = _codec.Decode(payload);
            LogServerMessage(message);
            if (message.Event == AstServerEventType.SessionFinished)
            {
                _logger?.LogInformation(
                    "AST session finished: channel={ChannelId}, sessionId={SessionId}, clientMessages={ClientMessages}, serverMessages={ServerMessages}, audioBytes={AudioBytes}.",
                    _channelId,
                    _sessionId,
                    _clientMessagesSent,
                    _serverMessagesReceived,
                    _audioBytesSent);
                yield break;
            }

            if (message.Event == AstServerEventType.TTSResponse &&
                message.Data is { Length: > 0 } audioData &&
                _audioOutputPlayer is not null)
            {
                _logger?.LogInformation(
                    "AST received returned audio: channel={ChannelId}, bytes={AudioBytes}.",
                    _channelId,
                    audioData.Length);
                await _audioOutputPlayer.PlayAsync(audioData, cancellationToken).ConfigureAwait(false);
            }

            foreach (var segment in _mapper.Apply(message))
            {
                _logger?.LogInformation(
                    "AST caption segment: channel={ChannelId}, sequence={Sequence}, stability={Stability}, source={SourceText}, translated={TranslatedText}.",
                    segment.ChannelId,
                    segment.ProviderSequence,
                    segment.Stability,
                    VolcengineLogFormatter.FormatText(segment.SourceText),
                    VolcengineLogFormatter.FormatText(segment.TranslatedText));
                yield return segment;
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_audioOutputPlayer is not null)
        {
            await _audioOutputPlayer.StopAsync().ConfigureAwait(false);
            await _audioOutputPlayer.DisposeAsync().ConfigureAwait(false);
        }

        await _transport.DisposeAsync().ConfigureAwait(false);
    }

    private async ValueTask SendMessageAsync(AstClientMessage message, CancellationToken cancellationToken)
    {
        var payload = _codec.Encode(message);
        _clientMessagesSent++;
        _logger?.LogDebug(
            "AST client event: channel={ChannelId}, event={Event}, sequence={Sequence}, audioBytes={AudioBytes}, payloadBytes={PayloadBytes}, sessionId={SessionId}.",
            _channelId,
            message.Event,
            message.RequestMeta?.Sequence,
            message.AudioData?.Length ?? 0,
            payload.Length,
            _sessionId);
        await _transport.SendAsync(payload, cancellationToken).ConfigureAwait(false);
    }

    private AstRequestMeta NextMeta()
    {
        return new AstRequestMeta(
            _providerOptions.ResourceId,
            _providerOptions.AppKey,
            _providerOptions.ResourceId,
            _connectionId,
            _sessionId,
            ++_sequence);
    }

    private void LogServerMessage(AstServerMessage message)
    {
        _serverMessagesReceived++;
        var statusCode = message.ResponseMeta?.StatusCode;
        if (message.Event == AstServerEventType.SessionFailed ||
            statusCode is not null and not 0 and not 20000000)
        {
            _logger?.LogWarning(
                "AST server problem: channel={ChannelId}, sessionId={SessionId}, {Message}.",
                _channelId,
                _sessionId,
                VolcengineLogFormatter.FormatAstServerMessage(message));
            return;
        }

        var level = message.Event switch
        {
            AstServerEventType.SessionStarted or
            AstServerEventType.SessionFinished or
            AstServerEventType.UsageResponse or
            AstServerEventType.AudioMuted or
            AstServerEventType.TTSSentenceStart or
            AstServerEventType.TTSSentenceEnd or
            AstServerEventType.TTSResponse or
            AstServerEventType.SourceSubtitleEnd or
            AstServerEventType.TranslationSubtitleEnd => LogLevel.Information,
            _ => LogLevel.Debug
        };

        _logger?.Log(
            level,
            "AST server event: channel={ChannelId}, sessionId={SessionId}, {Message}.",
            _channelId,
            _sessionId,
            VolcengineLogFormatter.FormatAstServerMessage(message));
    }

    private void EnsureStarted()
    {
        if (!_started)
        {
            throw new InvalidOperationException("Volcengine AST session has not received SessionStarted.");
        }
    }
}
