namespace GameSubRelay.Core.SpeechRecognition;

public sealed record SpeechRecognitionSessionOptions(
    string Language,
    string Region)
{
    public const string DefaultLanguage = "en";
    public const string DefaultRegion = "cn-north-1";

    public static SpeechRecognitionSessionOptions Default { get; } = new(
        DefaultLanguage,
        DefaultRegion);
}
