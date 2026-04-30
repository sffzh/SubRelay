using GameSubRelay.App.ViewModels;
using GameSubRelay.App.Diagnostics;
using GameSubRelay.Core.Audio;
using GameSubRelay.Core.Configuration;
using GameSubRelay.Core.Runtime;
using GameSubRelay.Infrastructure.Audio;
using GameSubRelay.Infrastructure.Runtime;
using NAudio.CoreAudioApi;
using Xunit;

namespace GameSubRelay.App.Tests;

public sealed class SettingsViewModelPersistenceTests
{
    [Fact]
    public async Task LoadAsync_applies_saved_settings_and_secrets_to_view_models()
    {
        var settings = AppSettings.Default with
        {
            Audio = new AudioSettings(
                "Saved Microphone",
                "Saved Speakers",
                "Saved TTS",
                MicrophoneEnabled: false,
                MonitorEnabled: true,
                TtsEnabled: true,
                TtsForMicrophone: false,
                TtsForMonitor: true),
            Translation = new TranslationSettings(
                "VolcengineAstTranslate",
                "ja",
                "zh",
                "cn-north-1"),
            GameCaption = new GameCaptionSettings(
                "en",
                "zh",
                "cn-north-1"),
            SpeechRecognition = new SpeechRecognitionSettings(
                "VolcengineStreamingAsr",
                "de",
                "cn-north-1"),
            Overlay = new OverlaySettings(11, 22, 333, 144, 0.7, 28, 4, Visible: true),
            Hotkeys = new HotkeySettings("Ctrl+Alt+J", "Ctrl+Alt+K", "Ctrl+Alt+L"),
            Tts = new TtsSettings(true, false, true, 3)
        };
        var secrets = SecretSettings.Empty with
        {
            VolcengineAppKey = "app-key",
            VolcengineAstAccessKey = "access-key",
            VolcengineTtsAppId = "tts-app",
            VolcengineTtsToken = "tts-token",
            VolcengineTtsCluster = "tts-cluster",
            VolcengineAsrAppKey = "asr-app-key",
            VolcengineAsrAccessKey = "asr-access-key"
        };
        var viewModel = new SettingsViewModel(
            new OverlayViewModel(),
            new InMemorySettingsStore(settings),
            new InMemorySecretStore(secrets));

        await viewModel.LoadAsync();

        Assert.Equal("Saved Microphone", viewModel.Audio.SelectedMicrophoneDevice);
        Assert.Equal("Saved Speakers", viewModel.Audio.SelectedMonitorDevice);
        Assert.Equal("Saved TTS", viewModel.Audio.SelectedTtsOutputDevice);
        Assert.False(viewModel.Audio.MicrophoneEnabled);
        Assert.True(viewModel.Audio.MonitorEnabled);
        Assert.Equal("ja", viewModel.Translation.SourceLanguage);
        Assert.Equal("zh", viewModel.Translation.TargetLanguage);
        Assert.Equal("app-key", viewModel.Translation.AccessKeyId);
        Assert.Equal("access-key", viewModel.Translation.SecretAccessKey);
        Assert.Equal("en", viewModel.GameCaption.SourceLanguage);
        Assert.Equal("zh", viewModel.GameCaption.TargetLanguage);
        Assert.Equal("de", viewModel.SpeechRecognition.Language);
        Assert.Equal("asr-app-key", viewModel.SpeechRecognition.AccessKeyId);
        Assert.Equal("asr-access-key", viewModel.SpeechRecognition.SecretAccessKey);
        Assert.Equal(11, viewModel.Overlay.Left);
        Assert.Equal(333, viewModel.Overlay.Width);
        Assert.True(viewModel.OverlayVisible);
        Assert.True(viewModel.Tts.TtsEnabled);
        Assert.False(viewModel.Tts.TtsForMicrophone);
        Assert.True(viewModel.Tts.TtsForMonitor);
        Assert.Equal("Ctrl+Alt+J", viewModel.Hotkeys.ToggleOverlayHotkey);
    }

