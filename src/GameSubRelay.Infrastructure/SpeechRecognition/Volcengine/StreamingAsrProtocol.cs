using System.Buffers.Binary;
using System.Globalization;
using System.IO.Compression;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using GameSubRelay.Infrastructure.Volcengine;
using Microsoft.Extensions.Logging;

namespace GameSubRelay.Infrastructure.SpeechRecognition.Volcengine;

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
    private readonly ILogger<ClientWebSocketStreamingAsrTransport>? _logger;

    public ClientWebSocketStreamingAsrTransport()
        : this(new ClientWebSocket())
    {
    }

    public ClientWebSocketStreamingAsrTransport(ILogger<ClientWebSocketStreamingAsrTransport>? logger)
        : this(new ClientWebSocket(), logger)
    {
    }

    public ClientWebSocketStreamingAsrTransport(
        ClientWebSocket webSocket,
        ILogger<ClientWebSocketStreamingAsrTransport>? logger = null)
    {
        _webSocket = webSocket;
        _logger = logger;
    }

    public async Task ConnectAsync(
        Uri endpoint,
        IReadOnlyDictionary<string, string> headers,
        CancellationToken cancellationToken)
    {
        _logger?.LogInformation(
            "Streaming ASR WebSocket connecting to {Endpoint}; headers: {Headers}",
            endpoint,
            VolcengineLogFormatter.FormatHeaders(headers));

        foreach (var header in headers)
        {
            _webSocket.Options.SetRequestHeader(header.Key, header.Value);
        }

        try
        {
            await _webSocket.ConnectAsync(endpoint, cancellationToken).ConfigureAwait(false);
            _logger?.LogInformation(
                "Streaming ASR WebSocket connected to {Endpoint}; state={State}",
                endpoint,
                _webSocket.State);
        }
        catch (Exception ex)
        {
            _logger?.LogError(
                ex,
                "Streaming ASR WebSocket handshake failed for {Endpoint}; hint={Hint}; headers: {Headers}",
                endpoint,
                VolcengineLogFormatter.FormatHandshakeFailureHint(ex),
                VolcengineLogFormatter.FormatHeaders(headers));
            throw;
        }
    }

    public async ValueTask SendAsync(ReadOnlyMemory<byte> payload, CancellationToken cancellationToken)
    {
        _logger?.LogDebug("Streaming ASR WebSocket sending {PayloadBytes} bytes.", payload.Length);
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
                _logger?.LogInformation(
                    "Streaming ASR WebSocket close received: status={CloseStatus}, description={CloseDescription}.",
                    result.CloseStatus,
                    result.CloseStatusDescription);
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
                var payload = stream.ToArray();
                _logger?.LogDebug("Streaming ASR WebSocket received {PayloadBytes} bytes.", payload.Length);
                return payload;
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
        _logger?.LogInformation("Streaming ASR WebSocket disposed.");
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
    private const int CompressionGzip = 0x01;

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
        if (span.Length < 4)
        {
            throw new InvalidOperationException("Volcengine streaming ASR response is too short.");
        }

        var headerSize = (span[0] & 0x0f) * 4;
        var messageType = span[1] >> 4;
        var flags = span[1] & 0x0f;
        var compression = span[2] & 0x0f;
        if (headerSize < 4 || headerSize > span.Length)
        {
            throw new InvalidOperationException(
                $"Volcengine streaming ASR response has invalid header size. {DescribeServerResponseFrame(payload)}");
        }

        var offset = headerSize;

        if (messageType == MessageTypeError)
        {
            var errorCode = ReadInt32(span, ref offset);
            var errorPayload = ReadPayload(span, ref offset, payload.Length);
            if (compression == CompressionGzip)
            {
                errorPayload = Decompress(errorPayload);
            }

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

        var sequence = HasSequence(flags) ? ReadInt32(span, ref offset) : 0;
        var responsePayload = ReadPayload(span, ref offset, payload.Length);
        if (compression == CompressionGzip)
        {
            responsePayload = Decompress(responsePayload);
        }

        return ParseResponseJson(sequence, IsFinalResponse(flags, sequence), responsePayload);
    }

    public static string DescribeServerResponseFrame(ReadOnlyMemory<byte> payload)
    {
        var span = payload.Span;
        if (span.Length < 4)
        {
            return $"bytes={span.Length}, prefix={FormatHexPrefix(span)}";
        }

        var headerSize = (span[0] & 0x0f) * 4;
        var messageType = span[1] >> 4;
        var flags = span[1] & 0x0f;
        var serialization = span[2] >> 4;
        var compression = span[2] & 0x0f;
        return string.Create(
            CultureInfo.InvariantCulture,
            $"bytes={span.Length}, headerSize={headerSize}, messageType=0x{messageType:X}, flags=0x{flags:X}, serialization=0x{serialization:X}, compression=0x{compression:X}, prefix={FormatHexPrefix(span)}");
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

    private static byte[] ReadPayload(ReadOnlySpan<byte> span, ref int offset, int frameLength)
    {
        var payloadSize = ReadInt32(span, ref offset);
        if (payloadSize < 0 || offset + payloadSize > span.Length)
        {
            throw new InvalidOperationException(
                $"Volcengine streaming ASR response has invalid payload size. frameBytes={frameLength}, offset={offset}, payloadSize={payloadSize}");
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

    private static bool HasSequence(int flags)
    {
        return flags is 0x01 or 0x03;
    }

    private static bool IsFinalResponse(int flags, int sequence)
    {
        return (flags is 0x02 or 0x03) || sequence < 0;
    }

    private static string FormatHexPrefix(ReadOnlySpan<byte> span)
    {
        var length = Math.Min(span.Length, 16);
        if (length == 0)
        {
            return "<empty>";
        }

        var builder = new StringBuilder(length * 3);
        for (var index = 0; index < length; index++)
        {
            if (index > 0)
            {
                builder.Append(' ');
            }

            builder.Append(span[index].ToString("X2", CultureInfo.InvariantCulture));
        }

        return builder.ToString();
    }
}
