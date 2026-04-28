using System;
using System.Collections.Generic;
using System.Text;

namespace GameSubRelay.Infrastructure.Translation.Volcengine;

public interface IAstProtocolCodec
{
    ReadOnlyMemory<byte> Encode(AstClientMessage message);

    AstServerMessage Decode(ReadOnlyMemory<byte> payload);
}

public sealed class VolcengineAstProtobufProtocolCodec : IAstProtocolCodec
{
    public ReadOnlyMemory<byte> Encode(AstClientMessage message)
    {
        if (message is null)
        {
            throw new ArgumentNullException(nameof(message));
        }

        var writer = new ProtobufWriter();

        if (message.RequestMeta is not null)
        {
            writer.WriteMessage(1, EncodeRequestMeta(message.RequestMeta));
        }

        writer.WriteVarintField(2, (ulong)message.Event);

        if (message.Event == AstClientEventType.StartSession)
        {
            if (message.SessionConfig is null)
            {
                throw new InvalidOperationException("AST StartSession requires session config.");
            }

            writer.WriteMessage(3, EncodeUser());
            writer.WriteMessage(4, EncodeAudio(message.SessionConfig.SourceAudio ?? AstAudioConfig.Pcm16Mono16Khz, null));

            if (string.Equals(message.SessionConfig.Mode, "s2s", StringComparison.OrdinalIgnoreCase))
            {
                writer.WriteMessage(5, EncodeAudio(message.SessionConfig.TargetAudio ?? AstAudioConfig.Pcm16Mono16Khz, null));
            }

            writer.WriteMessage(6, EncodeRequestParams(message.SessionConfig));
        }
        else if (message.Event == AstClientEventType.TaskRequest)
        {
            if (message.AudioData is null)
            {
                throw new InvalidOperationException("AST TaskRequest requires audio data.");
            }

            writer.WriteMessage(4, EncodeAudio(AstAudioConfig.Pcm16Mono16Khz, message.AudioData));
        }

        return writer.ToArray();
    }

    public AstServerMessage Decode(ReadOnlyMemory<byte> payload)
    {
        var reader = new ProtobufReader(payload.Span);
        AstServerEventType eventType = AstServerEventType.SessionFailed;
        string? text = null;
        byte[]? data = null;
        int? startTimeMs = null;
        int? endTimeMs = null;
        bool speakerChanged = false;
        int? mutedDurationMs = null;
        AstResponseMeta? responseMeta = null;

        while (reader.TryReadField(out var fieldNumber, out var wireType))
        {
            switch (fieldNumber)
            {
                case 1:
                    responseMeta = DecodeResponseMeta(reader.ReadLengthDelimited(wireType));
                    break;
                case 2:
                    eventType = (AstServerEventType)reader.ReadVarint(wireType);
                    break;
                case 3:
                    data = reader.ReadLengthDelimited(wireType).ToArray();
                    break;
                case 4:
                    text = Encoding.UTF8.GetString(reader.ReadLengthDelimited(wireType));
                    break;
                case 5:
                    startTimeMs = checked((int)reader.ReadVarint(wireType));
                    break;
                case 6:
                    endTimeMs = checked((int)reader.ReadVarint(wireType));
                    break;
                case 7:
                    speakerChanged = reader.ReadVarint(wireType) != 0;
                    break;
                case 8:
                    mutedDurationMs = checked((int)reader.ReadVarint(wireType));
                    break;
                default:
                    reader.Skip(wireType);
                    break;
            }
        }

        return new AstServerMessage(
            eventType,
            text,
            startTimeMs,
            endTimeMs,
            speakerChanged,
            data,
            mutedDurationMs,
            responseMeta);
    }

    private static byte[] EncodeRequestMeta(AstRequestMeta meta)
    {
        var writer = new ProtobufWriter();
        writer.WriteStringField(1, meta.Endpoint);
        writer.WriteStringField(2, meta.AppKey);
        writer.WriteStringField(4, meta.ResourceId);
        writer.WriteStringField(5, meta.ConnectionId);
        writer.WriteStringField(6, meta.SessionId);
        writer.WriteVarintField(7, (ulong)meta.Sequence);
        return writer.ToArray();
    }

    private static byte[] EncodeUser()
    {
        var writer = new ProtobufWriter();
        writer.WriteStringField(1, "game-sub-relay");
        writer.WriteStringField(3, "windows");
        return writer.ToArray();
    }

    private static byte[] EncodeAudio(AstAudioConfig config, byte[]? binaryData)
    {
        var writer = new ProtobufWriter();
        writer.WriteStringField(4, config.Format);
        writer.WriteStringField(5, config.Codec);
        writer.WriteVarintField(7, (ulong)config.Rate);
        writer.WriteVarintField(8, (ulong)config.Bits);
        writer.WriteVarintField(9, (ulong)config.Channel);

        if (binaryData is { Length: > 0 })
        {
            writer.WriteBytesField(14, binaryData);
        }

        return writer.ToArray();
    }

    private static byte[] EncodeRequestParams(AstSessionConfig config)
    {
        var writer = new ProtobufWriter();
        writer.WriteStringField(1, config.Mode);
        writer.WriteStringField(2, config.SourceLanguage);
        writer.WriteStringField(3, config.TargetLanguage);

        if (config.HotWords is { Count: > 0 })
        {
            writer.WriteMessage(100, EncodeCorpus(config.HotWords));
        }

        return writer.ToArray();
    }

