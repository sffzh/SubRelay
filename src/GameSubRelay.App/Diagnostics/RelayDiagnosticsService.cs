using GameSubRelay.App.ViewModels;
using GameSubRelay.Core.Audio;
using GameSubRelay.Core.SpeechRecognition;
using GameSubRelay.Core.Translation;
using GameSubRelay.Infrastructure.Audio;
using GameSubRelay.Infrastructure.SpeechRecognition.Volcengine;
using GameSubRelay.Infrastructure.Translation.Volcengine;
using Microsoft.Extensions.Logging;

namespace GameSubRelay.App.Diagnostics;

public sealed class RelayDiagnosticsService : IRelayDiagnosticsService
{
    private static readonly TimeSpan ConnectionTimeout = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan CaptureDuration = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan ResponseTimeout = TimeSpan.FromSeconds(15);

    private readonly INaudioDeviceService _deviceService;
    private readonly ILoggerFactory _loggerFactory;

    public RelayDiagnosticsService(
        INaudioDeviceService deviceService,
        ILoggerFactory loggerFactory)
    {
        _deviceService = deviceService ?? throw new ArgumentNullException(nameof(deviceService));
        _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
    }

    public async Task<string> TestTranslationConnectionAsync(
        TranslationSettingsViewModel translation,
        CancellationToken cancellationToken = default)
    {
        var snapshot = TranslationSnapshot.From(translation);
        ValidateCredentials(snapshot.AppKey, snapshot.AccessKey, "同声传译");

        using var timeout = CreateTimeout(cancellationToken, ConnectionTimeout);
        await using var session = await CreateTranslationProvider(snapshot, outputDeviceId: null)
            .StartSessionAsync(
                AudioChannelId.Microphone,
                new SpeechTranslationSessionOptions(
                    snapshot.SourceLanguage,
                    snapshot.TargetLanguage,
                    snapshot.Region,
                    Mode: "s2t"),
                timeout.Token)
            .ConfigureAwait(false);

        await session.CompleteAsync(timeout.Token).ConfigureAwait(false);
        return "连接成功：同声传译会话已建立";
    }

    public async Task<string> TestTranslationFunctionAsync(
        TranslationSettingsViewModel translation,
        AudioSettingsViewModel audio,
        CancellationToken cancellationToken = default)
    {
        var snapshot = TranslationSnapshot.From(translation);
        ValidateCredentials(snapshot.AppKey, snapshot.AccessKey, "同声传译");

        var frames = await CaptureFramesAsync(
                AudioChannelId.Microphone,
                new MicrophoneCaptureService(_deviceService, NormalizeDeviceId(audio.SelectedMicrophoneDevice)),
                cancellationToken)
            .ConfigureAwait(false);
        if (frames.Count == 0)
        {
            return "未采集到麦克风音频，请确认已选择可用麦克风并说话";
        }

        using var timeout = CreateTimeout(cancellationToken, ConnectionTimeout + ResponseTimeout);
        await using var session = await CreateTranslationProvider(snapshot, outputDeviceId: null)
            .StartSessionAsync(
                AudioChannelId.Microphone,
                new SpeechTranslationSessionOptions(
                    snapshot.SourceLanguage,
                    snapshot.TargetLanguage,
                    snapshot.Region,
                    Mode: "s2t"),
                timeout.Token)
            .ConfigureAwait(false);

        var segments = await SendFramesAndCollectTranslationAsync(session, frames, timeout.Token)
            .ConfigureAwait(false);
        var segment = segments.LastOrDefault(item =>
            !string.IsNullOrWhiteSpace(item.SourceText) ||
            !string.IsNullOrWhiteSpace(item.TranslatedText));

        return segment is null
            ? $"已采集并发送麦克风音频（{FormatAudioSummary(frames)}），但未收到字幕结果"
            : $"功能正常：{FormatAudioSummary(frames)}，原始：{Preview(segment.SourceText)}；翻译：{Preview(segment.TranslatedText)}";
    }

    public async Task<string> TestSpeechRecognitionConnectionAsync(
        SpeechRecognitionSettingsViewModel speechRecognition,
        CancellationToken cancellationToken = default)
    {
        var snapshot = SpeechRecognitionSnapshot.From(speechRecognition);
        ValidateCredentials(snapshot.AppKey, snapshot.AccessKey, "语音识别");

        using var timeout = CreateTimeout(cancellationToken, ConnectionTimeout);
        await using var session = await CreateSpeechRecognitionProvider(snapshot)
            .StartSessionAsync(
                AudioChannelId.Monitor,
                new SpeechRecognitionSessionOptions(snapshot.Language, snapshot.Region),
                timeout.Token)
            .ConfigureAwait(false);

        await session.CompleteAsync(timeout.Token).ConfigureAwait(false);
        return "连接成功：语音识别会话已建立";
    }

