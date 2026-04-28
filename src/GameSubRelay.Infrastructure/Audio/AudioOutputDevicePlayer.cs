using NAudio.CoreAudioApi;
using NAudio.Wave;
using GameSubRelay.Infrastructure.Windows;

namespace GameSubRelay.Infrastructure.Audio;

public sealed class AudioOutputDevicePlayer : IAudioOutputPlayer
{
    private readonly INaudioDeviceService _deviceService;
    private readonly object _sync = new();

    private WasapiOut? _output;
    private BufferedWaveProvider? _bufferedProvider;
    private bool _isRunning;
    private bool _disposed;
    private string? _activeDeviceId;

    public AudioOutputDevicePlayer(
        INaudioDeviceService deviceService)
    {
        _deviceService = deviceService;
    }

    public bool IsRunning => _isRunning;

    public async Task StartAsync(string? renderDeviceId = null, CancellationToken cancellationToken = default)
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(AudioOutputDevicePlayer));
        }

        if (_isRunning && renderDeviceId == _activeDeviceId)
        {
            return;
        }

        lock (_sync)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(AudioOutputDevicePlayer));
            }

            StopLocked();
            _activeDeviceId = renderDeviceId;
        }

        var device = await _deviceService.GetRenderDeviceAsync(renderDeviceId, cancellationToken);
        if (device is null)
        {
            throw new InvalidOperationException("No available render device.");
        }

        lock (_sync)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(AudioOutputDevicePlayer));
            }

            _bufferedProvider = WasapiAudioHelpers.CreateMono16KhzPlaybackBuffer();

            _output = new WasapiOut(device, AudioClientShareMode.Shared, false, 50);

            _output.Init(_bufferedProvider);
            _output.Play();
            _isRunning = true;
        }
    }

    public async Task PlayAsync(byte[] pcm16Mono16Khz, CancellationToken cancellationToken = default)
    {
        if (pcm16Mono16Khz is null)
        {
            throw new ArgumentNullException(nameof(pcm16Mono16Khz));
        }

        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(AudioOutputDevicePlayer));
        }

        if (!_isRunning)
        {
            await StartAsync(null, cancellationToken);
        }

        lock (_sync)
        {
            if (_bufferedProvider is null || _output is null || !_isRunning)
            {
                return;
            }

            if (_bufferedProvider.BufferedBytes + pcm16Mono16Khz.Length > _bufferedProvider.BufferLength)
            {
                _bufferedProvider.ClearBuffer();
            }

            _bufferedProvider.AddSamples(pcm16Mono16Khz, 0, pcm16Mono16Khz.Length);
        }
    }

    public async Task StopAsync()
    {
        if (!_isRunning)
        {
            return;
        }

        lock (_sync)
        {
            StopLocked();
        }

        await Task.CompletedTask;
    }

    private void StopLocked()
    {
        if (!_isRunning && _output is null)
        {
            return;
        }

        _output?.Stop();
        _output?.Dispose();
        _output = null;
        _bufferedProvider = null;
        _isRunning = false;
        _activeDeviceId = null;
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        lock (_sync)
        {
            StopLocked();
        }

        await Task.CompletedTask;
    }
}