    [Fact]
    public async Task SaveAsync_persists_current_view_model_settings_and_secrets()
    {
        var settingsStore = new InMemorySettingsStore(AppSettings.Default);
        var secretStore = new InMemorySecretStore(SecretSettings.Empty);
        var viewModel = new SettingsViewModel(new OverlayViewModel(), settingsStore, secretStore);

        viewModel.Audio.SelectedMicrophoneDevice = "Mic A";
        viewModel.Audio.SelectedMonitorDevice = "Speakers A";
        viewModel.Audio.SelectedTtsOutputDevice = "Cable A";
        viewModel.Audio.MicrophoneEnabled = false;
        viewModel.Translation.SourceLanguage = "en";
        viewModel.Translation.TargetLanguage = "zhen";
        viewModel.GameCaption.SourceLanguage = "en";
        viewModel.GameCaption.TargetLanguage = "zh";
        viewModel.GameCaption.Region = "cn-north-1";
        viewModel.Translation.AccessKeyId = "app-key";
        viewModel.Translation.SecretAccessKey = "access-key";
        viewModel.SpeechRecognition.Language = "ja";
        viewModel.SpeechRecognition.AccessKeyId = "asr-app-key";
        viewModel.SpeechRecognition.SecretAccessKey = "asr-access-key";
        viewModel.Overlay.Left = 101;
        viewModel.Overlay.Top = 202;
        viewModel.Overlay.Width = 303;
        viewModel.Overlay.Height = 404;
        viewModel.OverlayVisible = true;
        viewModel.Tts.TtsEnabled = true;
        viewModel.Tts.TtsForMonitor = true;
        viewModel.Hotkeys.ClearCaptionsHotkey = "Ctrl+Alt+V";

        await viewModel.SaveAsync();

        Assert.NotNull(settingsStore.SavedSettings);
        Assert.Equal("Mic A", settingsStore.SavedSettings.Audio.MicrophoneDeviceId);
        Assert.Equal("Speakers A", settingsStore.SavedSettings.Audio.MonitorRenderDeviceId);
        Assert.Equal("Cable A", settingsStore.SavedSettings.Audio.TtsOutputDeviceId);
        Assert.False(settingsStore.SavedSettings.Audio.MicrophoneEnabled);
        Assert.Equal("zhen", settingsStore.SavedSettings.Translation.TargetLanguage);
        Assert.Equal("en", settingsStore.SavedSettings.GameCaption.SourceLanguage);
        Assert.Equal("zh", settingsStore.SavedSettings.GameCaption.TargetLanguage);
        Assert.Equal("ja", settingsStore.SavedSettings.SpeechRecognition.Language);
        Assert.Equal(101, settingsStore.SavedSettings.Overlay.Left);
        Assert.Equal(404, settingsStore.SavedSettings.Overlay.Height);
        Assert.True(settingsStore.SavedSettings.Overlay.Visible);
        Assert.True(settingsStore.SavedSettings.Tts.Enabled);
        Assert.True(settingsStore.SavedSettings.Tts.UseForMonitor);
        Assert.Equal("Ctrl+Alt+V", settingsStore.SavedSettings.Hotkeys.ClearCaptions);

        Assert.NotNull(secretStore.SavedSecrets);
        Assert.Equal("app-key", secretStore.SavedSecrets.VolcengineAppKey);
        Assert.Equal("access-key", secretStore.SavedSecrets.VolcengineAstAccessKey);
        Assert.Equal("asr-app-key", secretStore.SavedSecrets.VolcengineAsrAppKey);
        Assert.Equal("asr-access-key", secretStore.SavedSecrets.VolcengineAsrAccessKey);
    }

