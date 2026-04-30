using System.Text.Json;
using GameSubRelay.Core.Configuration;

namespace GameSubRelay.Infrastructure.Configuration;

public sealed class JsonSettingsStore : ISettingsStore
{
    public const string DefaultFileName = "settings.json";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private readonly FileInfo _settingsFile;

    public JsonSettingsStore()
        : this(new FileInfo(GetDefaultSettingsFilePath()))
    {
    }

    public JsonSettingsStore(string directoryPath)
        : this(new DirectoryInfo(directoryPath))
    {
    }

    public JsonSettingsStore(string path, bool isFilePath)
        : this(new FileInfo(isFilePath ? path : Path.Combine(path, DefaultFileName)))
    {
    }

    public JsonSettingsStore(DirectoryInfo directory)
        : this(new FileInfo(Path.Combine(directory.FullName, DefaultFileName)))
    {
    }

    public JsonSettingsStore(FileInfo settingsFile)
    {
        _settingsFile = settingsFile;
    }

    public async ValueTask<AppSettings> LoadSettingsAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_settingsFile.FullName))
        {
            return AppSettings.Default.Normalize();
        }

        await using var stream = File.OpenRead(_settingsFile.FullName);
        var settings = await JsonSerializer.DeserializeAsync<AppSettings>(
            stream,
            JsonOptions,
            cancellationToken);

        return (settings ?? AppSettings.Default).Normalize();
    }

    public async ValueTask SaveSettingsAsync(AppSettings settings, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var normalized = settings.Normalize() with
        {
            LastUpdatedUtc = DateTimeOffset.UtcNow
        };
        var validation = normalized.Validate();
        if (!validation.IsValid)
        {
            throw new AppSettingsValidationException(validation);
        }

        Directory.CreateDirectory(_settingsFile.DirectoryName ?? ".");
        var tempFile = new FileInfo(_settingsFile.FullName + ".tmp");

        await using (var stream = tempFile.Create())
        {
            await JsonSerializer.SerializeAsync(stream, normalized, JsonOptions, cancellationToken);
        }

        File.Move(tempFile.FullName, _settingsFile.FullName, overwrite: true);
    }

    private static string GetDefaultSettingsFilePath()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        return Path.Combine(appData, "SubRelay", DefaultFileName);
    }
}
