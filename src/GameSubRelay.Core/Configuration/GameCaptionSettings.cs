namespace GameSubRelay.Core.Configuration;

public sealed record GameCaptionSettings(
    string SourceLanguage,
    string TargetLanguage,
    string Region)
{
    public static GameCaptionSettings Default => new(
        SourceLanguage: "en",
        TargetLanguage: "zh",
        Region: "cn-north-1");

    public static GameCaptionSettings FromTranslation(TranslationSettings? translation)
    {
        var normalized = (translation ?? TranslationSettings.Default).Normalize();
        return new GameCaptionSettings(
            normalized.SourceLanguage,
            normalized.TargetLanguage,
            normalized.Region);
    }

    public GameCaptionSettings Normalize() => this with
    {
        SourceLanguage = string.IsNullOrWhiteSpace(SourceLanguage) ? Default.SourceLanguage : SourceLanguage.Trim(),
        TargetLanguage = string.IsNullOrWhiteSpace(TargetLanguage) ? Default.TargetLanguage : TargetLanguage.Trim(),
        Region = string.IsNullOrWhiteSpace(Region) ? Default.Region : Region.Trim()
    };
}
