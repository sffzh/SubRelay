namespace GameSubRelay.Core.Configuration;

public sealed record GameCaptionSettings(
    string SourceLanguage,
    string TargetLanguage,
    string Region,
    string Mode)
{
    public static GameCaptionSettings Default => new(
        SourceLanguage: "en",
        TargetLanguage: "zh",
        Region: "cn-north-1",
        Mode: "s2t");

    public static GameCaptionSettings FromTranslation(TranslationSettings? translation)
    {
        var normalized = (translation ?? TranslationSettings.Default).Normalize();
        return new GameCaptionSettings(
            normalized.SourceLanguage,
            normalized.TargetLanguage,
            normalized.Region,
            normalized.Mode);
    }

    public GameCaptionSettings Normalize() => this with
    {
        SourceLanguage = string.IsNullOrWhiteSpace(SourceLanguage) ? Default.SourceLanguage : SourceLanguage.Trim(),
        TargetLanguage = string.IsNullOrWhiteSpace(TargetLanguage) ? Default.TargetLanguage : TargetLanguage.Trim(),
        Region = string.IsNullOrWhiteSpace(Region) ? Default.Region : Region.Trim(),
        Mode = string.IsNullOrWhiteSpace(Mode) ? Default.Mode : Mode.Trim()
    };
}
