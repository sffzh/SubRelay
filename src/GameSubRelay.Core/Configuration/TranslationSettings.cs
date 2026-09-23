namespace GameSubRelay.Core.Configuration;

public sealed record TranslationSettings(
    string Provider,
    string SourceLanguage,
    string TargetLanguage,
    string Region,
    string Mode)
{
    public const string DefaultProvider = "VolcengineAstTranslate";

    public static TranslationSettings Default => new(
        DefaultProvider,
        SourceLanguage: "en",
        TargetLanguage: "zh",
        Region: "cn-north-1",
        Mode: "s2t");

    public TranslationSettings Normalize() => this with
    {
        Provider = string.IsNullOrWhiteSpace(Provider) ? DefaultProvider : Provider.Trim(),
        SourceLanguage = string.IsNullOrWhiteSpace(SourceLanguage) ? Default.SourceLanguage : SourceLanguage.Trim(),
        TargetLanguage = string.IsNullOrWhiteSpace(TargetLanguage) ? Default.TargetLanguage : TargetLanguage.Trim(),
        Region = string.IsNullOrWhiteSpace(Region) ? Default.Region : Region.Trim(),
        Mode = string.IsNullOrWhiteSpace(Mode) ? Default.Mode : Mode.Trim()
    };
}
