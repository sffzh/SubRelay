using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace GameSubRelay.Infrastructure.Audio;

public abstract class WasapiCaptureServiceBase : IAudioCaptureService
{
    private readonly string? _deviceId;
    private readonly INaudioDeviceService _deviceService;
    private readonly Pcm16Mono16KhzConverter _converter;
    private readonly AudioCaptureMode _mode;
    private readonly object _sync = new();

    private WasapiCapture? _capture;
    private string? _activeDeviceId;
    private bool _disposed;

    protected WasapiCaptureServiceBase(
        string? deviceId,
        INaudioDeviceService deviceService,
        Pcm16Mono16KhzConverter converter,
        AudioCaptureMode mode)
    {
        _deviceId = deviceId;
        _deviceService = deviceService;
        _converter = converter;
        _mode = mode;
    }

    public bool IsRunning { get; private set; }
    public event EventHandler<CapturedAudioFrame>? FrameCaptured;

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (IsRunning)
        {
            return;
        }

        if (_disposed)
        {
            throw new ObjectDisposedException(GetType().Name);
        }

        var device = _mode == AudioCaptureMode.Microphone
            ? await _deviceService.GetCaptureDeviceAsync(_deviceId, cancellationToken)
            : await _deviceService.GetRenderDeviceAsync(_deviceId, cancellationToken);
        if (device is null)
        {
            throw new InvalidOperationException("No available capture device.");
        }

        lock (_sync)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(GetType().Name);
            }

            if (_capture is not null)
            {
                return;
            }

            _capture = CreateCapture(device);
            _capture.DataAvailable += OnDataAvailable;
            _capture.RecordingStopped += OnRecordingStopped;
            _capture.StartRecording();
            _activeDeviceId = device.ID;
            IsRunning = true;
        }
    }

    public async Task StopAsync()
    {
        WasapiCapture? capture = null;

        lock (_sync)
        {
            if (_capture is null)
            {
                return;
            }

            capture = _capture;
            _capture = null;
            IsRunning = false;
        }

        capture!.StopRecording();
        capture.Dispose();
        await Task.CompletedTask;
    }

    private void OnRecordingStopped(object? sender, StoppedEventArgs e)
    {
        // Keep capture lifecycle deterministic and avoid throwing inside callback threads.
        if (e.Exception is null)
        {
            return;
        }
    }

    private void OnDataAvailable(object? sender, WaveInEventArgs e)
    {
        var capture = _capture;
        if (_disposed || capture is null || e.BytesRecorded <= 0)
        {
            return;
        }

        var sourceFormat = capture.WaveFormat;
        var output = _converter.Convert(new ReadOnlySpan<byte>(e.Buffer, 0, e.BytesRecorded), sourceFormat);
        var duration = AudioMappingHelpers.GetTargetDurationFromOutputBytes(output.Length);

        FrameCaptured?.Invoke(this, new CapturedAudioFrame(
            _activeDeviceId ?? string.Empty,
            output,
            DateTimeOffset.UtcNow,
            duration));
    }

    protected abstract WasapiCapture CreateCapture(MMDevice device);

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        await StopAsync();
    }

    public enum AudioCaptureMode
    {
        Microphone,
        Loopback
    }
}
