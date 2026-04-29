using GameSubRelay.App.ViewModels;

namespace GameSubRelay.App.Diagnostics;

public interface IRelayDiagnosticsService
{
    Task<string> TestTranslationConnectionAsync(
        TranslationSettingsViewModel translation,
        CancellationToken cancellationToken = default);

    Task<string> TestTranslationFunctionAsync(
        TranslationSettingsViewModel translation,
        AudioSettingsViewModel audio,
        CancellationToken cancellationToken = default);

    Task<string> TestGameCaptionConnectionAsync(
        TranslationSettingsViewModel translation,
        GameCaptionSettingsViewModel gameCaption,
        CancellationToken cancellationToken = default);

    Task<string> TestGameCaptionFunctionAsync(
        TranslationSettingsViewModel translation,
        GameCaptionSettingsViewModel gameCaption,
        AudioSettingsViewModel audio,
        CancellationToken cancellationToken = default);

    Task<string> TestSpeechRecognitionConnectionAsync(
        SpeechRecognitionSettingsViewModel speechRecognition,
        CancellationToken cancellationToken = default);

    Task<string> TestSpeechRecognitionFunctionAsync(
        SpeechRecognitionSettingsViewModel speechRecognition,
        AudioSettingsViewModel audio,
        CancellationToken cancellationToken = default);
}
