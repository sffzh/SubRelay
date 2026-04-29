using System;
using System.Collections.ObjectModel;
using GameSubRelay.Core.Configuration;
using GameSubRelay.Infrastructure.Audio;
using GameSubRelay.Infrastructure.Runtime;

namespace GameSubRelay.App.ViewModels;

public sealed record AudioDeviceOption(string DeviceId, string DisplayText);

public sealed class AudioSettingsViewModel : ViewModelBase
{
    private string _selectedMicrophoneDevice = string.Empty;
    private string _selectedMonitorDevice = string.Empty;
    private string _selectedTtsOutputDevice = string.Empty;
    private bool _microphoneEnabled = true;
    private bool _monitorEnabled = true;
    private bool _showAdvancedDeviceInfo = true;
    private string _deviceStatusText = "设备未刷新";

    public ObservableCollection<AudioDeviceOption> AvailableMicrophones { get; } = new();

    public ObservableCollection<AudioDeviceOption> AvailableMonitorDevices { get; } = new();

    public ObservableCollection<AudioDeviceOption> AvailableTtsOutputDevices { get; } = new();

    public string DeviceStatusText
    {
        get => _deviceStatusText;
        set => SetProperty(ref _deviceStatusText, value);
    }

    public string SelectedMicrophoneDevice
    {
        get => _selectedMicrophoneDevice;
        set => SetProperty(ref _selectedMicrophoneDevice, value);
    }

    public string SelectedMonitorDevice
    {
        get => _selectedMonitorDevice;
        set
        {
            if (SetProperty(ref _selectedMonitorDevice, value))
            {
                OnPropertyChanged(nameof(IsDeviceFeedbackRisk));
            }
        }
    }

    public string SelectedTtsOutputDevice
    {
        get => _selectedTtsOutputDevice;
        set
        {
            if (SetProperty(ref _selectedTtsOutputDevice, value))
            {
                OnPropertyChanged(nameof(IsDeviceFeedbackRisk));
            }
        }
    }

    public bool MicrophoneEnabled
    {
        get => _microphoneEnabled;
        set => SetProperty(ref _microphoneEnabled, value);
    }

    public bool MonitorEnabled
    {
        get => _monitorEnabled;
        set => SetProperty(ref _monitorEnabled, value);
    }

    public bool ShowAdvancedDeviceInfo
    {
        get => _showAdvancedDeviceInfo;
        set => SetProperty(ref _showAdvancedDeviceInfo, value);
    }

    public bool IsDeviceFeedbackRisk
    {
        get => string.Equals(SelectedMonitorDevice, SelectedTtsOutputDevice, StringComparison.Ordinal);
    }
}

public sealed record ProviderOption(string Code, string Name)
{
    public string DisplayText => $"{Name} ({Code})";
}

public sealed record LanguageOption(string Code, string Name)
{
    public string DisplayText => $"{Name} ({Code})";
}

public sealed class TranslationSettingsViewModel : ViewModelBase
{
    private string _provider = "VolcengineAstTranslate";
    private string _sourceLanguage = "en";
    private string _targetLanguage = "zh";
    private string _region = "cn-north-1";
    private string _accessKeyId = string.Empty;
    private string _secretAccessKey = string.Empty;
    private string _ttsToken = string.Empty;
    private string _ttsAppId = string.Empty;
    private string _ttsCluster = "volcengine";
    private string _connectionTestStatus = "未测试";

    public ObservableCollection<ProviderOption> Providers { get; } = new()
    {
        new("VolcengineAstTranslate", "火山语音同传 + 大模型流式识别")
    };

    public ObservableCollection<LanguageOption> AvailableSourceLanguages { get; } = CreateVolcengineAstLanguages();

    public ObservableCollection<LanguageOption> AvailableTargetLanguages { get; } = CreateVolcengineAstLanguages();

    public string Provider
    {
        get => _provider;
        set => SetProperty(ref _provider, value);
    }

    public string SourceLanguage
    {
        get => _sourceLanguage;
        set => SetProperty(ref _sourceLanguage, value);
    }

    public string TargetLanguage
    {
        get => _targetLanguage;
        set => SetProperty(ref _targetLanguage, value);
    }

