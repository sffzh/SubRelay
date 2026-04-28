using System.Buffers.Binary;
using System.IO.Compression;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;

namespace GameSubRelay.Infrastructure.Translation.Volcengine;

public sealed record VolcengineStreamingAsrStartRequest(string? Language = null);

public sealed record VolcengineStreamingAsrUtterance(
    string Text,
    int StartTimeMs,
    int EndTimeMs,
    bool Definite);

public sealed record VolcengineStreamingAsrServerMessage(
    int Sequence,
    bool IsFinal,
    string Text,
    IReadOnlyList<VolcengineStreamingAsrUtterance> Utterances,
    int? ErrorCode = null,
    string? ErrorMessage = null);

public interface IStreamingAsrWebSocketTransport : IAsyncDisposable
{
    Task ConnectAsync(
        Uri endpoint,
        IReadOnlyDictionary<string, string> headers,
        CancellationToken cancellationToken);

    ValueTask SendAsync(ReadOnlyMemory<byte> payload, CancellationToken cancellationToken);

    ValueTask<byte[]?> ReceiveAsync(CancellationToken cancellationToken);
}

public sealed class ClientWebSocketStreamingAsrTransport : IStreamingAsrWebSocketTransport
{
    private readonly ClientWebSocket _webSocket;

    public ClientWebSocketStreamingAsrTransport()
        : this(new ClientWebSocket())
    {
    }

    public ClientWebSocketStreamingAsrTransport(ClientWebSocket webSocket)
    {
        _webSocket = webSocket;
    }

