using System.Runtime.CompilerServices;
using GameSubRelay.Core.Audio;
using GameSubRelay.Core.Captions;
using GameSubRelay.Core.Translation;

namespace GameSubRelay.Infrastructure.Translation.Volcengine;

public sealed class VolcengineStreamingAsrProvider : ISpeechTranslationProvider
{
    private readonly VolcengineStreamingAsrOptions _options;
    private readonly VolcengineStreamingAsrProtocolCodec _codec;
    private readonly Func<IStreamingAsrWebSocketTransport> _transportFactory;

    public VolcengineStreamingAsrProvider(VolcengineStreamingAsrOptions options)
        : this(options, new VolcengineStreamingAsrProtocolCodec(), () => new ClientWebSocketStreamingAsrTransport())
    {
    }

    public VolcengineStreamingAsrProvider(
        VolcengineStreamingAsrOptions options,
        VolcengineStreamingAsrProtocolCodec codec,
        Func<IStreamingAsrWebSocketTransport> transportFactory)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _codec = codec ?? throw new ArgumentNullException(nameof(codec));
        _transportFactory = transportFactory ?? throw new ArgumentNullException(nameof(transportFactory));
    }

    public async Task<ISpeechTranslationSession> StartSessionAsync(
        AudioChannelId channelId,
        SpeechTranslationSessionOptions options,
        CancellationToken cancellationToken = default)
    {
        _options.EnsureCredentialsPresent();

        var connectId = Guid.NewGuid().ToString("D");
        var transport = _transportFactory();
        await transport
            .ConnectAsync(_options.Endpoint, _options.CreateHeaders(connectId), cancellationToken)
            .ConfigureAwait(false);

        var session = new VolcengineStreamingAsrSession(channelId, options, _codec, transport);
        await session.StartAsync(cancellationToken).ConfigureAwait(false);
        return session;
    }
}

public sealed class VolcengineStreamingAsrSession : ISpeechTranslationSession
{
    private const int AudioChunkBytes = 16_000 * 2 * 200 / 1_000;

    private readonly AudioChannelId _channelId;
    private readonly SpeechTranslationSessionOptions _options;
    private readonly VolcengineStreamingAsrProtocolCodec _codec;
    private readonly IStreamingAsrWebSocketTransport _transport;
    private readonly List<byte> _pendingAudio = [];
    private long _currentSequence = 1;
    private bool _started;
    private bool _completed;

    public VolcengineStreamingAsrSession(
        AudioChannelId channelId,
        SpeechTranslationSessionOptions options,
        VolcengineStreamingAsrProtocolCodec codec,
        IStreamingAsrWebSocketTransport transport)
    {
        _channelId = channelId;
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _codec = codec ?? throw new ArgumentNullException(nameof(codec));
        _transport = transport ?? throw new ArgumentNullException(nameof(transport));
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var payload = _codec.EncodeFullClientRequest(new VolcengineStreamingAsrStartRequest(
            MapLanguage(_options.SourceLanguage)));
        await _transport.SendAsync(payload, cancellationToken).ConfigureAwait(false);
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

            var message = _codec.DecodeServerResponse(payload);
            if (message.ErrorCode is not null)
            {
                throw new InvalidOperationException(
                    $"Volcengine streaming ASR failed: {message.ErrorCode} {message.ErrorMessage}");
            }

            var utterance = message.Utterances.LastOrDefault();
            var text = !string.IsNullOrWhiteSpace(utterance?.Text)
                ? utterance.Text
                : message.Text;
            if (string.IsNullOrWhiteSpace(text))
            {
                continue;
            }

            var isFinal = message.IsFinal || utterance?.Definite == true;
            var sequence = _currentSequence;
            yield return new TranslationSegment(
                _channelId,
                sequence,
                _options.SourceLanguage,
                _options.TargetLanguage,
                text.Trim(),
                string.Empty,
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
