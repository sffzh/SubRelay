using System.Runtime.InteropServices;
using NAudio.CoreAudioApi;

namespace GameSubRelay.Infrastructure.Audio;

public interface INaudioDeviceService
{
    Task<IReadOnlyList<AudioDeviceInfo>> GetCaptureDevicesAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AudioDeviceInfo>> GetRenderDevicesAsync(CancellationToken cancellationToken = default);
    Task<string?> GetDefaultCaptureDeviceIdAsync(CancellationToken cancellationToken = default);
    Task<string?> GetDefaultRenderDeviceIdAsync(CancellationToken cancellationToken = default);
    Task<MMDevice?> GetCaptureDeviceAsync(string? deviceId, CancellationToken cancellationToken = default);
    Task<MMDevice?> GetRenderDeviceAsync(string? deviceId, CancellationToken cancellationToken = default);
}

public sealed class NaudioDeviceService : INaudioDeviceService, IDisposable
{
    private bool _disposed;

    public Task<IReadOnlyList<AudioDeviceInfo>> GetCaptureDevicesAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.Run(() => GetDevices(DataFlow.Capture, cancellationToken), cancellationToken);
    }

    public Task<IReadOnlyList<AudioDeviceInfo>> GetRenderDevicesAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.Run(() => GetDevices(DataFlow.Render, cancellationToken), cancellationToken);
    }

    public Task<string?> GetDefaultCaptureDeviceIdAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                using var enumerator = new MMDeviceEnumerator();
                var defaultDevice = enumerator.GetDefaultAudioEndpoint(DataFlow.Capture, Role.Multimedia);
                return defaultDevice?.ID;
            }
            catch (COMException)
            {
                return null;
            }
        }, cancellationToken);
    }

    public Task<string?> GetDefaultRenderDeviceIdAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                using var enumerator = new MMDeviceEnumerator();
                var defaultDevice = enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
                return defaultDevice?.ID;
            }
            catch (COMException)
            {
                return null;
            }
        }, cancellationToken);
    }

    public Task<MMDevice?> GetCaptureDeviceAsync(string? deviceId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return GetDeviceByIdAsync(DataFlow.Capture, deviceId, cancellationToken);
    }

    public Task<MMDevice?> GetRenderDeviceAsync(string? deviceId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return GetDeviceByIdAsync(DataFlow.Render, deviceId, cancellationToken);
    }

    private Task<MMDevice?> GetDeviceByIdAsync(DataFlow dataFlow, string? deviceId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(deviceId))
        {
            try
            {
                using var enumerator = new MMDeviceEnumerator();
                var defaultDevice = enumerator.GetDefaultAudioEndpoint(dataFlow, Role.Multimedia);
                return Task.FromResult<MMDevice?>(defaultDevice);
            }
            catch (COMException)
            {
                return Task.FromResult<MMDevice?>(null);
            }
        }

        try
        {
            using var enumerator = new MMDeviceEnumerator();
            var device = enumerator.GetDevice(deviceId);
            return Task.FromResult<MMDevice?>(device);
        }
        catch (COMException)
        {
            return Task.FromResult<MMDevice?>(null);
        }
    }

    private static IReadOnlyList<AudioDeviceInfo> GetDevices(DataFlow dataFlow, CancellationToken cancellationToken)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            using var enumerator = new MMDeviceEnumerator();
            var collection = dataFlow == DataFlow.Capture
                ? enumerator.EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active)
                : enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active);

            var defaultId = dataFlow == DataFlow.Capture
                ? enumerator.GetDefaultAudioEndpoint(DataFlow.Capture, Role.Multimedia)?.ID
                : enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia)?.ID;

            var devices = new List<AudioDeviceInfo>();
            foreach (var device in collection)
            {
                try
                {
                    devices.Add(AudioMappingHelpers.ToDeviceInfo(
                        device,
                        dataFlow,
                        string.Equals(device.ID, defaultId, StringComparison.Ordinal)));
                }
                catch (COMException)
                {
                    devices.Add(AudioMappingHelpers.ToDeviceInfo(
                        device.ID,
                        string.IsNullOrWhiteSpace(device.FriendlyName) ? device.DeviceFriendlyName : device.FriendlyName,
                        device.DeviceFriendlyName,
                        dataFlow,
                        isEnabled: true,
                        channels: 2,
                        sampleRate: 48000,
                        bitsPerSample: 16,
                        string.Equals(device.ID, defaultId, StringComparison.Ordinal)));
                }
            }

            return devices;
        }
        catch (COMException)
        {
            return Array.Empty<AudioDeviceInfo>();
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
    }
}