    public string Region
    {
        get => _region;
        set => SetProperty(ref _region, value);
    }

    public string AccessKeyId
    {
        get => _accessKeyId;
        set => SetProperty(ref _accessKeyId, value);
    }

    public string SecretAccessKey
    {
        get => _secretAccessKey;
        set => SetProperty(ref _secretAccessKey, value);
    }

    public string TtsToken
    {
        get => _ttsToken;
        set => SetProperty(ref _ttsToken, value);
    }

    public string TtsAppId
    {
        get => _ttsAppId;
        set => SetProperty(ref _ttsAppId, value);
    }

    public string TtsCluster
    {
        get => _ttsCluster;
        set => SetProperty(ref _ttsCluster, value);
    }

    public string ConnectionTestStatus
    {
        get => _connectionTestStatus;
        private set => SetProperty(ref _connectionTestStatus, value);
    }

    public RelayCommand TestConnectionCommand { get; }

    public TranslationSettingsViewModel()
    {
        TestConnectionCommand = new RelayCommand(TestConnection);
    }

    private void TestConnection()
    {
        ConnectionTestStatus = string.IsNullOrWhiteSpace(AccessKeyId) || string.IsNullOrWhiteSpace(SecretAccessKey)
            ? "请填写火山同声传译 APP ID 和 Access Token"
            : "凭据已填写；AST 与 ASR 连通性会在通道启动时验证";
    }

    private static ObservableCollection<LanguageOption> CreateVolcengineAstLanguages() => new()
    {
        new("zh", "中文"),
        new("en", "英语"),
        new("ja", "日语"),
        new("id", "印尼语"),
        new("es", "西班牙语"),
        new("pt", "葡萄牙语"),
        new("de", "德语"),
        new("fr", "法语"),
        new("zhen", "中英互译")
    };
}

public sealed class TtsSettingsViewModel : ViewModelBase
{
    private bool _ttsEnabled;
    private bool _ttsForMicrophone;
    private bool _ttsForMonitor;
    private string _strategyDescription = "队列最大长度 3；满后丢弃最旧项。";

    public bool TtsEnabled
    {
        get => _ttsEnabled;
        set => SetProperty(ref _ttsEnabled, value);
    }

    public bool TtsForMicrophone
    {
        get => _ttsForMicrophone;
        set => SetProperty(ref _ttsForMicrophone, value);
    }

    public bool TtsForMonitor
    {
        get => _ttsForMonitor;
        set => SetProperty(ref _ttsForMonitor, value);
    }

    public string QueueStrategy
    {
        get => _strategyDescription;
        set => SetProperty(ref _strategyDescription, value);
    }
}

public sealed class HotkeySettingsViewModel : ViewModelBase
{
    private string _toggleOverlayHotkey = "Ctrl+Alt+S";
    private string _toggleEditModeHotkey = "Ctrl+Alt+E";
    private string _clearCaptionsHotkey = "Ctrl+Alt+C";
    private bool _hasHotkeyConflict;

    public string ToggleOverlayHotkey
    {
        get => _toggleOverlayHotkey;
        set => SetProperty(ref _toggleOverlayHotkey, value);
    }

    public string ToggleEditModeHotkey
    {
        get => _toggleEditModeHotkey;
        set => SetProperty(ref _toggleEditModeHotkey, value);
    }

    public string ClearCaptionsHotkey
    {
        get => _clearCaptionsHotkey;
        set => SetProperty(ref _clearCaptionsHotkey, value);
    }

    public bool HasConflict
    {
        get => _hasHotkeyConflict;
        set => SetProperty(ref _hasHotkeyConflict, value);
    }
}

public sealed class SettingsViewModel : ViewModelBase
{
    private string _title = "GameSubRelay 设置";
    private readonly OverlayViewModel _overlayViewModel;
    private readonly ISettingsStore _settingsStore;
    private readonly ISecretStore _secretStore;
    private readonly INaudioDeviceService? _audioDeviceService;
    private IAppRuntimeService? _runtimeService;
    private string _statusText = string.Empty;
    private bool _isRelayRunning;
    private bool _isRelayBusy;

