namespace GameSubRelay.Core.Configuration;

public sealed record SecretSettings(
    string VolcengineAccessKeyId,
    string VolcengineSecretAccessKey,
    string VolcengineAppKey,
    string VolcengineAstAccessKey,
    string VolcengineTtsAppId,
    string VolcengineTtsToken,
    string VolcengineTtsCluster)
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
        VolcengineAccessKeyId = VolcengineAccessKeyId.Trim(),
        VolcengineSecretAccessKey = VolcengineSecretAccessKey.Trim(),
        VolcengineAppKey = VolcengineAppKey.Trim(),
        VolcengineAstAccessKey = VolcengineAstAccessKey.Trim(),
        VolcengineTtsAppId = VolcengineTtsAppId.Trim(),
        VolcengineTtsToken = VolcengineTtsToken.Trim(),
        VolcengineTtsCluster = VolcengineTtsCluster.Trim()
    };
}