    public async Task ConnectAsync(
        Uri endpoint,
        IReadOnlyDictionary<string, string> headers,
        CancellationToken cancellationToken)
    {
        foreach (var header in headers)
        {
            _webSocket.Options.SetRequestHeader(header.Key, header.Value);
        }

        await _webSocket.ConnectAsync(endpoint, cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask SendAsync(ReadOnlyMemory<byte> payload, CancellationToken cancellationToken)
    {
        await _webSocket
            .SendAsync(payload, WebSocketMessageType.Binary, endOfMessage: true, cancellationToken)
            .ConfigureAwait(false);
    }

    public async ValueTask<byte[]?> ReceiveAsync(CancellationToken cancellationToken)
    {
        var buffer = new byte[16 * 1024];
        using var stream = new MemoryStream();

        while (true)
        {
            var result = await _webSocket.ReceiveAsync(buffer, cancellationToken).ConfigureAwait(false);
            if (result.MessageType == WebSocketMessageType.Close)
            {
                return null;
            }

            if (result.MessageType != WebSocketMessageType.Binary)
            {
                throw new InvalidOperationException(
                    $"Volcengine streaming ASR returned unsupported WebSocket message type {result.MessageType}.");
            }

            stream.Write(buffer, 0, result.Count);
            if (result.EndOfMessage)
            {
                return stream.ToArray();
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_webSocket.State is WebSocketState.Open or WebSocketState.CloseReceived)
        {
            try
            {
                await _webSocket
                    .CloseAsync(WebSocketCloseStatus.NormalClosure, "disposing", CancellationToken.None)
                    .ConfigureAwait(false);
            }
            catch (WebSocketException)
            {
                // Closing during network teardown is best-effort.
            }
        }

        _webSocket.Dispose();
    }
}

public sealed class VolcengineStreamingAsrProtocolCodec
{
    private const byte ProtocolVersionAndHeaderSize = 0x11;
    private const byte FullClientRequest = 0x10;
    private const byte AudioOnlyRequest = 0x20;
    private const byte FinalAudioOnlyRequest = 0x22;
    private const byte JsonGzip = 0x11;
    private const byte RawGzip = 0x01;
    private const byte Reserved = 0x00;
    private const int MessageTypeFullServerResponse = 0x09;
    private const int MessageTypeError = 0x0f;

    public ReadOnlyMemory<byte> EncodeFullClientRequest(VolcengineStreamingAsrStartRequest request)
    {
        var audio = new Dictionary<string, object?>
        {
            ["format"] = "pcm",
            ["codec"] = "raw",
            ["rate"] = 16_000,
            ["bits"] = 16,
            ["channel"] = 1
        };

        if (!string.IsNullOrWhiteSpace(request.Language))
        {
            audio["language"] = request.Language;
        }

        var root = new Dictionary<string, object?>
        {
            ["user"] = new Dictionary<string, object?>
            {
                ["uid"] = "game-sub-relay"
            },
            ["audio"] = audio,
            ["request"] = new Dictionary<string, object?>
            {
                ["model_name"] = "bigmodel",
                ["enable_punc"] = true,
                ["enable_itn"] = true,
                ["show_utterances"] = true,
                ["enable_lid"] = true,
                ["show_language"] = true,
                ["enable_nonstream"] = true,
                ["result_type"] = "full"
            }
        };

        var json = JsonSerializer.SerializeToUtf8Bytes(root);
        return BuildFrame(FullClientRequest, JsonGzip, Compress(json), includeSequence: false);
    }

    public ReadOnlyMemory<byte> EncodeAudioOnlyRequest(byte[] pcm16Mono16Khz, bool isFinal)
    {
        return BuildFrame(
            isFinal ? FinalAudioOnlyRequest : AudioOnlyRequest,
            RawGzip,
            Compress(pcm16Mono16Khz),
            includeSequence: false);
    }

    public VolcengineStreamingAsrServerMessage DecodeServerResponse(ReadOnlyMemory<byte> payload)
    {
        var span = payload.Span;
        if (span.Length < 8)
        {
            throw new InvalidOperationException("Volcengine streaming ASR response is too short.");
        }

        var headerSize = (span[0] & 0x0f) * 4;
        var messageType = span[1] >> 4;
        var flags = span[1] & 0x0f;
        var compression = span[2] & 0x0f;
        var offset = headerSize;

        if (messageType == MessageTypeError)
        {
            var errorCode = ReadInt32(span, ref offset);
            var errorPayload = ReadPayload(span, ref offset);
            var errorText = Encoding.UTF8.GetString(errorPayload);
            return new VolcengineStreamingAsrServerMessage(
                Sequence: 0,
                IsFinal: true,
                Text: string.Empty,
                Utterances: [],
                ErrorCode: errorCode,
                ErrorMessage: errorText);
        }

        if (messageType != MessageTypeFullServerResponse)
        {
            throw new InvalidOperationException($"Unsupported Volcengine streaming ASR message type {messageType}.");
        }

        var sequence = ReadInt32(span, ref offset);
        var responsePayload = ReadPayload(span, ref offset);
        if (compression == 1)
        {
            responsePayload = Decompress(responsePayload);
        }

        return ParseResponseJson(sequence, flags == 0x03, responsePayload);
    }

    private static VolcengineStreamingAsrServerMessage ParseResponseJson(
        int sequence,
        bool isFinal,
        byte[] payload)
    {
        using var document = JsonDocument.Parse(payload);
        var root = document.RootElement;
        if (!root.TryGetProperty("result", out var result))
        {
            return new VolcengineStreamingAsrServerMessage(sequence, isFinal, string.Empty, []);
        }

        if (result.ValueKind == JsonValueKind.Array)
        {
            result = result.EnumerateArray().LastOrDefault();
        }

        var text = GetString(result, "text");
        var utterances = new List<VolcengineStreamingAsrUtterance>();
        if (result.ValueKind == JsonValueKind.Object &&
            result.TryGetProperty("utterances", out var utteranceArray) &&
            utteranceArray.ValueKind == JsonValueKind.Array)
        {
            foreach (var utterance in utteranceArray.EnumerateArray())
            {
                var utteranceText = GetString(utterance, "text");
                if (string.IsNullOrWhiteSpace(utteranceText))
                {
                    continue;
                }

                utterances.Add(new VolcengineStreamingAsrUtterance(
                    utteranceText,
                    GetInt32(utterance, "start_time"),
                    GetInt32(utterance, "end_time"),
                    GetBoolean(utterance, "definite")));
            }
        }

        return new VolcengineStreamingAsrServerMessage(sequence, isFinal, text, utterances);
    }

    private static ReadOnlyMemory<byte> BuildFrame(
        byte messageTypeAndFlags,
        byte serializationAndCompression,
        byte[] payload,
        bool includeSequence)
    {
        var size = includeSequence ? 12 + payload.Length : 8 + payload.Length;
        var frame = new byte[size];
        frame[0] = ProtocolVersionAndHeaderSize;
        frame[1] = messageTypeAndFlags;
        frame[2] = serializationAndCompression;
        frame[3] = Reserved;

        var offset = 4;
        if (includeSequence)
        {
            BinaryPrimitives.WriteInt32BigEndian(frame.AsSpan(offset, 4), 1);
            offset += 4;
        }

        BinaryPrimitives.WriteInt32BigEndian(frame.AsSpan(offset, 4), payload.Length);
        offset += 4;
        payload.CopyTo(frame.AsSpan(offset));
        return frame;
    }

    private static byte[] ReadPayload(ReadOnlySpan<byte> span, ref int offset)
    {
        var payloadSize = ReadInt32(span, ref offset);
        if (payloadSize < 0 || offset + payloadSize > span.Length)
        {
            throw new InvalidOperationException("Volcengine streaming ASR response has invalid payload size.");
        }

        var payload = span.Slice(offset, payloadSize).ToArray();
        offset += payloadSize;
        return payload;
    }

    private static int ReadInt32(ReadOnlySpan<byte> span, ref int offset)
    {
        if (offset + 4 > span.Length)
        {
            throw new InvalidOperationException("Volcengine streaming ASR response ended unexpectedly.");
        }

        var value = BinaryPrimitives.ReadInt32BigEndian(span.Slice(offset, 4));
        offset += 4;
        return value;
    }

    private static byte[] Compress(byte[] input)
    {
        using var output = new MemoryStream();
        using (var gzip = new GZipStream(output, CompressionLevel.Fastest, leaveOpen: true))
        {
            gzip.Write(input);
        }

        return output.ToArray();
    }

    private static byte[] Decompress(byte[] input)
    {
        using var compressed = new MemoryStream(input);
        using var gzip = new GZipStream(compressed, CompressionMode.Decompress);
        using var output = new MemoryStream();
        gzip.CopyTo(output);
        return output.ToArray();
    }

    private static string GetString(JsonElement element, string propertyName)
    {
        return element.ValueKind == JsonValueKind.Object &&
            element.TryGetProperty(propertyName, out var property) &&
            property.ValueKind == JsonValueKind.String
            ? property.GetString() ?? string.Empty
            : string.Empty;
    }

    private static int GetInt32(JsonElement element, string propertyName)
    {
        return element.ValueKind == JsonValueKind.Object &&
            element.TryGetProperty(propertyName, out var property) &&
            property.TryGetInt32(out var value)
            ? value
            : 0;
    }

    private static bool GetBoolean(JsonElement element, string propertyName)
    {
        return element.ValueKind == JsonValueKind.Object &&
            element.TryGetProperty(propertyName, out var property) &&
            property.ValueKind == JsonValueKind.True;
    }
}
