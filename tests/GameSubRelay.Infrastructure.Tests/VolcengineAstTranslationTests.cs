using FluentAssertions;
using GameSubRelay.Core.Audio;
using GameSubRelay.Core.Captions;
using GameSubRelay.Core.Translation;
using GameSubRelay.Infrastructure.Translation.Volcengine;
using Xunit;

namespace GameSubRelay.Infrastructure.Tests;

public class VolcengineAstTranslationTests
{
    [Fact]
    public void Ast_options_build_required_auth_headers()
    {
        var options = new VolcengineAstProviderOptions(
            AppKey: "app-key",
            AccessKey: "access-key");

        var headers = options.CreateHeaders();

        headers.Should().Contain("X-Api-App-Key", "app-key");
        headers.Should().Contain("X-Api-Access-Key", "access-key");
        headers.Should().Contain("X-Api-Resource-Id", VolcengineAstProviderOptions.DefaultResourceId);
        headers.Keys.Should().OnlyContain(key => key.StartsWith("X-Api-", StringComparison.Ordinal));
    }

    [Fact]
    public void Ast_mapper_emits_interim_and_final_segments_from_source_and_translation_events()
    {
        var mapper = new VolcengineAstSubtitleMapper(
            AudioChannelId.Monitor,
            sourceLanguage: "en",
            targetLanguage: "zh");

        mapper.Apply(AstServerMessage.SourceStart(startTimeMs: 1_200)).Should().BeEmpty();
        mapper.Apply(AstServerMessage.SourceResponse("enemy")).Should().BeEmpty();

        var interim = mapper.Apply(AstServerMessage.TranslationResponse("敌人")).Should().ContainSingle().Subject;
        interim.ChannelId.Should().Be(AudioChannelId.Monitor);
        interim.ProviderSequence.Should().Be(1);
        interim.SourceText.Should().Be("enemy");
        interim.TranslatedText.Should().Be("敌人");
        interim.Stability.Should().Be(SegmentStability.Interim);
        interim.BeginTime.Should().Be(TimeSpan.FromMilliseconds(1_200));

        mapper.Apply(AstServerMessage.SourceEnd("enemy on the left", startTimeMs: 1_200, endTimeMs: 2_400));

        var final = mapper.Apply(AstServerMessage.TranslationEnd("敌人在左边", startTimeMs: 1_200, endTimeMs: 2_450))
            .Should()
            .ContainSingle()
            .Subject;

        final.ProviderSequence.Should().Be(1);
        final.SourceText.Should().Be("enemy on the left");
        final.TranslatedText.Should().Be("敌人在左边");
        final.Stability.Should().Be(SegmentStability.Final);
        final.BeginTime.Should().Be(TimeSpan.FromMilliseconds(1_200));
        final.EndTime.Should().Be(TimeSpan.FromMilliseconds(2_450));
    }

    [Fact]
    public void Ast_mapper_waits_for_source_when_translation_arrives_first()
    {
        var mapper = new VolcengineAstSubtitleMapper(
            AudioChannelId.Microphone,
            sourceLanguage: "en",
            targetLanguage: "zh");

        mapper.Apply(AstServerMessage.TranslationStart(startTimeMs: 500)).Should().BeEmpty();
        mapper.Apply(AstServerMessage.TranslationResponse("你好")).Should().BeEmpty();

        var segment = mapper.Apply(AstServerMessage.SourceResponse("hello")).Should().ContainSingle().Subject;

        segment.ProviderSequence.Should().Be(1);
        segment.SourceText.Should().Be("hello");
        segment.TranslatedText.Should().Be("你好");
        segment.Stability.Should().Be(SegmentStability.Interim);
        segment.BeginTime.Should().Be(TimeSpan.FromMilliseconds(500));
    }

    [Theory]
    [InlineData(401, VolcengineAstErrorKind.Authentication, false)]
    [InlineData(403, VolcengineAstErrorKind.Authorization, false)]
    [InlineData(429, VolcengineAstErrorKind.RateLimited, true)]
    [InlineData(500, VolcengineAstErrorKind.RetryableServer, true)]
    [InlineData(-301, VolcengineAstErrorKind.ReconnectRequired, true)]
    public void Ast_session_failed_errors_are_typed(int statusCode, VolcengineAstErrorKind expectedKind, bool expectedRetryable)
    {
        var mapper = new VolcengineAstSubtitleMapper(
            AudioChannelId.Microphone,
            sourceLanguage: "en",
            targetLanguage: "zh");

        var act = () => mapper.Apply(AstServerMessage.SessionFailed(statusCode, "failed"));

        var exception = act.Should().Throw<VolcengineAstProviderException>().Which;
        exception.Kind.Should().Be(expectedKind);
        exception.IsRetryable.Should().Be(expectedRetryable);
    }

