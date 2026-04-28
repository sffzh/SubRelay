using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using GameSubRelay.Core.Audio;
using GameSubRelay.Core.Translation;
using GameSubRelay.Infrastructure.Audio;

namespace GameSubRelay.Infrastructure.Translation.Volcengine;

public sealed class VolcengineAstSpeechTranslationProvider : ISpeechTranslationProvider
{
    private readonly VolcengineAstProviderOptions _options;
    private readonly IAstProtocolCodec _codec;
    private readonly Func<IAstWebSocketTransport> _transportFactory;
    private readonly IAudioOutputPlayer? _audioOutputPlayer;
    private readonly string? _renderDeviceId;

    public VolcengineAstSpeechTranslationProvider(VolcengineAstProviderOptions options)
        : this(options, new VolcengineAstProtobufProtocolCodec(), () => new ClientWebSocketAstTransport())
    {
    }

    public VolcengineAstSpeechTranslationProvider(
        VolcengineAstProviderOptions options,
        IAstProtocolCodec codec,
        Func<IAstWebSocketTransport> transportFactory,
        IAudioOutputPlayer? audioOutputPlayer = null,
        string? renderDeviceId = null)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _codec = codec ?? throw new ArgumentNullException(nameof(codec));
        _transportFactory = transportFactory ?? throw new ArgumentNullException(nameof(transportFactory));
        _audioOutputPlayer = audioOutputPlayer;
        _renderDeviceId = renderDeviceId;
    }

    public async Task<ISpeechTranslationSession> StartSessionAsync(
        AudioChannelId channelId,
        SpeechTranslationSessionOptions options,
        CancellationToken cancellationToken = default)
    {
        var transport = _transportFactory();
        await transport.ConnectAsync(
                _options.Endpoint,
                _options.CreateHeaders(),
                cancellationToken)
            .ConfigureAwait(false);

        var session = new VolcengineAstSpeechTranslationSession(
            channelId,
            options,
            _options,
            _codec,
            transport,
            _audioOutputPlayer,
            _renderDeviceId);

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
    private readonly IAudioOutputPlayer? _audioOutputPlayer;
    private readonly string? _renderDeviceId;
    private readonly VolcengineAstSubtitleMapper _mapper;
    private readonly List<byte> _pendingAudio = [];
    private readonly string _connectionId = Guid.NewGuid().ToString("D");
    private readonly string _sessionId = Guid.NewGuid().ToString("D");
    private int _sequence;
    private bool _started;
    private bool _completed;

    public VolcengineAstSpeechTranslationSession(
        AudioChannelId channelId,
        SpeechTranslationSessionOptions options,
        VolcengineAstProviderOptions providerOptions,
        IAstProtocolCodec codec,
        IAstWebSocketTransport transport,
        IAudioOutputPlayer? audioOutputPlayer = null,
        string? renderDeviceId = null)
    {
        _channelId = channelId;
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _providerOptions = providerOptions ?? throw new ArgumentNullException(nameof(providerOptions));
        _codec = codec ?? throw new ArgumentNullException(nameof(codec));
        _transport = transport ?? throw new ArgumentNullException(nameof(transport));
        _audioOutputPlayer = audioOutputPlayer;
        _renderDeviceId = renderDeviceId;
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
            SourceAudio: AstAudioConfig.Pcm16Mono16Khz,
            TargetAudio: string.Equals(mode, "s2s", StringComparison.OrdinalIgnoreCase)
                ? AstAudioConfig.Pcm16Mono16Khz
                : null);

        if (_audioOutputPlayer is not null &&
            string.Equals(mode, "s2s", StringComparison.OrdinalIgnoreCase))
        {
            await _audioOutputPlayer.StartAsync(_renderDeviceId, cancellationToken).ConfigureAwait(false);
        }

        await SendMessageAsync(AstClientMessage.StartSession(config, NextMeta()), cancellationToken)
            .ConfigureAwait(false);

        var payload = await _transport.ReceiveAsync(cancellationToken).ConfigureAwait(false);
        if (payload is null)
        {
            throw VolcengineAstProviderException.Protocol(
                "Volcengine AST WebSocket closed before SessionStarted.");
        }

        var response = _codec.Decode(payload);
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
        }

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
            if (message.Event == AstServerEventType.SessionFinished)
            {
                yield break;
            }

            if (message.Event == AstServerEventType.TTSResponse &&
                message.Data is { Length: > 0 } audioData &&
                _audioOutputPlayer is not null)
            {
                await _audioOutputPlayer.PlayAsync(audioData, cancellationToken).ConfigureAwait(false);
            }

            foreach (var segment in _mapper.Apply(message))
            {
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

    private void EnsureStarted()
    {
        if (!_started)
        {
            throw new InvalidOperationException("Volcengine AST session has not received SessionStarted.");
        }
    }
}
