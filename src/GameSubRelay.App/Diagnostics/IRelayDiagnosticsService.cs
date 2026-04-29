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

    Task<string> TestSpeechRecognitionConnectionAsync(
        SpeechRecognitionSettingsViewModel speechRecognition,
        CancellationToken cancellationToken = default);

    Task<string> TestSpeechRecognitionFunctionAsync(
        SpeechRecognitionSettingsViewModel speechRecognition,
        AudioSettingsViewModel audio,
        CancellationToken cancellationToken = default);
}
