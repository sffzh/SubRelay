using System.Collections.Generic;

namespace GameSubRelay.Core.Translation;

public sealed record SpeechTranslationSessionOptions(
    string SourceLanguage,
    string TargetLanguage,
    string Region,
    IReadOnlyList<string>? HotWords = null,
    string Mode = "s2t")
{
    public const string DefaultSourceLanguage = "en";
    public const string DefaultTargetLanguage = "zh";
    public const string DefaultRegion = "cn-north-1";
    public const string DefaultMode = "s2t";

    public static SpeechTranslationSessionOptions Default { get; } = new(
        DefaultSourceLanguage,
        DefaultTargetLanguage,
        DefaultRegion,
        null,
        DefaultMode);
}