    [Fact]
    public async Task Ast_provider_session_sends_start_audio_and_finish_messages()
    {
        var transport = new RecordingAstWebSocketTransport(new[] { new byte[] { 0x01 } });
        var codec = new RecordingAstProtocolCodec(
            new AstServerMessage(AstServerEventType.SessionStarted));
        var provider = new VolcengineAstSpeechTranslationProvider(
            new VolcengineAstProviderOptions("app-key", "access-key"),
            codec,
            () => transport);

        await using var session = await provider.StartSessionAsync(
            AudioChannelId.Microphone,
            new SpeechTranslationSessionOptions("en", "zh", "cn-north-1"),
            CancellationToken.None);

        transport.ConnectedUri.Should().Be(VolcengineAstProviderOptions.DefaultEndpoint);
        transport.Headers.Should().Contain("X-Api-App-Key", "app-key");
        codec.EncodedMessages.Should().ContainSingle(message => message.Event == AstClientEventType.StartSession);
        codec.EncodedMessages[0].SessionConfig.Should().NotBeNull();
        codec.EncodedMessages[0].SessionConfig!.SourceLanguage.Should().Be("en");
        codec.EncodedMessages[0].SessionConfig!.TargetLanguage.Should().Be("zh");

        var frame = new AudioFrame(
            AudioChannelId.Microphone,
            new byte[] { 0x11, 0x22, 0x33 },
            DateTimeOffset.UtcNow,
            TimeSpan.FromMilliseconds(80));

        await session.SendAudioAsync(frame, CancellationToken.None);
        await session.CompleteAsync(CancellationToken.None);

        codec.EncodedMessages.Select(message => message.Event).Should().Equal(
            AstClientEventType.StartSession,
            AstClientEventType.TaskRequest,
            AstClientEventType.FinishSession);
        codec.EncodedMessages[1].AudioData.Should().Equal(frame.Pcm16Mono16Khz);
        transport.SentPayloads.Should().HaveCount(3);
    }

    [Fact]
    public void Default_ast_protocol_codec_encodes_start_session_and_decodes_tts_response()
    {
        var codec = new VolcengineAstProtobufProtocolCodec();

        var encoded = codec.Encode(AstClientMessage.StartSession(new AstSessionConfig(
            SourceLanguage: "zh",
            TargetLanguage: "en",
            Mode: "s2s")));

        encoded.Length.Should().BeGreaterThan(0);

        var decoded = codec.Decode(CreateAstResponsePayload(
            AstServerEventType.TTSResponse,
            data: [0x11, 0x22, 0x33]));

        decoded.Event.Should().Be(AstServerEventType.TTSResponse);
        decoded.Data.Should().Equal(0x11, 0x22, 0x33);
    }

    [Fact]
    public async Task Ast_session_uses_s2s_mode_and_routes_tts_audio_to_output_player()
    {
        var transport = new RecordingAstWebSocketTransport(new[] { new byte[] { 0x01 }, new byte[] { 0x02 }, new byte[] { 0x03 } });
        var codec = new RecordingAstProtocolCodec(
            new AstServerMessage(AstServerEventType.SessionStarted),
            new AstServerMessage(AstServerEventType.TTSResponse, Data: [0x10, 0x20]),
            new AstServerMessage(AstServerEventType.SessionFinished));
        var output = new RecordingAudioOutputPlayer();
        var provider = new VolcengineAstSpeechTranslationProvider(
            new VolcengineAstProviderOptions("app-key", "access-key"),
            codec,
            () => transport,
            output,
            renderDeviceId: "headphones");

        await using var session = await provider.StartSessionAsync(
            AudioChannelId.Microphone,
            new SpeechTranslationSessionOptions("zh", "en", "cn-north-1", Mode: "s2s"),
            CancellationToken.None);

        await foreach (var _ in session.ReadSegmentsAsync(CancellationToken.None))
        {
        }

        codec.EncodedMessages[0].SessionConfig!.Mode.Should().Be("s2s");
        output.StartedDeviceIds.Should().ContainSingle().Which.Should().Be("headphones");
        output.PlayedChunks.Should().ContainSingle().Which.Should().Equal(0x10, 0x20);
    }

