using GameSubRelay.App.ViewModels;
using GameSubRelay.Core.Audio;
using GameSubRelay.Core.Captions;
using GameSubRelay.Core.Translation;
using GameSubRelay.Infrastructure.Audio;
using GameSubRelay.Infrastructure.Runtime;
using GameSubRelay.Infrastructure.Translation.Volcengine;
using Microsoft.Extensions.Logging;

namespace GameSubRelay.App.Runtime;

public sealed class AppTranslationChannelWorkerFactory : ITranslationChannelWorkerFactory
{
    private readonly SettingsViewModel _settings;
    private readonly INaudioDeviceService _deviceService;
    private readonly CaptionStore _captionStore;
    private readonly ILoggerFactory _loggerFactory;

    public AppTranslationChannelWorkerFactory(
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

    public IReadOnlyList<ITranslationChannelWorker> CreateWorkers()
    {
        var workers = new List<ITranslationChannelWorker>();
        var logger = _loggerFactory.CreateLogger<TranslationChannelWorker>();
        var astLogger = _loggerFactory.CreateLogger<VolcengineAstSpeechTranslationProvider>();
        var asrLogger = _loggerFactory.CreateLogger<VolcengineStreamingAsrProvider>();

        if (_settings.Audio.MicrophoneEnabled)
        {
            var astProvider = new VolcengineAstSpeechTranslationProvider(
                new VolcengineAstProviderOptions(
                    _settings.Translation.AccessKeyId,
                    _settings.Translation.SecretAccessKey),
                new VolcengineAstProtobufProtocolCodec(),
                () => new ClientWebSocketAstTransport(
                    logger: _loggerFactory.CreateLogger<ClientWebSocketAstTransport>()),
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
                astProvider,
                astSessionOptions,
                logger));
        }

        if (_settings.Audio.MonitorEnabled)
        {
            var asrProvider = new VolcengineStreamingAsrProvider(new VolcengineStreamingAsrOptions(
                _settings.Translation.AccessKeyId,
                _settings.Translation.SecretAccessKey),
                new VolcengineStreamingAsrProtocolCodec(),
                () => new ClientWebSocketStreamingAsrTransport(
                    logger: _loggerFactory.CreateLogger<ClientWebSocketStreamingAsrTransport>()),
                asrLogger);
            var asrSessionOptions = new SpeechTranslationSessionOptions(
                _settings.Translation.SourceLanguage,
                _settings.Translation.TargetLanguage,
                _settings.Translation.Region,
                Mode: "asr");

            workers.Add(CreateWorker(
                AudioChannelId.Monitor,
                new LoopbackCaptureService(_deviceService, NormalizeDeviceId(_settings.Audio.SelectedMonitorDevice)),
                asrProvider,
                asrSessionOptions,
                logger));
        }

        return workers;
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
}
