using System.Windows;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using GameSubRelay.App.Overlay;
using GameSubRelay.App.ViewModels;
using GameSubRelay.App.Runtime;
using GameSubRelay.App.Logging;
using GameSubRelay.Core.Captions;
using GameSubRelay.Infrastructure.Hotkeys;
using GameSubRelay.Infrastructure.Runtime;

namespace GameSubRelay.App;

public partial class App : Application
{
    private IHost? _host;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _host = AppHost.BuildHost(e.Args);

        var logger = _host.Services.GetRequiredService<ILogger<App>>();
        var runtimeService = _host.Services.GetRequiredService<IAppRuntimeService>();
        var hotkeyService = _host.Services.GetRequiredService<IGlobalHotkeyService>();
        var settingsViewModel = _host.Services.GetRequiredService<SettingsViewModel>();
        var overlayViewModel = _host.Services.GetRequiredService<OverlayViewModel>();
        var captionStore = _host.Services.GetRequiredService<CaptionStore>();
        settingsViewModel.AttachRuntimeService(runtimeService);

        logger.LogInformation("GameSubRelay starting. Log file: {LogFile}", AppLogPaths.DefaultLogFilePath);

        captionStore.CaptionLinesChanged += (_, args) =>
        {
            Dispatcher.Invoke(() => overlayViewModel.ApplyCaptionSnapshot(args.Lines));
        };

        runtimeService.ChannelStateChanged += (sender, state) =>
        {
            logger.LogInformation(
                "Channel '{ChannelId}' status is {Status}",
                state.ChannelId,
                state.Status);
            Dispatcher.Invoke(() =>
            {
                settingsViewModel.ApplyRuntimeChannelState(state);
                settingsViewModel.SetStatus(state.ErrorMessage is null
                    ? $"{state.ChannelId}：{state.Status}"
                    : $"{state.ChannelId}：{state.Status} - {state.ErrorMessage}");
            });
        };

        settingsViewModel.SettingsSaved += async (_, _) =>
        {
            try
            {
                if (!runtimeService.IsRunning)
                {
                    logger.LogInformation("Settings saved while relay is stopped. Channels remain stopped.");
                    settingsViewModel.SetStatus("配置已保存；语音通道未启动");
                    return;
                }

                settingsViewModel.SetStatus("配置已保存，正在重启语音通道...");
                logger.LogInformation("Settings saved. Restarting runtime channels.");
                await runtimeService.RestartAsync();
                settingsViewModel.SetStatus(runtimeService.IsRunning
                    ? "语音通道已按当前配置重启"
                    : "语音通道重启失败，请查看日志");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to restart runtime after settings save.");
                settingsViewModel.SetStatus($"重启语音通道失败：{ex.Message}");
            }
        };

        try
        {
            await settingsViewModel.LoadAsync();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to load saved settings.");
        }

        await _host.StartAsync();

        var overlayWindow = _host.Services.GetRequiredService<OverlayWindow>();
        var mainWindow = _host.Services.GetRequiredService<MainWindow>();

        MainWindow = mainWindow;
        mainWindow.Show();
        mainWindow.Activate();

        try
        {
            overlayWindow.Show();
            overlayWindow.UpdateWindowVisibility();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to initialize overlay window.");
        }

        try
        {
            await hotkeyService.StartAsync();
            await hotkeyService.RegisterAsync(
                new GlobalHotkeyBinding("ToggleOverlay", settingsViewModel.Hotkeys.ToggleOverlayHotkey),
                () => Dispatcher.Invoke(() =>
                {
                    overlayViewModel.ToggleOverlayVisibility();
                    overlayWindow.UpdateWindowVisibility();
                    logger.LogInformation("ToggleOverlay hotkey triggered");
                }));
            await hotkeyService.RegisterAsync(
                new GlobalHotkeyBinding("ToggleEditMode", settingsViewModel.Hotkeys.ToggleEditModeHotkey),
                () => Dispatcher.Invoke(() =>
                {
                    overlayViewModel.ToggleEditMode();
                    overlayWindow.ApplyFromViewModel();
                    logger.LogInformation("ToggleEditMode hotkey triggered");
                }));
            await hotkeyService.RegisterAsync(
                new GlobalHotkeyBinding("ClearCaptions", settingsViewModel.Hotkeys.ClearCaptionsHotkey),
                () => Dispatcher.Invoke(() =>
                {
                    captionStore.Clear();
                    overlayViewModel.ClearCaptions();
                    logger.LogInformation("ClearCaptions hotkey triggered");
                }));
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Global hotkeys are disabled.");
        }

    }

    protected override async void OnExit(ExitEventArgs e)
    {
        if (_host is not null)
        {
            await _host.StopAsync();
            _host.Dispose();
        }

        base.OnExit(e);
    }
}
