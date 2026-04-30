using GameSubRelay.App.ViewModels;
using GameSubRelay.Core.Audio;
using GameSubRelay.Core.Captions;
using GameSubRelay.Core.Configuration;
using GameSubRelay.Core.Translation;
using GameSubRelay.Infrastructure.Audio;
using GameSubRelay.Infrastructure.Runtime;
using GameSubRelay.Infrastructure.Translation.Volcengine;
using Microsoft.Extensions.Logging;

namespace GameSubRelay.App.Runtime;

public sealed class AppAudioChannelWorkerFactory : IAudioChannelWorkerFactory
{
    private readonly SettingsViewModel _settings;
    private readonly INaudioDeviceService _deviceService;
    private readonly CaptionStore _captionStore;
    private readonly ILoggerFactory _loggerFactory;

    public AppAudioChannelWorkerFactory(
        SettingsViewModel settings,
        INaudioDeviceService deviceService,
        CaptionStore captionStore,
        ILoggerFactory loggerFactory)
    {
        _settings = settings;
        _deviceService = deviceService;
        _captionStore = captionStore;
        _loggerFactory = loggerFactory;
    }

    public IReadOnlyList<IAudioChannelWorker> CreateWorkers()
    {
        var workers = new List<IAudioChannelWorker>();
        var logger = _loggerFactory.CreateLogger<TranslationChannelWorker>();
        var astLogger = _loggerFactory.CreateLogger<VolcengineAstSpeechTranslationProvider>();

        if (_settings.Audio.MicrophoneEnabled)
        {
            var translationProvider = CreateSpeechTranslationProvider(
                _settings.Translation.Provider,
                _settings.Translation.AccessKeyId,
                _settings.Translation.SecretAccessKey,
                new AudioOutputDevicePlayer(
                    _deviceService,
                    _loggerFactory.CreateLogger<AudioOutputDevicePlayer>()),
                NormalizeDeviceId(_settings.Audio.SelectedTtsOutputDevice),
                astLogger);
            var astSessionOptions = new SpeechTranslationSessionOptions(
                _settings.Translation.SourceLanguage,
                _settings.Translation.TargetLanguage,
                _settings.Translation.Region,
                Mode: "s2s");

            workers.Add(CreateWorker(
                AudioChannelId.Microphone,
                new MicrophoneCaptureService(_deviceService, NormalizeDeviceId(_settings.Audio.SelectedMicrophoneDevice)),
                translationProvider,
                astSessionOptions,
                logger));
        }

        if (_settings.Audio.MonitorEnabled)
        {
            var translationProvider = CreateSpeechTranslationProvider(
                _settings.Translation.Provider,
                _settings.Translation.AccessKeyId,
                _settings.Translation.SecretAccessKey,
                audioOutputPlayer: null,
                renderDeviceId: null,
                astLogger: astLogger);
            var astSessionOptions = new SpeechTranslationSessionOptions(
                _settings.GameCaption.SourceLanguage,
                _settings.GameCaption.TargetLanguage,
                _settings.GameCaption.Region,
                Mode: "s2t");

            workers.Add(CreateWorker(
                AudioChannelId.Monitor,
                new LoopbackCaptureService(_deviceService, NormalizeDeviceId(_settings.Audio.SelectedMonitorDevice)),
                translationProvider,
                astSessionOptions,
                logger));
        }

        return workers;
    }

    private ISpeechTranslationProvider CreateSpeechTranslationProvider(
        string provider,
        string appKey,
        string accessKey,
        IAudioOutputPlayer? audioOutputPlayer,
        string? renderDeviceId,
        ILogger<VolcengineAstSpeechTranslationProvider> astLogger)
    {
        return NormalizeProvider(provider) switch
        {
            TranslationSettings.DefaultProvider => new VolcengineAstSpeechTranslationProvider(
                new VolcengineAstProviderOptions(appKey, accessKey),
                new VolcengineAstProtobufProtocolCodec(),
                () => new ClientWebSocketAstTransport(
                    logger: _loggerFactory.CreateLogger<ClientWebSocketAstTransport>()),
                audioOutputPlayer,
                renderDeviceId,
                astLogger),
            var providerCode => throw new NotSupportedException($"尚未接入语音翻译服务商：{providerCode}")
        };
    }

    private TranslationChannelWorker CreateWorker(
        AudioChannelId channelId,
        IAudioCaptureService captureService,
        ISpeechTranslationProvider provider,
        SpeechTranslationSessionOptions sessionOptions,
        ILogger<TranslationChannelWorker> logger)
    {
        return new TranslationChannelWorker(
            channelId,
            new CaptureAudioFrameSource(
                channelId,
                captureService,
                _loggerFactory.CreateLogger<CaptureAudioFrameSource>()),
            provider,
            sessionOptions,
            _captionStore,
            logger);
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

    private static string NormalizeProvider(string provider)
    {
        return string.IsNullOrWhiteSpace(provider)
            ? TranslationSettings.DefaultProvider
            : provider.Trim();
    }
}