    private static byte[] EncodeCorpus(IReadOnlyList<string> hotWords)
    {
        var writer = new ProtobufWriter();
        foreach (var hotWord in hotWords)
        {
            if (!string.IsNullOrWhiteSpace(hotWord))
            {
                writer.WriteStringField(9, hotWord.Trim());
            }
        }

        return writer.ToArray();
    }

    private static AstResponseMeta DecodeResponseMeta(ReadOnlySpan<byte> payload)
    {
        var reader = new ProtobufReader(payload);
        var sessionId = string.Empty;
        var statusCode = 0;
        var message = string.Empty;

        while (reader.TryReadField(out var fieldNumber, out var wireType))
        {
            switch (fieldNumber)
            {
                case 1:
                    sessionId = Encoding.UTF8.GetString(reader.ReadLengthDelimited(wireType));
                    break;
                case 3:
                    statusCode = checked((int)reader.ReadVarint(wireType));
                    break;
                case 4:
                    message = Encoding.UTF8.GetString(reader.ReadLengthDelimited(wireType));
                    break;
                default:
                    reader.Skip(wireType);
                    break;
            }
        }

        return new AstResponseMeta(statusCode, message, string.IsNullOrWhiteSpace(sessionId) ? null : sessionId);
    }

    private sealed class ProtobufWriter
    {
        private readonly List<byte> _buffer = [];

        public void WriteStringField(int fieldNumber, string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return;
            }

            WriteBytesField(fieldNumber, Encoding.UTF8.GetBytes(value));
        }

        public void WriteBytesField(int fieldNumber, byte[] value)
        {
            WriteTag(fieldNumber, wireType: 2);
            WriteVarint((ulong)value.Length);
            _buffer.AddRange(value);
        }

        public void WriteMessage(int fieldNumber, byte[] value)
        {
            WriteBytesField(fieldNumber, value);
        }

        public void WriteVarintField(int fieldNumber, ulong value)
        {
            WriteTag(fieldNumber, wireType: 0);
            WriteVarint(value);
        }

        public byte[] ToArray()
        {
            return _buffer.ToArray();
        }

        private void WriteTag(int fieldNumber, int wireType)
        {
            WriteVarint(((ulong)fieldNumber << 3) | (uint)wireType);
        }

        private void WriteVarint(ulong value)
        {
            while (value >= 0x80)
            {
                _buffer.Add((byte)(value | 0x80));
                value >>= 7;
            }

            _buffer.Add((byte)value);
        }
    }

    private ref struct ProtobufReader
    {
        private readonly ReadOnlySpan<byte> _payload;
        private int _offset;

        public ProtobufReader(ReadOnlySpan<byte> payload)
        {
            _payload = payload;
            _offset = 0;
        }

        public bool TryReadField(out int fieldNumber, out int wireType)
        {
            if (_offset >= _payload.Length)
            {
                fieldNumber = 0;
                wireType = 0;
                return false;
            }

            var tag = ReadRawVarint();
            fieldNumber = (int)(tag >> 3);
            wireType = (int)(tag & 0x07);
            return true;
        }

        public ulong ReadVarint(int wireType)
        {
            EnsureWireType(wireType, expected: 0);
            return ReadRawVarint();
        }

        public ReadOnlySpan<byte> ReadLengthDelimited(int wireType)
        {
            EnsureWireType(wireType, expected: 2);
            var length = checked((int)ReadRawVarint());
            if (length < 0 || _offset + length > _payload.Length)
            {
                throw VolcengineAstProviderException.Protocol("AST protobuf payload has invalid length-delimited field.");
            }

            var value = _payload.Slice(_offset, length);
            _offset += length;
            return value;
        }

        public void Skip(int wireType)
        {
            switch (wireType)
            {
                case 0:
                    ReadRawVarint();
                    break;
                case 1:
                    _offset += 8;
                    break;
                case 2:
                    ReadLengthDelimited(wireType);
                    break;
                case 5:
                    _offset += 4;
                    break;
                default:
                    throw VolcengineAstProviderException.Protocol($"AST protobuf payload has unsupported wire type {wireType}.");
            }

            if (_offset > _payload.Length)
            {
                throw VolcengineAstProviderException.Protocol("AST protobuf payload ended unexpectedly.");
            }
        }

        private ulong ReadRawVarint()
        {
            ulong result = 0;
            var shift = 0;
            while (shift <= 63)
            {
                if (_offset >= _payload.Length)
                {
                    throw VolcengineAstProviderException.Protocol("AST protobuf varint ended unexpectedly.");
                }

                var current = _payload[_offset++];
                result |= (ulong)(current & 0x7f) << shift;
                if ((current & 0x80) == 0)
                {
                    return result;
                }

                shift += 7;
            }

            throw VolcengineAstProviderException.Protocol("AST protobuf varint is too large.");
        }

        private static void EnsureWireType(int actual, int expected)
        {
            if (actual != expected)
            {
                throw VolcengineAstProviderException.Protocol(
                    $"AST protobuf wire type mismatch. Expected {expected}, got {actual}.");
            }
        }
    }
}
