namespace GameSubRelay.Core.Configuration;

public sealed record SecretSettings(
    string VolcengineAccessKeyId,
    string VolcengineSecretAccessKey,
    string VolcengineAppKey,
    string VolcengineAstAccessKey,
    string VolcengineTtsAppId,
    string VolcengineTtsToken,
    string VolcengineTtsCluster,
    string VolcengineAsrAppKey = "",
    string VolcengineAsrAccessKey = "")
{
    public static SecretSettings Empty => new(
        VolcengineAccessKeyId: string.Empty,
        VolcengineSecretAccessKey: string.Empty,
        VolcengineAppKey: string.Empty,
        VolcengineAstAccessKey: string.Empty,
        VolcengineTtsAppId: string.Empty,
        VolcengineTtsToken: string.Empty,
        VolcengineTtsCluster: string.Empty);

    public SecretSettings Normalize() => this with
    {
        VolcengineAccessKeyId = Trim(VolcengineAccessKeyId),
        VolcengineSecretAccessKey = Trim(VolcengineSecretAccessKey),
        VolcengineAppKey = Trim(VolcengineAppKey),
        VolcengineAstAccessKey = Trim(VolcengineAstAccessKey),
        VolcengineTtsAppId = Trim(VolcengineTtsAppId),
        VolcengineTtsToken = Trim(VolcengineTtsToken),
        VolcengineTtsCluster = Trim(VolcengineTtsCluster),
        VolcengineAsrAppKey = Trim(VolcengineAsrAppKey),
        VolcengineAsrAccessKey = Trim(VolcengineAsrAccessKey)
    };

    private static string Trim(string? value) => value?.Trim() ?? string.Empty;
}
