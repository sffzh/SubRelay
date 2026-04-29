using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using GameSubRelay.Core.Runtime;

namespace GameSubRelay.Infrastructure.Runtime;

public sealed class AppRuntimeService : BackgroundService, IAppRuntimeService
{
    private readonly ITranslationChannelWorkerFactory _workerFactory;
    private readonly ILogger<AppRuntimeService> _logger;
    private readonly SemaphoreSlim _lifecycleLock = new(1, 1);
    private List<ITranslationChannelWorker> _channelWorkers = [];

    public event EventHandler<ChannelRuntimeState> ChannelStateChanged = delegate { };

    public bool IsRunning { get; private set; }

    public AppRuntimeService(
        ITranslationChannelWorkerFactory workerFactory,
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
            if (IsRunning)
            {
                _logger.LogInformation("Runtime start requested while relay is already running.");
                return;
            }

            _logger.LogInformation("Runtime start requested.");
            var startedCount = await StartChannelsAsync(cancellationToken);
            IsRunning = startedCount > 0;
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
            if (!IsRunning && _channelWorkers.Count == 0)
            {
                _logger.LogInformation("Runtime stop requested while relay is already stopped.");
                return;
            }

            _logger.LogInformation("Runtime stop requested.");
            await StopChannelsAsync(cancellationToken);
            IsRunning = false;
            _logger.LogInformation("Runtime stop completed.");
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
            await StopChannelsAsync(cancellationToken);
            var startedCount = await StartChannelsAsync(cancellationToken);
            IsRunning = startedCount > 0;
            _logger.LogInformation("Runtime restart completed.");
        }
        finally
        {
            _lifecycleLock.Release();
        }
    }

    private async Task<int> StartChannelsAsync(CancellationToken stoppingToken)
    {
        var workers = _workerFactory.CreateWorkers().ToList();
        var startedCount = 0;
        _channelWorkers = [];
        _logger.LogInformation("Created {WorkerCount} runtime channel workers.", workers.Count);
        foreach (var worker in workers)
        {
            try
            {
                await worker.StartAsync(stoppingToken);
                _channelWorkers.Add(worker);
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
                if (worker is IAsyncDisposable disposable)
                {
                    await disposable.DisposeAsync();
                }

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
            await StopChannelsAsync(cancellationToken);
            IsRunning = false;
        }
        finally
        {
            _lifecycleLock.Release();
        }

        await base.StopAsync(cancellationToken);
    }

    private async Task StopChannelsAsync(CancellationToken cancellationToken)
    {
        foreach (var worker in _channelWorkers)
        {
            _logger.LogInformation("Stopping runtime channel worker {ChannelId}.", worker.ChannelId);
            var channelId = worker.ChannelId;
            await worker.StopAsync(cancellationToken);
            if (worker is IAsyncDisposable disposable)
            {
                await disposable.DisposeAsync();
            }

            ChannelStateChanged(this, new ChannelRuntimeState(
                channelId,
                ChannelRuntimeStatus.Stopped,
                IsEnabled: true,
                LastUpdatedAt: DateTimeOffset.UtcNow));
        }

        _logger.LogInformation("Stopped {WorkerCount} runtime channel workers.", _channelWorkers.Count);
        _channelWorkers.Clear();
    }
}
