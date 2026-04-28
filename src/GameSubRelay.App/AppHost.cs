using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
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
            .ConfigureServices((context, services) =>
            {
                services.Configure<RuntimeOptions>(context.Configuration.GetSection("Runtime"));

                services.AddSingleton<OverlayViewModel>();
                services.AddSingleton<SettingsViewModel>();
                services.AddSingleton<MainWindow>();
                services.AddSingleton<OverlayWindow>();
                services.AddSingleton<CaptionStore>();
                services.AddSingleton<ISettingsStore, JsonSettingsStore>();
                services.AddSingleton<ISecretStore, DpapiSecretStore>();
                services.AddSingleton<INaudioDeviceService, NaudioDeviceService>();

                services.AddSingleton<IGlobalHotkeyService, GlobalHotkeyService>();
                services.AddSingleton<ITranslationChannelWorkerFactory, AppTranslationChannelWorkerFactory>();

                services.AddSingleton<AppRuntimeService>();
                services.AddSingleton<IAppRuntimeService>(sp => sp.GetRequiredService<AppRuntimeService>());
                services.AddHostedService(sp => sp.GetRequiredService<AppRuntimeService>());
            })
            .Build();
    }

    private sealed record RuntimeOptions(bool EnableMicrophoneChannel = true, bool EnableMonitorChannel = true);
}
