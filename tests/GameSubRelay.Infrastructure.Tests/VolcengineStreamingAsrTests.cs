using System.IO.Compression;
using System.Text;
using FluentAssertions;
using GameSubRelay.Core.Audio;
using GameSubRelay.Core.Captions;
using GameSubRelay.Core.SpeechRecognition;
using GameSubRelay.Infrastructure.SpeechRecognition.Volcengine;
using Xunit;

namespace GameSubRelay.Infrastructure.Tests;

public sealed class VolcengineStreamingAsrTests
{
    [Fact]
    public void Streaming_asr_options_default_to_bigmodel_async_and_build_headers()
    {
        var options = new VolcengineStreamingAsrOptions("app-key", "access-key");

        options.Endpoint.Should().Be(VolcengineStreamingAsrOptions.OptimizedBidirectionalEndpoint);

        var headers = options.CreateHeaders("connect-id");

        headers.Should().Contain("X-Api-Key", "access-key");
        // headers.Should().Contain("X-Api-App-Key", "app-key");
        // headers.Should().Contain("X-Api-Access-Key", "access-key");
        headers.Should().Contain("X-Api-Resource-Id", VolcengineStreamingAsrOptions.BigAsr1DurationResourceId);
        headers.Should().Contain("X-Api-Connect-Id", "connect-id");
    }

    [Fact]
    public void Streaming_asr_protocol_codec_encodes_requests_and_decodes_server_response()
    {
        var codec = new VolcengineStreamingAsrProtocolCodec();

        var fullRequest = codec.EncodeFullClientRequest(new VolcengineStreamingAsrStartRequest(
            Language: "zh-CN"));
        var audioRequest = codec.EncodeAudioOnlyRequest([0x01, 0x02], isFinal: false);
        var finalAudioRequest = codec.EncodeAudioOnlyRequest([], isFinal: true);

        fullRequest.Span[0].Should().Be(0x11);
        fullRequest.Span[1].Should().Be(0x10);
        fullRequest.Span[2].Should().Be(0x11);
        audioRequest.Span[1].Should().Be(0x20);
        finalAudioRequest.Span[1].Should().Be(0x22);

        var decoded = codec.DecodeServerResponse(CreateServerResponsePayload(
            """
            {
              "result": {
                "text": "hello",
                "utterances": [
                  { "text": "hello", "start_time": 10, "end_time": 260, "definite": true }
                ]
              }
            }
            """,
            sequence: 7,
            final: true));

        decoded.Sequence.Should().Be(7);
        decoded.IsFinal.Should().BeTrue();
        decoded.Text.Should().Be("hello");
        decoded.Utterances.Should().ContainSingle().Which.Definite.Should().BeTrue();
    }

    [Fact]
    public void Streaming_asr_protocol_codec_decodes_server_response_without_sequence()
    {
        var codec = new VolcengineStreamingAsrProtocolCodec();

        var decoded = codec.DecodeServerResponse(CreateServerResponsePayload(
            """
            {
              "result": {
                "text": "hello without sequence"
              }
            }
            """,
            sequence: 0,
            final: false,
            includeSequence: false));

        decoded.Sequence.Should().Be(0);
        decoded.IsFinal.Should().BeFalse();
        decoded.Text.Should().Be("hello without sequence");
    }

    [Fact]
    public void Streaming_asr_protocol_codec_marks_no_sequence_final_response()
    {
        var codec = new VolcengineStreamingAsrProtocolCodec();

        var decoded = codec.DecodeServerResponse(CreateServerResponsePayload(
            """
            {
              "result": {
                "text": ""
              }
            }
            """,
            sequence: 0,
            final: true,
            includeSequence: false));

        decoded.Sequence.Should().Be(0);
        decoded.IsFinal.Should().BeTrue();
    }

    [Fact]
    public void Streaming_asr_protocol_codec_decodes_error_response()
    {
        var codec = new VolcengineStreamingAsrProtocolCodec();

        var decoded = codec.DecodeServerResponse(CreateErrorResponsePayload(
            4001001,
            """{"message":"bad request"}"""));

        decoded.IsFinal.Should().BeTrue();
        decoded.ErrorCode.Should().Be(4001001);
        decoded.ErrorMessage.Should().Be("""{"message":"bad request"}""");
    }

