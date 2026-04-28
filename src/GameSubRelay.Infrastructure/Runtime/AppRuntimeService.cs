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

    public AppRuntimeService(
        ITranslationChannelWorkerFactory workerFactory,
        ILogger<AppRuntimeService> logger)
    {
        _workerFactory = workerFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await StartChannelsAsync(stoppingToken);
    }

    public async Task RestartAsync(CancellationToken cancellationToken = default)
    {
        await _lifecycleLock.WaitAsync(cancellationToken);
        try
        {
            await StopChannelsAsync(cancellationToken);
            await StartChannelsAsync(cancellationToken);
        }
        finally
        {
            _lifecycleLock.Release();
        }
    }

    private async Task StartChannelsAsync(CancellationToken stoppingToken)
    {
        _channelWorkers = _workerFactory.CreateWorkers().ToList();
        foreach (var worker in _channelWorkers)
        {
            try
            {
                await worker.StartAsync(stoppingToken);
                ChannelStateChanged(this, new ChannelRuntimeState(
                    worker.ChannelId,
                    ChannelRuntimeStatus.Capturing,
                    IsEnabled: true,
                    LastUpdatedAt: DateTimeOffset.UtcNow));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to start channel {ChannelId}", worker.ChannelId);
                ChannelStateChanged(this, new ChannelRuntimeState(
                    worker.ChannelId,
                    ChannelRuntimeStatus.Error,
                    IsEnabled: true,
                    LastUpdatedAt: DateTimeOffset.UtcNow,
                    ErrorMessage: ex.Message));
            }
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await _lifecycleLock.WaitAsync(cancellationToken);
        try
        {
            await StopChannelsAsync(cancellationToken);
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
            await worker.StopAsync(cancellationToken);
            if (worker is IAsyncDisposable disposable)
            {
                await disposable.DisposeAsync();
            }
        }

        _channelWorkers.Clear();
    }
}
