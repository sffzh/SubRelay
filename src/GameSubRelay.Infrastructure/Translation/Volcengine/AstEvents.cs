using System.Collections.Generic;

namespace GameSubRelay.Infrastructure.Translation.Volcengine;

public enum AstClientEventType
{
    StartSession = 100,
    FinishSession = 102,
    TaskRequest = 200,
    UpdateConfig = 201
}

public enum AstServerEventType
{
    SessionStarted = 150,
    SessionFinished = 152,
    SessionFailed = 153,
    UsageResponse = 154,
    AudioMuted = 250,
    TTSSentenceStart = 350,
    TTSSentenceEnd = 351,
    TTSResponse = 352,
    SourceSubtitleStart = 650,
    SourceSubtitleResponse = 651,
    SourceSubtitleEnd = 652,
    TranslationSubtitleStart = 653,
    TranslationSubtitleResponse = 654,
    TranslationSubtitleEnd = 655
}

public sealed record AstSessionConfig(
    string SourceLanguage,
    string TargetLanguage,
    string Mode = "s2t",
    IReadOnlyList<string>? HotWords = null,
    AstAudioConfig? SourceAudio = null,
    AstAudioConfig? TargetAudio = null);

public sealed record AstAudioConfig(
    string Format,
    int Rate,
    int Bits = 16,
    int Channel = 1,
    string Codec = "raw")
{
    public static AstAudioConfig Pcm16Mono16Khz { get; } = new("pcm", 16_000);
}

public sealed record AstRequestMeta(
    string Endpoint,
    string AppKey,
    string ResourceId,
    string ConnectionId,
    string SessionId,
    int Sequence);

public sealed record AstClientMessage(
    AstClientEventType Event,
    AstRequestMeta? RequestMeta = null,
    AstSessionConfig? SessionConfig = null,
    byte[]? AudioData = null)
{
    public static AstClientMessage StartSession(AstSessionConfig config, AstRequestMeta? requestMeta = null)
    {
        return new AstClientMessage(AstClientEventType.StartSession, requestMeta, SessionConfig: config);
    }

    public static AstClientMessage TaskRequest(byte[] audioData, AstRequestMeta? requestMeta = null)
    {
        return new AstClientMessage(
            AstClientEventType.TaskRequest,
            requestMeta,
            AudioData: audioData.ToArray());
    }

    public static AstClientMessage FinishSession(AstRequestMeta? requestMeta = null)
    {
        return new AstClientMessage(AstClientEventType.FinishSession, requestMeta);
    }
}

public sealed record AstResponseMeta(
    int StatusCode,
    string Message,
    string? SessionId = null);

public sealed record AstServerMessage(
    AstServerEventType Event,
    string? Text = null,
    int? StartTimeMs = null,
    int? EndTimeMs = null,
    bool SpeakerChanged = false,
    byte[]? Data = null,
    int? MutedDurationMs = null,
    AstResponseMeta? ResponseMeta = null)
{
    public static AstServerMessage SourceStart(int startTimeMs, bool speakerChanged = false)
    {
        return new AstServerMessage(
            AstServerEventType.SourceSubtitleStart,
            StartTimeMs: startTimeMs,
            SpeakerChanged: speakerChanged);
    }

    public static AstServerMessage SourceResponse(string text)
    {
        return new AstServerMessage(AstServerEventType.SourceSubtitleResponse, Text: text);
    }

    public static AstServerMessage SourceEnd(string text, int startTimeMs, int endTimeMs)
    {
        return new AstServerMessage(
            AstServerEventType.SourceSubtitleEnd,
            Text: text,
            StartTimeMs: startTimeMs,
            EndTimeMs: endTimeMs);
    }

    public static AstServerMessage TranslationStart(int startTimeMs, bool speakerChanged = false)
    {
        return new AstServerMessage(
            AstServerEventType.TranslationSubtitleStart,
            StartTimeMs: startTimeMs,
            SpeakerChanged: speakerChanged);
    }

    public static AstServerMessage TranslationResponse(string text)
    {
        return new AstServerMessage(AstServerEventType.TranslationSubtitleResponse, Text: text);
    }

    public static AstServerMessage TranslationEnd(string text, int startTimeMs, int endTimeMs)
    {
        return new AstServerMessage(
            AstServerEventType.TranslationSubtitleEnd,
            Text: text,
            StartTimeMs: startTimeMs,
            EndTimeMs: endTimeMs);
    }

    public static AstServerMessage SessionFailed(int statusCode, string message)
    {
        return new AstServerMessage(
            AstServerEventType.SessionFailed,
            ResponseMeta: new AstResponseMeta(statusCode, message));
    }
}
