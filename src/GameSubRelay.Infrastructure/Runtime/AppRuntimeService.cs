using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using GameSubRelay.Core.Audio;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using GameSubRelay.Core.Runtime;

namespace GameSubRelay.Infrastructure.Runtime;

public sealed class AppRuntimeService : BackgroundService, IAppRuntimeService
{
    private readonly IAudioChannelWorkerFactory _workerFactory;
    private readonly ILogger<AppRuntimeService> _logger;
    private readonly SemaphoreSlim _lifecycleLock = new(1, 1);
    private readonly Dictionary<AudioChannelId, IAudioChannelWorker> _channelWorkers = [];

    public event EventHandler<ChannelRuntimeState> ChannelStateChanged = delegate { };

    public bool IsRunning => _channelWorkers.Count > 0;

    public AppRuntimeService(
        IAudioChannelWorkerFactory workerFactory,
        ILogger<AppRuntimeService> logger)
    {
        _workerFactory = workerFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Runtime service ready; relay channels are stopped until the user starts them.");
        try
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, stoppingToken);
        }
        catch (OperationCanceledException)
        {
        }
    }

    public async Task StartRelayAsync(CancellationToken cancellationToken = default)
    {
        await _lifecycleLock.WaitAsync(cancellationToken);
        try
        {
            _logger.LogInformation("Runtime start requested.");
            var startedCount = await StartChannelsAsync(channelIds: null, cancellationToken);
            _logger.LogInformation(
                "Runtime start completed: startedChannels={StartedCount}, isRunning={IsRunning}.",
                startedCount,
                IsRunning);
        }
        finally
        {
            _lifecycleLock.Release();
        }
    }

    public async Task StopRelayAsync(CancellationToken cancellationToken = default)
    {
        await _lifecycleLock.WaitAsync(cancellationToken);
        try
        {
            if (!IsRunning)
            {
                _logger.LogInformation("Runtime stop requested while relay is already stopped.");
                return;
            }

            _logger.LogInformation("Runtime stop requested.");
            await StopChannelsAsync(channelIds: null, cancellationToken);
            _logger.LogInformation("Runtime stop completed.");
        }
        finally
        {
            _lifecycleLock.Release();
        }
    }

    public bool IsChannelRunning(AudioChannelId channelId)
    {
        return _channelWorkers.ContainsKey(channelId);
    }

    public async Task StartChannelAsync(
        AudioChannelId channelId,
        CancellationToken cancellationToken = default)
    {
        await _lifecycleLock.WaitAsync(cancellationToken);
        try
        {
            if (_channelWorkers.ContainsKey(channelId))
            {
                _logger.LogInformation("Runtime channel start requested while {ChannelId} is already running.", channelId);
                return;
            }

            _logger.LogInformation("Runtime channel start requested: {ChannelId}.", channelId);
            var startedCount = await StartChannelsAsync([channelId], cancellationToken);
            _logger.LogInformation(
                "Runtime channel start completed: channel={ChannelId}, startedChannels={StartedCount}, isRunning={IsRunning}.",
                channelId,
                startedCount,
                IsRunning);
        }
        finally
        {
            _lifecycleLock.Release();
        }
    }

    public async Task StopChannelAsync(
        AudioChannelId channelId,
        CancellationToken cancellationToken = default)
    {
        await _lifecycleLock.WaitAsync(cancellationToken);
        try
        {
            if (!_channelWorkers.ContainsKey(channelId))
            {
                _logger.LogInformation("Runtime channel stop requested while {ChannelId} is already stopped.", channelId);
                return;
            }

            _logger.LogInformation("Runtime channel stop requested: {ChannelId}.", channelId);
            await StopChannelsAsync([channelId], cancellationToken);
            _logger.LogInformation(
                "Runtime channel stop completed: channel={ChannelId}, isRunning={IsRunning}.",
                channelId,
                IsRunning);
        }
        finally
        {
            _lifecycleLock.Release();
        }
    }

    public async Task RestartAsync(CancellationToken cancellationToken = default)
    {
        await _lifecycleLock.WaitAsync(cancellationToken);
        try
        {
            if (!IsRunning)
            {
                _logger.LogInformation("Runtime restart requested while relay is stopped; keeping channels stopped.");
                return;
            }

            _logger.LogInformation("Runtime restart requested.");
            var runningChannelIds = _channelWorkers.Keys.ToArray();
            await StopChannelsAsync(runningChannelIds, cancellationToken);
            var startedCount = await StartChannelsAsync(runningChannelIds, cancellationToken);
            _logger.LogInformation(
                "Runtime restart completed: startedChannels={StartedCount}, isRunning={IsRunning}.",
                startedCount,
                IsRunning);
        }
        finally
        {
            _lifecycleLock.Release();
        }
    }

    private async Task<int> StartChannelsAsync(
        IEnumerable<AudioChannelId>? channelIds,
        CancellationToken stoppingToken)
    {
        var workers = _workerFactory.CreateWorkers().ToList();
        var requested = channelIds?.ToHashSet();
        var startedCount = 0;
        _logger.LogInformation("Created {WorkerCount} runtime channel workers.", workers.Count);
        foreach (var worker in workers)
        {
            if (requested is not null && !requested.Contains(worker.ChannelId))
            {
                await DisposeWorkerAsync(worker);
                continue;
            }

            if (_channelWorkers.ContainsKey(worker.ChannelId))
            {
                _logger.LogInformation("Skipping already running channel {ChannelId}.", worker.ChannelId);
                await DisposeWorkerAsync(worker);
                continue;
            }

            try
            {
                await worker.StartAsync(stoppingToken);
                _channelWorkers[worker.ChannelId] = worker;
                startedCount++;
                ChannelStateChanged(this, new ChannelRuntimeState(
                    worker.ChannelId,
                    ChannelRuntimeStatus.Capturing,
                    IsEnabled: true,
                    LastUpdatedAt: DateTimeOffset.UtcNow));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to start channel {ChannelId}", worker.ChannelId);
                await DisposeWorkerAsync(worker);

                ChannelStateChanged(this, new ChannelRuntimeState(
                    worker.ChannelId,
                    ChannelRuntimeStatus.Error,
                    IsEnabled: true,
                    LastUpdatedAt: DateTimeOffset.UtcNow,
                    ErrorMessage: ex.Message));
            }
        }

        return startedCount;
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await _lifecycleLock.WaitAsync(cancellationToken);
        try
        {
            await StopChannelsAsync(channelIds: null, cancellationToken);
        }
        finally
        {
            _lifecycleLock.Release();
        }

        await base.StopAsync(cancellationToken);
    }

    private async Task StopChannelsAsync(
        IEnumerable<AudioChannelId>? channelIds,
        CancellationToken cancellationToken)
    {
        var requested = channelIds?.ToHashSet();
        var workers = _channelWorkers
            .Where(item => requested is null || requested.Contains(item.Key))
            .ToList();

        foreach (var (channelId, worker) in workers)
        {
            _logger.LogInformation("Stopping runtime channel worker {ChannelId}.", channelId);
            await worker.StopAsync(cancellationToken);
            await DisposeWorkerAsync(worker);
            _channelWorkers.Remove(channelId);

            ChannelStateChanged(this, new ChannelRuntimeState(
                channelId,
                ChannelRuntimeStatus.Stopped,
                IsEnabled: true,
                LastUpdatedAt: DateTimeOffset.UtcNow));
        }

        _logger.LogInformation("Stopped {WorkerCount} runtime channel workers.", workers.Count);
    }

    private static async ValueTask DisposeWorkerAsync(IAudioChannelWorker worker)
    {
        if (worker is IAsyncDisposable disposable)
        {
            await disposable.DisposeAsync();
        }
    }
}