    public async Task<string> TestSpeechRecognitionFunctionAsync(
        SpeechRecognitionSettingsViewModel speechRecognition,
        AudioSettingsViewModel audio,
        CancellationToken cancellationToken = default)
    {
        var snapshot = SpeechRecognitionSnapshot.From(speechRecognition);
        ValidateCredentials(snapshot.AppKey, snapshot.AccessKey, "语音识别");

        var frames = await CaptureFramesAsync(
                AudioChannelId.Monitor,
                new LoopbackCaptureService(_deviceService, NormalizeDeviceId(audio.SelectedMonitorDevice)),
                cancellationToken)
            .ConfigureAwait(false);
        if (frames.Count == 0)
        {
            return "未采集到游戏/系统声音，请确认已选择播放设备并让它正在出声";
        }

        using var timeout = CreateTimeout(cancellationToken, ConnectionTimeout + ResponseTimeout);
        await using var session = await CreateSpeechRecognitionProvider(snapshot)
            .StartSessionAsync(
                AudioChannelId.Monitor,
                new SpeechRecognitionSessionOptions(snapshot.Language, snapshot.Region),
                timeout.Token)
            .ConfigureAwait(false);

        var segments = await SendFramesAndCollectRecognitionAsync(session, frames, timeout.Token)
            .ConfigureAwait(false);
        var segment = segments.LastOrDefault(item => !string.IsNullOrWhiteSpace(item.Text));

        return segment is null
            ? $"已采集并发送游戏/系统声音（{FormatAudioSummary(frames)}），但未收到识别结果"
            : $"功能正常：{FormatAudioSummary(frames)}，识别：{Preview(segment.Text)}";
    }

    private VolcengineAstSpeechTranslationProvider CreateTranslationProvider(
        TranslationSnapshot snapshot,
        string? outputDeviceId)
    {
        return new VolcengineAstSpeechTranslationProvider(
            new VolcengineAstProviderOptions(snapshot.AppKey, snapshot.AccessKey),
            new VolcengineAstProtobufProtocolCodec(),
            () => new ClientWebSocketAstTransport(
                logger: _loggerFactory.CreateLogger<ClientWebSocketAstTransport>()),
            audioOutputPlayer: null,
            renderDeviceId: outputDeviceId,
            _loggerFactory.CreateLogger<VolcengineAstSpeechTranslationProvider>());
    }

    private VolcengineStreamingAsrProvider CreateSpeechRecognitionProvider(SpeechRecognitionSnapshot snapshot)
    {
        return new VolcengineStreamingAsrProvider(
            new VolcengineStreamingAsrOptions(snapshot.AppKey, snapshot.AccessKey),
            new VolcengineStreamingAsrProtocolCodec(),
            () => new ClientWebSocketStreamingAsrTransport(
                logger: _loggerFactory.CreateLogger<ClientWebSocketStreamingAsrTransport>()),
            _loggerFactory.CreateLogger<VolcengineStreamingAsrProvider>());
    }

    private async Task<IReadOnlyList<AudioFrame>> CaptureFramesAsync(
        AudioChannelId channelId,
        IAudioCaptureService captureService,
        CancellationToken cancellationToken)
    {
        await using var source = new CaptureAudioFrameSource(
            channelId,
            captureService,
            _loggerFactory.CreateLogger<CaptureAudioFrameSource>());

        using var readCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var frames = new List<AudioFrame>();
        var readTask = Task.Run(async () =>
        {
            await foreach (var frame in source.GetFramesAsync(readCts.Token).ConfigureAwait(false))
            {
                frames.Add(frame);
            }
        }, CancellationToken.None);

        await source.StartAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await Task.Delay(CaptureDuration, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            await source.StopAsync().ConfigureAwait(false);
            readCts.Cancel();
            await IgnoreCancellationAsync(readTask).ConfigureAwait(false);
        }

        return frames;
    }

