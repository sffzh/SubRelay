namespace GameSubRelay.Core.Configuration;

public sealed record SpeechRecognitionSettings(
    string Provider,
    string Language,
    string Region)
{
    public const string DefaultProvider = "VolcengineStreamingAsr";

    public static SpeechRecognitionSettings Default => new(
        DefaultProvider,
        Language: "en",
        Region: "cn-north-1");

    public static SpeechRecognitionSettings FromTranslation(TranslationSettings? translation)
    {
        var normalized = (translation ?? TranslationSettings.Default).Normalize();
        return new SpeechRecognitionSettings(
            DefaultProvider,
            normalized.SourceLanguage,
            normalized.Region);
    }

    public SpeechRecognitionSettings Normalize() => this with
    {
        Provider = string.IsNullOrWhiteSpace(Provider) ? DefaultProvider : Provider.Trim(),
        Language = string.IsNullOrWhiteSpace(Language) ? Default.Language : Language.Trim(),
        Region = string.IsNullOrWhiteSpace(Region) ? Default.Region : Region.Trim()
    };
}
