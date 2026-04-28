using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Extensions.Logging;

namespace GameSubRelay.Infrastructure.Hotkeys;

public sealed class GlobalHotkeyService : IGlobalHotkeyService
{
    private const int WmHotkey = 0x0312;

    private readonly Dictionary<string, RegisteredHotkey> _registrationsByName = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<int, Action> _handlersById = [];
    private readonly ILogger<GlobalHotkeyService>? _logger;
    private HotkeyMessageWindow? _window;
    private int _nextId;
    private bool _started;

    public GlobalHotkeyService(ILogger<GlobalHotkeyService>? logger = null)
    {
        _logger = logger;
    }

    public ValueTask StartAsync()
    {
        if (_started)
        {
            return ValueTask.CompletedTask;
        }

        _window = new HotkeyMessageWindow(DispatchHotkey);
        _window.CreateHandle(new CreateParams());
        _started = true;
        _logger?.LogInformation("Global hotkey service started.");
        return ValueTask.CompletedTask;
    }

    public ValueTask StopAsync()
    {
        foreach (var registration in _registrationsByName.Values)
        {
            UnregisterHotKey(_window?.Handle ?? IntPtr.Zero, registration.Id);
        }

        _started = false;
        _registrationsByName.Clear();
        _handlersById.Clear();
        _window?.DestroyHandle();
        _window = null;
        _logger?.LogInformation("Global hotkey service stopped.");
        return ValueTask.CompletedTask;
    }

    public Task RegisterAsync(GlobalHotkeyBinding binding, Action callback)
    {
        if (!_started)
        {
            throw new InvalidOperationException("Hotkey service is not started.");
        }

        if (_window is null)
        {
            throw new InvalidOperationException("Hotkey message window is not available.");
        }

        if (!GlobalHotkeyMapping.TryCreate(binding.Gesture, out var mapping, out var error) || mapping is null)
        {
            throw new ArgumentException(error, nameof(binding));
        }

        callback = callback ?? throw new ArgumentNullException(nameof(callback));

        if (_registrationsByName.Remove(binding.Name, out var existing))
        {
            _handlersById.Remove(existing.Id);
            UnregisterHotKey(_window.Handle, existing.Id);
        }

        var id = ++_nextId;
        if (!RegisterHotKey(_window.Handle, id, mapping.Modifiers, mapping.VirtualKey))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), $"Failed to register hotkey {binding}.");
        }

        _registrationsByName[binding.Name] = new RegisteredHotkey(id, binding);
        _handlersById[id] = callback;
        return Task.CompletedTask;
    }

    public ValueTask DisposeAsync()
    {
        return StopAsync();
    }

    private void DispatchHotkey(int id)
    {
        if (_handlersById.TryGetValue(id, out var callback))
        {
            callback();
        }
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    private sealed record RegisteredHotkey(int Id, GlobalHotkeyBinding Binding);

    private sealed class HotkeyMessageWindow : NativeWindow
    {
        private readonly Action<int> _dispatch;

        public HotkeyMessageWindow(Action<int> dispatch)
        {
            _dispatch = dispatch;
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WmHotkey)
            {
                _dispatch(m.WParam.ToInt32());
                return;
            }

            base.WndProc(ref m);
        }
    }
}