    [Fact]
    public async Task LoadAsync_populates_audio_options_from_system_device_service()
    {
        var audioDevices = new InMemoryAudioDeviceService(
            captureDevices:
            [
                AudioMappingHelpers.ToDeviceInfo(
                    "mic-id",
                    "USB 麦克风",
                    "USB",
                    DataFlow.Capture,
                    isEnabled: true,
                    channels: 2,
                    sampleRate: 48000,
                    bitsPerSample: 16,
                    isDefault: true)
            ],
            renderDevices:
            [
                AudioMappingHelpers.ToDeviceInfo(
                    "speaker-id",
                    "系统扬声器",
                    "Realtek",
                    DataFlow.Render,
                    isEnabled: true,
                    channels: 2,
                    sampleRate: 48000,
                    bitsPerSample: 16,
                    isDefault: true)
            ],
            defaultCaptureDeviceId: "mic-id",
            defaultRenderDeviceId: "speaker-id");
        var viewModel = new SettingsViewModel(
            new OverlayViewModel(),
            new InMemorySettingsStore(AppSettings.Default),
            new InMemorySecretStore(SecretSettings.Empty),
            audioDevices);

        await viewModel.LoadAsync();

        Assert.Contains(viewModel.Audio.AvailableMicrophones, device => device.DeviceId == "mic-id");
        Assert.Contains(viewModel.Audio.AvailableMonitorDevices, device => device.DeviceId == "speaker-id");
        Assert.Contains(viewModel.Audio.AvailableTtsOutputDevices, device => device.DeviceId == "speaker-id");
        Assert.Equal("mic-id", viewModel.Audio.SelectedMicrophoneDevice);
        Assert.Equal("speaker-id", viewModel.Audio.SelectedMonitorDevice);
        Assert.Equal("speaker-id", viewModel.Audio.SelectedTtsOutputDevice);
    }

    [Fact]
    public async Task RefreshAudioDevicesCommand_reloads_device_lists_and_updates_status()
    {
        var audioDevices = new MutableAudioDeviceService();
        var viewModel = new SettingsViewModel(
            new OverlayViewModel(),
            new InMemorySettingsStore(AppSettings.Default),
            new InMemorySecretStore(SecretSettings.Empty),
            audioDevices);

        audioDevices.SetDevices(
            captureDevices:
            [
                AudioMappingHelpers.ToDeviceInfo("first-mic", "第一个麦克风", null, DataFlow.Capture, true, 1, 16000, 16, true)
            ],
            renderDevices:
            [
                AudioMappingHelpers.ToDeviceInfo("first-speaker", "第一个扬声器", null, DataFlow.Render, true, 2, 48000, 16, true)
            ],
            "first-mic",
            "first-speaker");
        await viewModel.RefreshAudioDevicesAsync();

        audioDevices.SetDevices(
            captureDevices:
            [
                AudioMappingHelpers.ToDeviceInfo("second-mic", "第二个麦克风", null, DataFlow.Capture, true, 1, 16000, 16, true)
            ],
            renderDevices:
            [
                AudioMappingHelpers.ToDeviceInfo("second-speaker", "第二个扬声器", null, DataFlow.Render, true, 2, 48000, 16, true)
            ],
            "second-mic",
            "second-speaker");

        viewModel.RefreshAudioDevicesCommand.Execute(null);

        Assert.Contains(viewModel.Audio.AvailableMicrophones, device => device.DeviceId == "second-mic");
        Assert.Contains(viewModel.Audio.AvailableMonitorDevices, device => device.DeviceId == "second-speaker");
        Assert.Equal("second-mic", viewModel.Audio.SelectedMicrophoneDevice);
        Assert.Equal("second-speaker", viewModel.Audio.SelectedMonitorDevice);
        Assert.Contains("输入 1 个，输出 1 个", viewModel.Audio.DeviceStatusText);
    }