    private async Task<IReadOnlyList<TranslationSegment>> SendFramesAndCollectTranslationAsync(
        ISpeechTranslationSession session,
        IReadOnlyList<AudioFrame> frames,
        CancellationToken cancellationToken)
    {
        var segments = new List<TranslationSegment>();
        using var readCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var readTask = Task.Run(async () =>
        {
            await foreach (var segment in session.ReadSegmentsAsync(readCts.Token).ConfigureAwait(false))
            {
                segments.Add(segment);
            }
        }, CancellationToken.None);

        foreach (var frame in frames)
        {
            await session.SendAudioAsync(frame, cancellationToken).ConfigureAwait(false);
        }

        await session.CompleteAsync(cancellationToken).ConfigureAwait(false);
        await WaitForReadTaskAsync(readTask, readCts, cancellationToken).ConfigureAwait(false);
        return segments;
    }

    private async Task<IReadOnlyList<SpeechRecognitionSegment>> SendFramesAndCollectRecognitionAsync(
        ISpeechRecognitionSession session,
        IReadOnlyList<AudioFrame> frames,
        CancellationToken cancellationToken)
    {
        var segments = new List<SpeechRecognitionSegment>();
        using var readCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var readTask = Task.Run(async () =>
        {
            await foreach (var segment in session.ReadSegmentsAsync(readCts.Token).ConfigureAwait(false))
            {
                segments.Add(segment);
            }
        }, CancellationToken.None);

        foreach (var frame in frames)
        {
            await session.SendAudioAsync(frame, cancellationToken).ConfigureAwait(false);
        }

        await session.CompleteAsync(cancellationToken).ConfigureAwait(false);
        await WaitForReadTaskAsync(readTask, readCts, cancellationToken).ConfigureAwait(false);
        return segments;
    }

    private static async Task WaitForReadTaskAsync(
        Task readTask,
        CancellationTokenSource readCts,
        CancellationToken cancellationToken)
    {
        var waitTask = Task.Delay(ResponseTimeout, cancellationToken);
        var completed = await Task.WhenAny(readTask, waitTask).ConfigureAwait(false);
        if (completed == readTask)
        {
            await readTask.ConfigureAwait(false);
            return;
        }

        readCts.Cancel();
        await IgnoreCancellationAsync(readTask).ConfigureAwait(false);
    }

    private static async Task IgnoreCancellationAsync(Task task)
    {
        try
        {
            await task.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }
    }

    private static CancellationTokenSource CreateTimeout(CancellationToken cancellationToken, TimeSpan timeout)
    {
        var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(timeout);
        return cts;
    }

    private static void ValidateCredentials(string appKey, string accessKey, string serviceName)
    {
        if (string.IsNullOrWhiteSpace(appKey) || string.IsNullOrWhiteSpace(accessKey))
        {
            throw new InvalidOperationException($"请先填写{serviceName} APP ID 和 Access Token");
        }
    }

    private static string FormatAudioSummary(IReadOnlyList<AudioFrame> frames)
    {
        var duration = TimeSpan.FromTicks(frames.Sum(frame => frame.Duration.Ticks));
        var bytes = frames.Sum(frame => frame.Pcm16Mono16Khz.Length);
        return $"{frames.Count} 帧，{duration.TotalSeconds:F1} 秒，{bytes / 1024.0:F1} KB";
    }

    private static string Preview(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "<无>";
        }

        var normalized = value.Trim();
        return normalized.Length <= 48 ? normalized : normalized[..48] + "...";
    }

    private static string? NormalizeDeviceId(string deviceId)
    {
        if (string.IsNullOrWhiteSpace(deviceId))
        {
            return null;
        }

        return deviceId.StartsWith("Default ", StringComparison.OrdinalIgnoreCase)
            ? null
            : deviceId;
    }

    private sealed record TranslationSnapshot(
        string SourceLanguage,
        string TargetLanguage,
        string Region,
        string AppKey,
        string AccessKey)
    {
        public static TranslationSnapshot From(TranslationSettingsViewModel settings)
        {
            return new TranslationSnapshot(
                settings.SourceLanguage,
                settings.TargetLanguage,
                settings.Region,
                settings.AccessKeyId,
                settings.SecretAccessKey);
        }
    }

    private sealed record SpeechRecognitionSnapshot(
        string Language,
        string Region,
        string AppKey,
        string AccessKey)
    {
        public static SpeechRecognitionSnapshot From(SpeechRecognitionSettingsViewModel settings)
        {
            return new SpeechRecognitionSnapshot(
                settings.Language,
                settings.Region,
                settings.AccessKeyId,
                settings.SecretAccessKey);
        }
    }
}
