using System.Threading;
using System.Threading.Tasks;

namespace GameSubRelay.Core.Configuration;

public interface ISettingsStore
{
    ValueTask<AppSettings> LoadSettingsAsync(CancellationToken cancellationToken = default);

    ValueTask SaveSettingsAsync(AppSettings settings, CancellationToken cancellationToken = default);
}

public interface ISecretStore
{
    ValueTask<SecretSettings> LoadSecretsAsync(CancellationToken cancellationToken = default);

    ValueTask SaveSecretsAsync(SecretSettings secrets, CancellationToken cancellationToken = default);
}
