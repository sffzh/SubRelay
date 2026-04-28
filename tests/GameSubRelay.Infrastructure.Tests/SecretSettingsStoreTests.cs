using System.Text;
using GameSubRelay.Core.Configuration;
using GameSubRelay.Infrastructure.Configuration;
using Xunit;

namespace GameSubRelay.Infrastructure.Tests;

public sealed class SecretSettingsStoreTests
{
    [Fact]
    public async Task LoadSecretsAsync_returns_empty_secrets_when_file_is_missing()
    {
        var directory = CreateTempDirectory();
        try
        {
            var store = new DpapiSecretStore(new DirectoryInfo(directory));

            var secrets = await store.LoadSecretsAsync();

            Assert.Equal(SecretSettings.Empty, secrets);
            Assert.False(File.Exists(Path.Combine(directory, "secrets.json.dpapi")));
        }
        finally
        {
            DeleteTempDirectory(directory);
        }
    }

    [Fact]
    public async Task SaveSecretsAsync_encrypts_and_round_trips_secrets_in_custom_directory()
    {
        var directory = CreateTempDirectory();
        try
        {
            var store = new DpapiSecretStore(new DirectoryInfo(directory));
            var secrets = new SecretSettings(
                VolcengineAccessKeyId: "ak-test",
                VolcengineSecretAccessKey: "sk-test",
                VolcengineAppKey: "app-key-test",
                VolcengineAstAccessKey: "ast-access-key-test",
                VolcengineTtsAppId: "appid-test",
                VolcengineTtsToken: "token-test",
                VolcengineTtsCluster: "cluster-test");

            await store.SaveSecretsAsync(secrets);
            var loaded = await store.LoadSecretsAsync();
            var encryptedFile = Path.Combine(directory, "secrets.json.dpapi");
            var encryptedText = Encoding.UTF8.GetString(await File.ReadAllBytesAsync(encryptedFile));

            Assert.Equal(secrets, loaded);
            Assert.DoesNotContain("ak-test", encryptedText);
            Assert.DoesNotContain("sk-test", encryptedText);
            Assert.DoesNotContain("token-test", encryptedText);
        }
        finally
        {
            DeleteTempDirectory(directory);
        }
    }

    [Fact]
    public async Task Constructor_accepts_custom_secret_file_path()
    {
        var directory = CreateTempDirectory();
        try
        {
            var file = new FileInfo(Path.Combine(directory, "nested", "custom-secrets.bin"));
            var store = new DpapiSecretStore(file.FullName, isFilePath: true);
            var secrets = SecretSettings.Empty with
            {
                VolcengineTtsToken = "token-test"
            };

            await store.SaveSecretsAsync(secrets);

            Assert.True(File.Exists(file.FullName));
            Assert.Equal(secrets, await store.LoadSecretsAsync());
        }
        finally
        {
            DeleteTempDirectory(directory);
        }
    }

    private static string CreateTempDirectory()
    {
        var directory = Path.Combine(Path.GetTempPath(), "GameSubRelay.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }

    private static void DeleteTempDirectory(string directory)
    {
        if (Directory.Exists(directory))
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