    [Fact]
    public void Relay_commands_start_and_stop_translation_channel_on_request()
    {
        var runtimeService = new RecordingRuntimeService();
        var viewModel = new SettingsViewModel(
            new OverlayViewModel(),
            new InMemorySettingsStore(AppSettings.Default),
            new InMemorySecretStore(SecretSettings.Empty),
            null,
            runtimeService);

        Assert.False(viewModel.IsRelayRunning);
        Assert.False(viewModel.IsSpeechRecognitionRunning);
        Assert.Equal("同传已停止", viewModel.RelayStateText);
        Assert.Equal("系统字幕已停止", viewModel.SpeechRecognitionStateText);
        Assert.True(viewModel.StartRelayCommand.CanExecute(null));
        Assert.False(viewModel.StopRelayCommand.CanExecute(null));
        Assert.True(viewModel.StartSpeechRecognitionCommand.CanExecute(null));
        Assert.False(viewModel.StopSpeechRecognitionCommand.CanExecute(null));

        viewModel.StartRelayCommand.Execute(null);

        Assert.True(viewModel.IsRelayRunning);
        Assert.False(viewModel.IsSpeechRecognitionRunning);
        Assert.Equal(1, runtimeService.GetStartCount(AudioChannelId.Microphone));
        Assert.Equal(0, runtimeService.GetStartCount(AudioChannelId.Monitor));
        Assert.Equal("同传运行中", viewModel.RelayStateText);
        Assert.False(viewModel.StartRelayCommand.CanExecute(null));
        Assert.True(viewModel.StopRelayCommand.CanExecute(null));
        Assert.True(viewModel.StartSpeechRecognitionCommand.CanExecute(null));

        viewModel.StopRelayCommand.Execute(null);

        Assert.False(viewModel.IsRelayRunning);
        Assert.False(viewModel.IsSpeechRecognitionRunning);
        Assert.Equal(1, runtimeService.GetStopCount(AudioChannelId.Microphone));
        Assert.Equal(0, runtimeService.GetStopCount(AudioChannelId.Monitor));
        Assert.Equal("同传已停止", viewModel.RelayStateText);
        Assert.True(viewModel.StartRelayCommand.CanExecute(null));
        Assert.False(viewModel.StopRelayCommand.CanExecute(null));
    }

    [Fact]
    public void Speech_recognition_commands_start_and_stop_monitor_channel_on_request()
    {
        var runtimeService = new RecordingRuntimeService();
        var viewModel = new SettingsViewModel(
            new OverlayViewModel(),
            new InMemorySettingsStore(AppSettings.Default),
            new InMemorySecretStore(SecretSettings.Empty),
            null,
            runtimeService);

        viewModel.StartSpeechRecognitionCommand.Execute(null);

        Assert.False(viewModel.IsRelayRunning);
        Assert.True(viewModel.IsSpeechRecognitionRunning);
        Assert.Equal(0, runtimeService.GetStartCount(AudioChannelId.Microphone));
        Assert.Equal(1, runtimeService.GetStartCount(AudioChannelId.Monitor));
        Assert.Equal("系统字幕运行中", viewModel.SpeechRecognitionStateText);
        Assert.True(viewModel.StartRelayCommand.CanExecute(null));
        Assert.False(viewModel.StartSpeechRecognitionCommand.CanExecute(null));
        Assert.True(viewModel.StopSpeechRecognitionCommand.CanExecute(null));

        viewModel.StopSpeechRecognitionCommand.Execute(null);

        Assert.False(viewModel.IsSpeechRecognitionRunning);
        Assert.Equal(0, runtimeService.GetStopCount(AudioChannelId.Microphone));
        Assert.Equal(1, runtimeService.GetStopCount(AudioChannelId.Monitor));
        Assert.Equal("系统字幕已停止", viewModel.SpeechRecognitionStateText);
        Assert.True(viewModel.StartSpeechRecognitionCommand.CanExecute(null));
        Assert.False(viewModel.StopSpeechRecognitionCommand.CanExecute(null));
    }

