using GameSubRelay.Core.Configuration;
using GameSubRelay.Infrastructure.Configuration;
using Xunit;

namespace GameSubRelay.Infrastructure.Tests;

public sealed class JsonSettingsStoreTests
{
    [Fact]
    public async Task LoadSettingsAsync_returns_default_settings_when_file_is_missing()
    {
        var directory = CreateTempDirectory();
        try
        {
            var store = new JsonSettingsStore(new DirectoryInfo(directory));

            var settings = await store.LoadSettingsAsync();

            Assert.Equal("en", settings.Translation.SourceLanguage);
            Assert.Equal("zh", settings.Translation.TargetLanguage);
            Assert.Equal("en", settings.GameCaption.SourceLanguage);
            Assert.Equal("zh", settings.GameCaption.TargetLanguage);
            Assert.Equal("en", settings.SpeechRecognition.Language);
            Assert.Equal(6, settings.Overlay.MaxLines);
            Assert.False(File.Exists(Path.Combine(directory, "settings.json")));
        }
        finally
        {
            DeleteTempDirectory(directory);
        }
    }

    [Fact]
    public async Task SaveSettingsAsync_writes_normalized_json_to_custom_directory()
    {
        var directory = CreateTempDirectory();
        try
        {
            var store = new JsonSettingsStore(new DirectoryInfo(directory));
            var settings = AppSettings.Default with
            {
                Overlay = AppSettings.Default.Overlay with
                {
                    MaxLines = 99,
                    Opacity = 0.01
                },
                Hotkeys = AppSettings.Default.Hotkeys with
                {
                    ToggleOverlay = "Alt+Ctrl+S"
                }
            };

            await store.SaveSettingsAsync(settings);
            var loaded = await store.LoadSettingsAsync();

            Assert.True(File.Exists(Path.Combine(directory, "settings.json")));
            Assert.Equal(12, loaded.Overlay.MaxLines);
            Assert.Equal(0.2, loaded.Overlay.Opacity);
            Assert.Equal("Ctrl+Alt+S", loaded.Hotkeys.ToggleOverlay);
        }
        finally
        {
            DeleteTempDirectory(directory);
        }
    }

    [Fact]
    public async Task Constructor_accepts_custom_settings_file_path()
    {
        var directory = CreateTempDirectory();
        try
        {
            var file = new FileInfo(Path.Combine(directory, "nested", "custom-settings.json"));
            var store = new JsonSettingsStore(file.FullName, isFilePath: true);

            await store.SaveSettingsAsync(AppSettings.Default);

            Assert.True(File.Exists(file.FullName));
            var loaded = await store.LoadSettingsAsync();
            Assert.Equal("cn-north-1", loaded.Translation.Region);
            Assert.Equal("cn-north-1", loaded.GameCaption.Region);
            Assert.Equal("cn-north-1", loaded.SpeechRecognition.Region);
        }
        finally
        {
            DeleteTempDirectory(directory);
        }
    }

    [Fact]
    public async Task LoadSettingsAsync_maps_design_json_audio_tts_flags_to_tts_settings()
    {
        var directory = CreateTempDirectory();
        try
        {
            await File.WriteAllTextAsync(
                Path.Combine(directory, "settings.json"),
                """
                {
                  "audio": {
                    "microphoneDeviceId": "",
                    "monitorRenderDeviceId": "",
                    "ttsOutputDeviceId": "",
                    "microphoneEnabled": true,
                    "monitorEnabled": true,
                    "ttsEnabled": true,
                    "ttsForMicrophone": true,
                    "ttsForMonitor": false
                  },
                  "translation": {
                    "provider": "VolcengineSpeechTranslate",
                    "sourceLanguage": "en",
                    "targetLanguage": "zh",
                    "region": "cn-north-1"
                  },
                  "overlay": {
                    "left": 120,
                    "top": 720,
                    "width": 760,
                    "height": 220,
                    "opacity": 0.65,
                    "fontSize": 22,
                    "maxLines": 6,
                    "visible": true
                  },
                  "hotkeys": {
                    "toggleOverlay": "Ctrl+Alt+S",
                    "toggleEditMode": "Ctrl+Alt+E",
                    "clearCaptions": "Ctrl+Alt+C"
                  }
                }
                """);
            var store = new JsonSettingsStore(new DirectoryInfo(directory));

            var settings = await store.LoadSettingsAsync();

            Assert.True(settings.Tts.Enabled);
            Assert.True(settings.Tts.UseForMicrophone);
            Assert.False(settings.Tts.UseForMonitor);
            Assert.True(settings.Audio.TtsEnabled);
            Assert.True(settings.Audio.TtsForMicrophone);
            Assert.Equal("en", settings.SpeechRecognition.Language);
            Assert.Equal("en", settings.GameCaption.SourceLanguage);
            Assert.Equal("zh", settings.GameCaption.TargetLanguage);
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