    [Fact]
    public async Task Streaming_asr_provider_emits_source_only_segments_from_bigmodel_async()
    {
        var transport = new RecordingStreamingAsrWebSocketTransport(
            CreateServerResponsePayload(
                """
                {
                  "result": {
                    "text": "enemy on the left",
                    "utterances": [
                      { "text": "enemy on the left", "start_time": 100, "end_time": 900, "definite": true }
                    ]
                  }
                }
                """,
                sequence: 3,
                final: true),
            null);
        var provider = new VolcengineStreamingAsrProvider(
            new VolcengineStreamingAsrOptions("app-key", "access-key"),
            new VolcengineStreamingAsrProtocolCodec(),
            () => transport);

        await using var session = await provider.StartSessionAsync(
            AudioChannelId.Monitor,
            new SpeechRecognitionSessionOptions("en", "cn-north-1"),
            CancellationToken.None);

        var segments = new List<SpeechRecognitionSegment>();
        await foreach (var segment in session.ReadSegmentsAsync(CancellationToken.None))
        {
            segments.Add(segment);
        }

        transport.ConnectedUri.Should().Be(VolcengineStreamingAsrOptions.OptimizedBidirectionalEndpoint);
        transport.SentPayloads.Should().ContainSingle();
        segments.Should().ContainSingle();
        segments[0].ChannelId.Should().Be(AudioChannelId.Monitor);
        segments[0].Text.Should().Be("enemy on the left");
        segments[0].Stability.Should().Be(SegmentStability.Final);
    }

    private static byte[] CreateServerResponsePayload(
        string json,
        int sequence,
        bool final,
        bool includeSequence = true)
    {
        var payload = Compress(Encoding.UTF8.GetBytes(json));
        var flags = includeSequence
            ? final ? 0x03 : 0x01
            : final ? 0x02 : 0x00;
        var bytes = new List<byte>
        {
            0x11,
            (byte)(0x90 | flags),
            0x11,
            0x00
        };
        if (includeSequence)
        {
            bytes.AddRange(ToBigEndianBytes(sequence));
        }

        bytes.AddRange(ToBigEndianBytes(payload.Length));
        bytes.AddRange(payload);
        return bytes.ToArray();
    }

    private static byte[] CreateErrorResponsePayload(int code, string message)
    {
        var payload = Encoding.UTF8.GetBytes(message);
        var bytes = new List<byte>
        {
            0x11,
            0xf0,
            0x10,
            0x00
        };
        bytes.AddRange(ToBigEndianBytes(code));
        bytes.AddRange(ToBigEndianBytes(payload.Length));
        bytes.AddRange(payload);
        return bytes.ToArray();
    }

    private static byte[] ToBigEndianBytes(int value)
    {
        var bytes = BitConverter.GetBytes(value);
        if (BitConverter.IsLittleEndian)
        {
            Array.Reverse(bytes);
        }

        return bytes;
    }

    private static byte[] Compress(byte[] input)
    {
        using var output = new MemoryStream();
        using (var gzip = new GZipStream(output, CompressionLevel.SmallestSize, leaveOpen: true))
        {
            gzip.Write(input);
        }

        return output.ToArray();
    }

    private sealed class RecordingStreamingAsrWebSocketTransport : IStreamingAsrWebSocketTransport
    {
        private readonly Queue<byte[]?> _responses;

        public RecordingStreamingAsrWebSocketTransport(params byte[]?[] responses)
        {
            _responses = new Queue<byte[]?>(responses);
        }

        public Uri? ConnectedUri { get; private set; }

        public IReadOnlyDictionary<string, string> Headers { get; private set; } =
            new Dictionary<string, string>();

        public List<byte[]> SentPayloads { get; } = [];

        public Task ConnectAsync(Uri endpoint, IReadOnlyDictionary<string, string> headers, CancellationToken cancellationToken)
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
            return ValueTask.FromResult(_responses.Count == 0 ? null : _responses.Dequeue());
        }

        public ValueTask DisposeAsync()
        {
            return ValueTask.CompletedTask;
        }
    }
}