    [Fact]
    public async Task LoadAsync_prefers_real_default_device_over_stale_saved_device()
    {
        var settings = AppSettings.Default with
        {
            Audio = AppSettings.Default.Audio with
            {
                MicrophoneDeviceId = "Default Microphone",
                MonitorRenderDeviceId = "Default Speakers",
                TtsOutputDeviceId = "Default Speaker"
            }
        };
        var audioDevices = new InMemoryAudioDeviceService(
            captureDevices:
            [
                AudioMappingHelpers.ToDeviceInfo("real-mic", "真实麦克风", null, DataFlow.Capture, true, 1, 16000, 16, true)
            ],
            renderDevices:
            [
                AudioMappingHelpers.ToDeviceInfo("real-speaker", "真实扬声器", null, DataFlow.Render, true, 2, 48000, 16, true)
            ],
            defaultCaptureDeviceId: "real-mic",
            defaultRenderDeviceId: "real-speaker");
        var viewModel = new SettingsViewModel(
            new OverlayViewModel(),
            new InMemorySettingsStore(settings),
            new InMemorySecretStore(SecretSettings.Empty),
            audioDevices);

        await viewModel.LoadAsync();

        Assert.Equal("real-mic", viewModel.Audio.SelectedMicrophoneDevice);
        Assert.Equal("real-speaker", viewModel.Audio.SelectedMonitorDevice);
        Assert.Equal("real-speaker", viewModel.Audio.SelectedTtsOutputDevice);
        Assert.DoesNotContain(viewModel.Audio.AvailableMicrophones, device => device.DeviceId == "Default Microphone");
    }

    [Fact]
    public async Task Diagnostic_commands_call_configured_diagnostics_service()
    {
        var diagnostics = new RecordingDiagnosticsService();
        var viewModel = new SettingsViewModel(
            new OverlayViewModel(),
            new InMemorySettingsStore(AppSettings.Default),
            new InMemorySecretStore(SecretSettings.Empty),
            null,
            null,
            diagnostics);

        await viewModel.Translation.TestConnectionAsync();
        await viewModel.Translation.TestFunctionAsync();
        await viewModel.GameCaption.TestConnectionAsync();
        await viewModel.GameCaption.TestFunctionAsync();
        await viewModel.SpeechRecognition.TestConnectionAsync();
        await viewModel.SpeechRecognition.TestFunctionAsync();

        Assert.Equal("translation connection ok", viewModel.Translation.ConnectionTestStatus);
        Assert.Equal("translation function ok", viewModel.Translation.FunctionTestStatus);
        Assert.Equal("game caption connection ok", viewModel.GameCaption.ConnectionTestStatus);
        Assert.Equal("game caption function ok", viewModel.GameCaption.FunctionTestStatus);
        Assert.Equal("recognition connection ok", viewModel.SpeechRecognition.ConnectionTestStatus);
        Assert.Equal("recognition function ok", viewModel.SpeechRecognition.FunctionTestStatus);
        Assert.Equal(1, diagnostics.TranslationConnectionCount);
        Assert.Equal(1, diagnostics.TranslationFunctionCount);
        Assert.Equal(1, diagnostics.GameCaptionConnectionCount);
        Assert.Equal(1, diagnostics.GameCaptionFunctionCount);
        Assert.Equal(1, diagnostics.RecognitionConnectionCount);
        Assert.Equal(1, diagnostics.RecognitionFunctionCount);
    }

    private sealed class InMemorySettingsStore : ISettingsStore
    {
        private readonly AppSettings _settings;

        public InMemorySettingsStore(AppSettings settings)
        {
            _settings = settings;
        }

        public AppSettings? SavedSettings { get; private set; }

        public ValueTask<AppSettings> LoadSettingsAsync(CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult(_settings);
        }

        public ValueTask SaveSettingsAsync(AppSettings settings, CancellationToken cancellationToken = default)
        {
            SavedSettings = settings;
            return ValueTask.CompletedTask;
        }
    }

