using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using GameSubRelay.App.Diagnostics;
using GameSubRelay.App.Logging;
using GameSubRelay.App.Overlay;
using GameSubRelay.App.ViewModels;
using GameSubRelay.Core.Configuration;
using GameSubRelay.Infrastructure.Audio;
using GameSubRelay.Core.Captions;
using GameSubRelay.Infrastructure.Hotkeys;
using GameSubRelay.Infrastructure.Configuration;
using GameSubRelay.Infrastructure.Runtime;

namespace GameSubRelay.App.Runtime;

public static class AppHost
{
    public static IHost BuildHost(string[]? args)
    {
        return Host.CreateDefaultBuilder(args)
            .ConfigureLogging((context, logging) =>
            {
                logging.ClearProviders();
                logging.AddDebug();
                logging.AddConsole();
                logging.AddProvider(new FileLoggerProvider(AppLogPaths.DefaultLogFilePath));
                logging.SetMinimumLevel(ResolveMinimumLogLevel(context.Configuration));
            })
            .ConfigureServices((context, services) =>
            {
                services.Configure<RuntimeOptions>(context.Configuration.GetSection("Runtime"));

                services.AddSingleton<OverlayViewModel>();
                services.AddSingleton<MainWindow>();
                services.AddSingleton<OverlayWindow>();
                services.AddSingleton<CaptionStore>();
                services.AddSingleton<ISettingsStore, JsonSettingsStore>();
                services.AddSingleton<ISecretStore, DpapiSecretStore>();
                services.AddSingleton<INaudioDeviceService, NaudioDeviceService>();
                services.AddSingleton<IRelayDiagnosticsService, RelayDiagnosticsService>();
                services.AddSingleton(sp => new SettingsViewModel(
                    sp.GetRequiredService<OverlayViewModel>(),
                    sp.GetRequiredService<ISettingsStore>(),
                    sp.GetRequiredService<ISecretStore>(),
                    sp.GetRequiredService<INaudioDeviceService>(),
                    runtimeService: null,
                    diagnosticsService: sp.GetRequiredService<IRelayDiagnosticsService>()));

                services.AddSingleton<IGlobalHotkeyService, GlobalHotkeyService>();
                services.AddSingleton<IAudioChannelWorkerFactory, AppAudioChannelWorkerFactory>();

                services.AddSingleton<AppRuntimeService>();
                services.AddSingleton<IAppRuntimeService>(sp => sp.GetRequiredService<AppRuntimeService>());
                services.AddHostedService(sp => sp.GetRequiredService<AppRuntimeService>());
            })
            .Build();
    }

    private static LogLevel ResolveMinimumLogLevel(IConfiguration configuration)
    {
        var configuredLevel =
            configuration["SubRelay:LogLevel"] ??
            configuration["GameSubRelay:LogLevel"] ??
            configuration["Logging:LogLevel:Default"] ??
            Environment.GetEnvironmentVariable("SUBRELAY_LOG_LEVEL") ??
            Environment.GetEnvironmentVariable("GAMESUBRELAY_LOG_LEVEL");

        return Enum.TryParse<LogLevel>(configuredLevel, ignoreCase: true, out var level)
            ? level
            : LogLevel.Debug;
    }

    private sealed record RuntimeOptions(bool EnableMicrophoneChannel = true, bool EnableMonitorChannel = true);
}