    public SettingsViewModel(
        OverlayViewModel overlayViewModel,
        ISettingsStore settingsStore,
        ISecretStore secretStore)
        : this(overlayViewModel, settingsStore, secretStore, null, null)
    {
    }

    public SettingsViewModel(
        OverlayViewModel overlayViewModel,
        ISettingsStore settingsStore,
        ISecretStore secretStore,
        INaudioDeviceService? audioDeviceService)
        : this(overlayViewModel, settingsStore, secretStore, audioDeviceService, null)
    {
    }

    public SettingsViewModel(
        OverlayViewModel overlayViewModel,
        ISettingsStore settingsStore,
        ISecretStore secretStore,
        INaudioDeviceService? audioDeviceService,
        IAppRuntimeService? runtimeService)
    {
        _overlayViewModel = overlayViewModel;
        _settingsStore = settingsStore;
        _secretStore = secretStore;
        _audioDeviceService = audioDeviceService;
        _runtimeService = runtimeService;
        _isRelayRunning = runtimeService?.IsRunning ?? false;
        Audio = new AudioSettingsViewModel();
        Translation = new TranslationSettingsViewModel();
        Overlay = overlayViewModel.Settings;
        Tts = new TtsSettingsViewModel();
        Hotkeys = new HotkeySettingsViewModel();
        ApplyCommand = new RelayCommand(Apply);
        SaveCommand = new RelayCommand(async () => await SaveCommandAsync());
        StartRelayCommand = new RelayCommand(async () => await StartRelayCommandAsync(), CanStartRelay);
        StopRelayCommand = new RelayCommand(async () => await StopRelayCommandAsync(), CanStopRelay);
        RefreshAudioDevicesCommand = new RelayCommand(async () => await RefreshAudioDevicesCommandAsync());
        OpenOverlayCommand = new RelayCommand(OpenOverlay);
        HideOverlayCommand = new RelayCommand(HideOverlay);
        ToggleOverlayLockCommand = new RelayCommand(ToggleOverlayLock);
    }

    public string WindowTitle
    {
        get => _title;
        set => SetProperty(ref _title, value);
    }

    public AudioSettingsViewModel Audio { get; }
    public TranslationSettingsViewModel Translation { get; }
    public OverlayRenderSettings Overlay { get; }
    public TtsSettingsViewModel Tts { get; }
    public HotkeySettingsViewModel Hotkeys { get; }

    public RelayCommand ApplyCommand { get; }
    public RelayCommand SaveCommand { get; }
    public RelayCommand StartRelayCommand { get; }
    public RelayCommand StopRelayCommand { get; }
    public RelayCommand RefreshAudioDevicesCommand { get; }
    public RelayCommand OpenOverlayCommand { get; }
    public RelayCommand HideOverlayCommand { get; }
    public RelayCommand ToggleOverlayLockCommand { get; }

    public event EventHandler? SettingsSaved;

    public string OverlayLockText => IsEditMode ? "锁定字幕浮层" : "解锁字幕浮层";

    public void AttachRuntimeService(IAppRuntimeService runtimeService)
    {
        _runtimeService = runtimeService ?? throw new ArgumentNullException(nameof(runtimeService));
        IsRelayRunning = runtimeService.IsRunning;
        RefreshRelayCommands();
    }

    public bool IsRelayRunning
    {
        get => _isRelayRunning;
        private set
        {
            if (SetProperty(ref _isRelayRunning, value))
            {
                OnPropertyChanged(nameof(RelayStateText));
                RefreshRelayCommands();
            }
        }
    }

    public bool IsRelayBusy
    {
        get => _isRelayBusy;
        private set
        {
            if (SetProperty(ref _isRelayBusy, value))
            {
                RefreshRelayCommands();
            }
        }
    }

    public string RelayStateText => IsRelayRunning ? "同传运行中" : "同传已停止";

    public string StatusText
    {
        get => _statusText;
        private set => SetProperty(ref _statusText, value);
    }

    public bool OverlayVisible
    {
        get => _overlayViewModel.IsVisible;
        set
        {
            _overlayViewModel.IsVisible = value;
            OnPropertyChanged();
        }
    }