    private sealed class InMemorySecretStore : ISecretStore
    {
        private readonly SecretSettings _secrets;

        public InMemorySecretStore(SecretSettings secrets)
        {
            _secrets = secrets;
        }

        public SecretSettings? SavedSecrets { get; private set; }

        public ValueTask<SecretSettings> LoadSecretsAsync(CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult(_secrets);
        }

        public ValueTask SaveSecretsAsync(SecretSettings secrets, CancellationToken cancellationToken = default)
        {
            SavedSecrets = secrets;
            return ValueTask.CompletedTask;
        }
    }

    private sealed class RecordingRuntimeService : IAppRuntimeService
    {
        private readonly HashSet<AudioChannelId> _runningChannels = [];
        private readonly Dictionary<AudioChannelId, int> _startCounts = [];
        private readonly Dictionary<AudioChannelId, int> _stopCounts = [];

        public event EventHandler<ChannelRuntimeState> ChannelStateChanged = delegate { };

        public bool IsRunning => _runningChannels.Count > 0;

        public int StartCount { get; private set; }

        public int StopCount { get; private set; }

        public int RestartCount { get; private set; }

        public bool IsChannelRunning(AudioChannelId channelId)
        {
            return _runningChannels.Contains(channelId);
        }

        public int GetStartCount(AudioChannelId channelId)
        {
            return _startCounts.GetValueOrDefault(channelId);
        }

        public int GetStopCount(AudioChannelId channelId)
        {
            return _stopCounts.GetValueOrDefault(channelId);
        }

        public Task StartChannelAsync(
            AudioChannelId channelId,
            CancellationToken cancellationToken = default)
        {
            _startCounts[channelId] = GetStartCount(channelId) + 1;
            _runningChannels.Add(channelId);
            return Task.CompletedTask;
        }

        public Task StopChannelAsync(
            AudioChannelId channelId,
            CancellationToken cancellationToken = default)
        {
            _stopCounts[channelId] = GetStopCount(channelId) + 1;
            _runningChannels.Remove(channelId);
            return Task.CompletedTask;
        }

        public Task StartRelayAsync(CancellationToken cancellationToken = default)
        {
            StartCount++;
            _runningChannels.Add(AudioChannelId.Microphone);
            _runningChannels.Add(AudioChannelId.Monitor);
            return Task.CompletedTask;
        }

        public Task StopRelayAsync(CancellationToken cancellationToken = default)
        {
            StopCount++;
            _runningChannels.Clear();
            return Task.CompletedTask;
        }

