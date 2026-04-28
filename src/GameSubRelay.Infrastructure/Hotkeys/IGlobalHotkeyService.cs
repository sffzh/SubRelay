using System;
using System.Threading.Tasks;

namespace GameSubRelay.Infrastructure.Hotkeys;

public interface IGlobalHotkeyService : IAsyncDisposable
{
    ValueTask StartAsync();

    ValueTask StopAsync();

    Task RegisterAsync(GlobalHotkeyBinding binding, Action callback);
}