    public bool IsEditMode
    {
        get => _overlayViewModel.IsEditMode;
        set
        {
            _overlayViewModel.IsEditMode = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(OverlayLockText));
        }
    }

    private void Apply()
    {
        if (string.Equals(Audio.SelectedMonitorDevice, Audio.SelectedTtsOutputDevice, StringComparison.Ordinal))
        {
            Audio.ShowAdvancedDeviceInfo = true;
        }
    }

    public async ValueTask LoadAsync(CancellationToken cancellationToken = default)
    {
        await RefreshAudioDevicesAsync(cancellationToken);

        var settings = await _settingsStore.LoadSettingsAsync(cancellationToken);
        var secrets = await _secretStore.LoadSecretsAsync(cancellationToken);

        ApplySettings(settings.Normalize(), secrets.Normalize());
        StatusText = "配置已加载";
    }

    public async ValueTask SaveAsync(CancellationToken cancellationToken = default)
    {
        Apply();

        await _settingsStore.SaveSettingsAsync(CreateSettings(), cancellationToken);
        await _secretStore.SaveSecretsAsync(CreateSecrets(), cancellationToken);
        StatusText = "配置已保存";
        SettingsSaved?.Invoke(this, EventArgs.Empty);
    }

    public void SetStatus(string statusText)
    {
        StatusText = statusText;
    }

    public async ValueTask RefreshAudioDevicesAsync(CancellationToken cancellationToken = default)
    {
        if (_audioDeviceService is null)
        {
            Audio.DeviceStatusText = "未启用设备枚举服务";
            return;
        }

        var captureDevices = await _audioDeviceService.GetCaptureDevicesAsync(cancellationToken);
        var renderDevices = await _audioDeviceService.GetRenderDevicesAsync(cancellationToken);
        var defaultCaptureDeviceId = await _audioDeviceService.GetDefaultCaptureDeviceIdAsync(cancellationToken);
        var defaultRenderDeviceId = await _audioDeviceService.GetDefaultRenderDeviceIdAsync(cancellationToken);

        ReplaceDeviceOptions(Audio.AvailableMicrophones, captureDevices, "未检测到录音设备");
        ReplaceDeviceOptions(Audio.AvailableMonitorDevices, renderDevices, "未检测到播放设备");
        ReplaceDeviceOptions(Audio.AvailableTtsOutputDevices, renderDevices, "未检测到同声输出设备");

        SelectDevice(Audio.AvailableMicrophones, defaultCaptureDeviceId, value => Audio.SelectedMicrophoneDevice = value, Audio.SelectedMicrophoneDevice);
        SelectDevice(Audio.AvailableMonitorDevices, defaultRenderDeviceId, value => Audio.SelectedMonitorDevice = value, Audio.SelectedMonitorDevice);
        SelectDevice(Audio.AvailableTtsOutputDevices, defaultRenderDeviceId, value => Audio.SelectedTtsOutputDevice = value, Audio.SelectedTtsOutputDevice);
        Audio.DeviceStatusText = $"已刷新设备：输入 {captureDevices.Count} 个，输出 {renderDevices.Count} 个";
    }

    private async Task RefreshAudioDevicesCommandAsync()
    {
        try
        {
            await RefreshAudioDevicesAsync();
        }
        catch (Exception ex)
        {
            Audio.DeviceStatusText = $"刷新设备失败：{ex.Message}";
        }
    }

    private async Task SaveCommandAsync()
    {
        try
        {
            await SaveAsync();
        }
        catch (Exception ex)
        {
            StatusText = $"保存失败：{ex.Message}";
        }
    }

    private async Task StartRelayCommandAsync()
    {
        if (_runtimeService is null)
        {
            StatusText = "运行时服务未启用";
            return;
        }

        IsRelayBusy = true;
        try
        {
            Apply();
            StatusText = "正在开始同声传译...";
            await _runtimeService.StartRelayAsync();
            IsRelayRunning = _runtimeService.IsRunning;
            StatusText = IsRelayRunning ? "同声传译已开始" : "同声传译启动失败，请查看日志";
        }
        catch (Exception ex)
        {
            IsRelayRunning = _runtimeService.IsRunning;
            StatusText = $"开始失败：{ex.Message}";
        }
        finally
        {
            IsRelayBusy = false;
        }
    }

    private async Task StopRelayCommandAsync()
    {
        if (_runtimeService is null)
        {
            StatusText = "运行时服务未启用";
            return;
        }

        IsRelayBusy = true;
        try
        {
            StatusText = "正在停止同声传译...";
            await _runtimeService.StopRelayAsync();
            IsRelayRunning = _runtimeService.IsRunning;
            StatusText = "同声传译已停止";
        }
        catch (Exception ex)
        {
            IsRelayRunning = _runtimeService.IsRunning;
            StatusText = $"停止失败：{ex.Message}";
        }
        finally
        {
            IsRelayBusy = false;
        }
    }

    private bool CanStartRelay()
    {
        return _runtimeService is not null && !IsRelayRunning && !IsRelayBusy;
    }

    private bool CanStopRelay()
    {
        return _runtimeService is not null && IsRelayRunning && !IsRelayBusy;
    }

    private void RefreshRelayCommands()
    {
        StartRelayCommand.RaiseCanExecuteChanged();
        StopRelayCommand.RaiseCanExecuteChanged();
    }

    private void OpenOverlay()
    {
        OverlayVisible = true;
        IsEditMode = true;
    }

    private void HideOverlay()
    {
        OverlayVisible = false;
    }

    private void ToggleOverlayLock()
    {
        if (!OverlayVisible)
        {
            OverlayVisible = true;
        }

        IsEditMode = !IsEditMode;
    }

    private void ApplySettings(AppSettings settings, SecretSettings secrets)
    {
        Audio.SelectedMicrophoneDevice = SelectSavedOrExisting(Audio.AvailableMicrophones, settings.Audio.MicrophoneDeviceId, Audio.SelectedMicrophoneDevice);
        Audio.SelectedMonitorDevice = SelectSavedOrExisting(Audio.AvailableMonitorDevices, settings.Audio.MonitorRenderDeviceId, Audio.SelectedMonitorDevice);
        Audio.SelectedTtsOutputDevice = SelectSavedOrExisting(Audio.AvailableTtsOutputDevices, settings.Audio.TtsOutputDeviceId, Audio.SelectedTtsOutputDevice);
        Audio.MicrophoneEnabled = settings.Audio.MicrophoneEnabled;
        Audio.MonitorEnabled = settings.Audio.MonitorEnabled;

        Translation.Provider = settings.Translation.Provider;
        Translation.SourceLanguage = settings.Translation.SourceLanguage;
        Translation.TargetLanguage = settings.Translation.TargetLanguage;
        Translation.Region = settings.Translation.Region;
        Translation.AccessKeyId = FirstNonEmpty(secrets.VolcengineAppKey, secrets.VolcengineAccessKeyId);
        Translation.SecretAccessKey = FirstNonEmpty(secrets.VolcengineAstAccessKey, secrets.VolcengineSecretAccessKey);
        Translation.TtsAppId = secrets.VolcengineTtsAppId;
        Translation.TtsToken = secrets.VolcengineTtsToken;
        Translation.TtsCluster = secrets.VolcengineTtsCluster;

        Overlay.Left = settings.Overlay.Left;
        Overlay.Top = settings.Overlay.Top;
        Overlay.Width = settings.Overlay.Width;
        Overlay.Height = settings.Overlay.Height;
        Overlay.Opacity = settings.Overlay.Opacity;
        Overlay.FontSize = settings.Overlay.FontSize;
        Overlay.MaxLines = settings.Overlay.MaxLines;
        OverlayVisible = settings.Overlay.Visible;

        Hotkeys.ToggleOverlayHotkey = settings.Hotkeys.ToggleOverlay;
        Hotkeys.ToggleEditModeHotkey = settings.Hotkeys.ToggleEditMode;
        Hotkeys.ClearCaptionsHotkey = settings.Hotkeys.ClearCaptions;

        Tts.TtsEnabled = settings.Tts.Enabled;
        Tts.TtsForMicrophone = settings.Tts.UseForMicrophone;
        Tts.TtsForMonitor = settings.Tts.UseForMonitor;
    }

    private AppSettings CreateSettings()
    {
        var tts = new TtsSettings(
            Tts.TtsEnabled,
            Tts.TtsForMicrophone,
            Tts.TtsForMonitor,
            TtsSettings.Default.MaxQueueLength);

        return new AppSettings(
            new AudioSettings(
                Audio.SelectedMicrophoneDevice,
                Audio.SelectedMonitorDevice,
                Audio.SelectedTtsOutputDevice,
                Audio.MicrophoneEnabled,
                Audio.MonitorEnabled,
                Tts.TtsEnabled,
                Tts.TtsForMicrophone,
                Tts.TtsForMonitor),
            new TranslationSettings(
                Translation.Provider,
                Translation.SourceLanguage,
                Translation.TargetLanguage,
                Translation.Region),
            new OverlaySettings(
                ToInt(Overlay.Left),
                ToInt(Overlay.Top),
                ToInt(Overlay.Width),
                ToInt(Overlay.Height),
                Overlay.Opacity,
                ToInt(Overlay.FontSize),
                Overlay.MaxLines,
                OverlayVisible),
            new HotkeySettings(
                Hotkeys.ToggleOverlayHotkey,
                Hotkeys.ToggleEditModeHotkey,
                Hotkeys.ClearCaptionsHotkey),
            tts);
    }

    private SecretSettings CreateSecrets()
    {
        return new SecretSettings(
            Translation.AccessKeyId,
            Translation.SecretAccessKey,
            Translation.AccessKeyId,
            Translation.SecretAccessKey,
            Translation.TtsAppId,
            Translation.TtsToken,
            Translation.TtsCluster);
    }

    private static void ReplaceDeviceOptions(
        ObservableCollection<AudioDeviceOption> target,
        IReadOnlyList<AudioDeviceInfo> devices,
        string emptyText)
    {
        target.Clear();

        foreach (var device in devices.OrderByDescending(device => device.IsDefault).ThenBy(device => device.Name, StringComparer.CurrentCultureIgnoreCase))
        {
            target.Add(new AudioDeviceOption(device.DeviceId, FormatDeviceName(device)));
        }

        if (target.Count == 0)
        {
            target.Add(new AudioDeviceOption(string.Empty, emptyText));
        }
    }

    private static void SelectDevice(
        ObservableCollection<AudioDeviceOption> options,
        string? preferredDeviceId,
        Action<string> setSelected,
        string currentSelection)
    {
        if (!string.IsNullOrWhiteSpace(currentSelection) &&
            options.Any(option => string.Equals(option.DeviceId, currentSelection, StringComparison.Ordinal)))
        {
            setSelected(currentSelection);
            return;
        }

        if (!string.IsNullOrWhiteSpace(preferredDeviceId) &&
            options.Any(option => string.Equals(option.DeviceId, preferredDeviceId, StringComparison.Ordinal)))
        {
            setSelected(preferredDeviceId);
            return;
        }

        setSelected(options.FirstOrDefault()?.DeviceId ?? string.Empty);
    }

    private static string SelectSavedOrExisting(
        ObservableCollection<AudioDeviceOption> options,
        string savedDeviceId,
        string currentSelection)
    {
        var hasRealOptions = options.Any(option => !string.IsNullOrWhiteSpace(option.DeviceId));
        if (!hasRealOptions)
        {
            return !string.IsNullOrWhiteSpace(savedDeviceId) ? savedDeviceId : currentSelection;
        }

        if (!string.IsNullOrWhiteSpace(savedDeviceId) &&
            options.Any(option => string.Equals(option.DeviceId, savedDeviceId, StringComparison.Ordinal)))
        {
            return savedDeviceId;
        }

        if (!string.IsNullOrWhiteSpace(currentSelection) &&
            options.Any(option => string.Equals(option.DeviceId, currentSelection, StringComparison.Ordinal)))
        {
            return currentSelection;
        }

        return options.FirstOrDefault()?.DeviceId ?? string.Empty;
    }

    private static string FormatDeviceName(AudioDeviceInfo device)
    {
        var name = device.IsDefault ? $"{device.Name}（默认）" : device.Name;
        return device.IsEnabled ? name : $"{name}（不可用）";
    }

    private static string FirstNonEmpty(params string[] values)
    {
        return values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;
    }

    private static int ToInt(double value)
    {
        return (int)Math.Round(value, MidpointRounding.AwayFromZero);
    }
}