        public Task RestartAsync(CancellationToken cancellationToken = default)
        {
            RestartCount++;
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingDiagnosticsService : IRelayDiagnosticsService
    {
        public int TranslationConnectionCount { get; private set; }
        public int TranslationFunctionCount { get; private set; }
        public int GameCaptionConnectionCount { get; private set; }
        public int GameCaptionFunctionCount { get; private set; }
        public int RecognitionConnectionCount { get; private set; }
        public int RecognitionFunctionCount { get; private set; }

        public Task<string> TestTranslationConnectionAsync(
            TranslationSettingsViewModel translation,
            CancellationToken cancellationToken = default)
        {
            TranslationConnectionCount++;
            return Task.FromResult("translation connection ok");
        }

        public Task<string> TestTranslationFunctionAsync(
            TranslationSettingsViewModel translation,
            AudioSettingsViewModel audio,
            CancellationToken cancellationToken = default)
        {
            TranslationFunctionCount++;
            return Task.FromResult("translation function ok");
        }

        public Task<string> TestGameCaptionConnectionAsync(
            TranslationSettingsViewModel translation,
            GameCaptionSettingsViewModel gameCaption,
            CancellationToken cancellationToken = default)
        {
            GameCaptionConnectionCount++;
            return Task.FromResult("game caption connection ok");
        }

        public Task<string> TestGameCaptionFunctionAsync(
            TranslationSettingsViewModel translation,
            GameCaptionSettingsViewModel gameCaption,
            AudioSettingsViewModel audio,
            CancellationToken cancellationToken = default)
        {
            GameCaptionFunctionCount++;
            return Task.FromResult("game caption function ok");
        }

        public Task<string> TestSpeechRecognitionConnectionAsync(
            SpeechRecognitionSettingsViewModel speechRecognition,
            CancellationToken cancellationToken = default)
        {
            RecognitionConnectionCount++;
            return Task.FromResult("recognition connection ok");
        }

        public Task<string> TestSpeechRecognitionFunctionAsync(
            SpeechRecognitionSettingsViewModel speechRecognition,
            AudioSettingsViewModel audio,
            CancellationToken cancellationToken = default)
        {
            RecognitionFunctionCount++;
            return Task.FromResult("recognition function ok");
        }
    }

    private sealed class InMemoryAudioDeviceService : INaudioDeviceService
    {
        private readonly IReadOnlyList<AudioDeviceInfo> _captureDevices;
        private readonly IReadOnlyList<AudioDeviceInfo> _renderDevices;
        private readonly string? _defaultCaptureDeviceId;
        private readonly string? _defaultRenderDeviceId;

        public InMemoryAudioDeviceService(
            IReadOnlyList<AudioDeviceInfo> captureDevices,
            IReadOnlyList<AudioDeviceInfo> renderDevices,
            string? defaultCaptureDeviceId,
            string? defaultRenderDeviceId)
        {
            _captureDevices = captureDevices;
            _renderDevices = renderDevices;
            _defaultCaptureDeviceId = defaultCaptureDeviceId;
            _defaultRenderDeviceId = defaultRenderDeviceId;
        }

        public Task<IReadOnlyList<AudioDeviceInfo>> GetCaptureDevicesAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_captureDevices);
        }

        public Task<IReadOnlyList<AudioDeviceInfo>> GetRenderDevicesAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_renderDevices);
        }

        public Task<string?> GetDefaultCaptureDeviceIdAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_defaultCaptureDeviceId);
        }

        public Task<string?> GetDefaultRenderDeviceIdAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_defaultRenderDeviceId);
        }

        public Task<MMDevice?> GetCaptureDeviceAsync(string? deviceId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<MMDevice?>(null);
        }

        public Task<MMDevice?> GetRenderDeviceAsync(string? deviceId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<MMDevice?>(null);
        }
    }

    private sealed class MutableAudioDeviceService : INaudioDeviceService
    {
        private IReadOnlyList<AudioDeviceInfo> _captureDevices = [];
        private IReadOnlyList<AudioDeviceInfo> _renderDevices = [];
        private string? _defaultCaptureDeviceId;
        private string? _defaultRenderDeviceId;

        public void SetDevices(
            IReadOnlyList<AudioDeviceInfo> captureDevices,
            IReadOnlyList<AudioDeviceInfo> renderDevices,
            string? defaultCaptureDeviceId,
            string? defaultRenderDeviceId)
        {
            _captureDevices = captureDevices;
            _renderDevices = renderDevices;
            _defaultCaptureDeviceId = defaultCaptureDeviceId;
            _defaultRenderDeviceId = defaultRenderDeviceId;
        }

        public Task<IReadOnlyList<AudioDeviceInfo>> GetCaptureDevicesAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_captureDevices);
        }

        public Task<IReadOnlyList<AudioDeviceInfo>> GetRenderDevicesAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_renderDevices);
        }

        public Task<string?> GetDefaultCaptureDeviceIdAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_defaultCaptureDeviceId);
        }

        public Task<string?> GetDefaultRenderDeviceIdAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_defaultRenderDeviceId);
        }

        public Task<MMDevice?> GetCaptureDeviceAsync(string? deviceId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<MMDevice?>(null);
        }

        public Task<MMDevice?> GetRenderDeviceAsync(string? deviceId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<MMDevice?>(null);
        }
    }
}
