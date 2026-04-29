using System.Reflection;
using GameSubRelay.App.Runtime;
using GameSubRelay.App.ViewModels;
using GameSubRelay.Core.Audio;
using GameSubRelay.Core.Captions;
using GameSubRelay.Core.Configuration;
using GameSubRelay.Core.Translation;
using GameSubRelay.Infrastructure.Audio;
using GameSubRelay.Infrastructure.Runtime;
using GameSubRelay.Infrastructure.Translation.Volcengine;
using Microsoft.Extensions.Logging;
using NAudio.CoreAudioApi;
using Xunit;

namespace GameSubRelay.App.Tests;

public sealed class AppAudioChannelWorkerFactoryTests
{
    [Fact]
    public void CreateWorkers_uses_ast_s2t_for_game_audio_caption_channel()
    {
        var settings = CreateSettings();
        settings.Audio.MicrophoneEnabled = false;
        settings.Audio.MonitorEnabled = true;
        settings.Audio.SelectedMonitorDevice = "speaker-id";
        settings.Translation.SourceLanguage = "zh";
        settings.Translation.TargetLanguage = "en";
        settings.Translation.Region = "cn-north-1";
        settings.GameCaption.SourceLanguage = "en";
        settings.GameCaption.TargetLanguage = "zh";
        settings.GameCaption.Region = "cn-north-1";
        settings.Translation.AccessKeyId = "app-key";
        settings.Translation.SecretAccessKey = "access-token";
        settings.SpeechRecognition.AccessKeyId = "asr-app-key";
        settings.SpeechRecognition.SecretAccessKey = "asr-access-token";

        using var loggerFactory = LoggerFactory.Create(static _ => { });
        var factory = new AppAudioChannelWorkerFactory(
            settings,
            new StubAudioDeviceService(),
            new CaptionStore(),
            loggerFactory);

        var worker = Assert.Single(factory.CreateWorkers());
        var translationWorker = Assert.IsType<TranslationChannelWorker>(worker);
        var sessionOptions = GetPrivateField<SpeechTranslationSessionOptions>(
            translationWorker,
            "_sessionOptions");
        var provider = GetPrivateField<VolcengineAstSpeechTranslationProvider>(
            translationWorker,
            "_translationProvider");
        var providerOptions = GetPrivateField<VolcengineAstProviderOptions>(
            provider,
            "_options");
        var audioOutputPlayer = GetPrivateFieldValue(provider, "_audioOutputPlayer");

        Assert.Equal(AudioChannelId.Monitor, translationWorker.ChannelId);
        Assert.Equal("app-key", providerOptions.AppKey);
        Assert.Equal("access-token", providerOptions.AccessKey);
        Assert.Equal("en", sessionOptions.SourceLanguage);
        Assert.Equal("zh", sessionOptions.TargetLanguage);
        Assert.Equal("cn-north-1", sessionOptions.Region);
        Assert.Equal("s2t", sessionOptions.Mode);
        Assert.Null(audioOutputPlayer);
    }

    private static SettingsViewModel CreateSettings()
    {
        return new SettingsViewModel(
            new OverlayViewModel(),
            new StubSettingsStore(),
            new StubSecretStore());
    }

    private static T GetPrivateField<T>(object instance, string fieldName)
    {
        var field = instance.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        return Assert.IsAssignableFrom<T>(field.GetValue(instance));
    }

    private static object? GetPrivateFieldValue(object instance, string fieldName)
    {
        var field = instance.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        return field.GetValue(instance);
    }

    private sealed class StubSettingsStore : ISettingsStore
    {
        public ValueTask<AppSettings> LoadSettingsAsync(CancellationToken cancellationToken = default)
            => ValueTask.FromResult(AppSettings.Default);

        public ValueTask SaveSettingsAsync(AppSettings settings, CancellationToken cancellationToken = default)
            => ValueTask.CompletedTask;
    }

    private sealed class StubSecretStore : ISecretStore
    {
        public ValueTask<SecretSettings> LoadSecretsAsync(CancellationToken cancellationToken = default)
            => ValueTask.FromResult(SecretSettings.Empty);

        public ValueTask SaveSecretsAsync(SecretSettings secrets, CancellationToken cancellationToken = default)
            => ValueTask.CompletedTask;
    }

    private sealed class StubAudioDeviceService : INaudioDeviceService
    {
        public Task<IReadOnlyList<AudioDeviceInfo>> GetCaptureDevicesAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<AudioDeviceInfo>>(Array.Empty<AudioDeviceInfo>());

        public Task<IReadOnlyList<AudioDeviceInfo>> GetRenderDevicesAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<AudioDeviceInfo>>(Array.Empty<AudioDeviceInfo>());

        public Task<string?> GetDefaultCaptureDeviceIdAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<string?>(null);

        public Task<string?> GetDefaultRenderDeviceIdAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<string?>(null);

        public Task<MMDevice?> GetCaptureDeviceAsync(string? deviceId, CancellationToken cancellationToken = default)
            => Task.FromResult<MMDevice?>(null);

        public Task<MMDevice?> GetRenderDeviceAsync(string? deviceId, CancellationToken cancellationToken = default)
            => Task.FromResult<MMDevice?>(null);
    }
}