    private static byte[] CreateAstResponsePayload(
        AstServerEventType eventType,
        string? text = null,
        byte[]? data = null)
    {
        var bytes = new List<byte>();
        WriteVarintField(bytes, fieldNumber: 2, (ulong)eventType);
        if (data is not null)
        {
            WriteLengthDelimitedField(bytes, fieldNumber: 3, data);
        }

        if (text is not null)
        {
            WriteLengthDelimitedField(bytes, fieldNumber: 4, System.Text.Encoding.UTF8.GetBytes(text));
        }

        return bytes.ToArray();
    }

    private static void WriteVarintField(List<byte> target, int fieldNumber, ulong value)
    {
        WriteVarint(target, ((ulong)fieldNumber << 3) | 0);
        WriteVarint(target, value);
    }

    private static void WriteLengthDelimitedField(List<byte> target, int fieldNumber, byte[] value)
    {
        WriteVarint(target, ((ulong)fieldNumber << 3) | 2);
        WriteVarint(target, (ulong)value.Length);
        target.AddRange(value);
    }

    private static void WriteVarint(List<byte> target, ulong value)
    {
        while (value >= 0x80)
        {
            target.Add((byte)(value | 0x80));
            value >>= 7;
        }

        target.Add((byte)value);
    }

    private sealed class RecordingAstProtocolCodec : IAstProtocolCodec
    {
        private readonly Queue<AstServerMessage> _responses;

        public RecordingAstProtocolCodec(params AstServerMessage[] responses)
        {
            _responses = new Queue<AstServerMessage>(responses);
        }

        public List<AstClientMessage> EncodedMessages { get; } = new();

        public ReadOnlyMemory<byte> Encode(AstClientMessage message)
        {
            EncodedMessages.Add(message);
            return new byte[] { (byte)message.Event };
        }

        public AstServerMessage Decode(ReadOnlyMemory<byte> payload)
        {
            return _responses.Dequeue();
        }
    }

    private sealed class RecordingAstWebSocketTransport : IAstWebSocketTransport
    {
        private readonly Queue<byte[]> _receivePayloads;

        public RecordingAstWebSocketTransport(IEnumerable<byte[]> receivePayloads)
        {
            _receivePayloads = new Queue<byte[]>(receivePayloads);
        }

        public Uri? ConnectedUri { get; private set; }

        public IReadOnlyDictionary<string, string> Headers { get; private set; } =
            new Dictionary<string, string>();

        public List<byte[]> SentPayloads { get; } = new();

        public Task ConnectAsync(
            Uri endpoint,
            IReadOnlyDictionary<string, string> headers,
            CancellationToken cancellationToken)
        {
            ConnectedUri = endpoint;
            Headers = headers;
            return Task.CompletedTask;
        }

        public ValueTask SendAsync(ReadOnlyMemory<byte> payload, CancellationToken cancellationToken)
        {
            SentPayloads.Add(payload.ToArray());
            return ValueTask.CompletedTask;
        }

        public ValueTask<byte[]?> ReceiveAsync(CancellationToken cancellationToken)
        {
            return ValueTask.FromResult(_receivePayloads.Count == 0 ? null : _receivePayloads.Dequeue());
        }

        public ValueTask DisposeAsync()
        {
            return ValueTask.CompletedTask;
        }
    }

    private sealed class RecordingAudioOutputPlayer : GameSubRelay.Infrastructure.Audio.IAudioOutputPlayer
    {
        public List<string?> StartedDeviceIds { get; } = [];

        public List<byte[]> PlayedChunks { get; } = [];

        public bool IsRunning { get; private set; }

        public Task StartAsync(string? renderDeviceId = null, CancellationToken cancellationToken = default)
        {
            StartedDeviceIds.Add(renderDeviceId);
            IsRunning = true;
            return Task.CompletedTask;
        }

        public Task PlayAsync(byte[] pcm16Mono16Khz, CancellationToken cancellationToken = default)
        {
            PlayedChunks.Add(pcm16Mono16Khz.ToArray());
            return Task.CompletedTask;
        }

        public Task StopAsync()
        {
            IsRunning = false;
            return Task.CompletedTask;
        }

        public ValueTask DisposeAsync()
        {
            return ValueTask.CompletedTask;
        }
    }
}
