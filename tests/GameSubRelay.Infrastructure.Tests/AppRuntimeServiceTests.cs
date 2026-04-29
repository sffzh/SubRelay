using GameSubRelay.Core.Audio;
using GameSubRelay.Core.Runtime;
using GameSubRelay.Infrastructure.Runtime;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace GameSubRelay.Infrastructure.Tests;

public sealed class AppRuntimeServiceTests
{
    [Fact]
    public async Task StartRelayAsync_starts_workers_only_when_requested()
    {
        var worker = new RecordingWorker(AudioChannelId.Microphone);
        var factory = new RecordingWorkerFactory(() => [worker]);
        var service = new AppRuntimeService(factory, NullLogger<AppRuntimeService>.Instance);

        Assert.False(service.IsRunning);
        Assert.Equal(0, worker.StartCount);

        await service.StartRelayAsync();

        Assert.True(service.IsRunning);
        Assert.Equal(1, factory.CreateCalls);
        Assert.Equal(1, worker.StartCount);
    }

    [Fact]
    public async Task StopRelayAsync_stops_and_disposes_started_workers()
    {
        var worker = new RecordingWorker(AudioChannelId.Microphone);
        var service = new AppRuntimeService(
            new RecordingWorkerFactory(() => [worker]),
            NullLogger<AppRuntimeService>.Instance);

        await service.StartRelayAsync();
        await service.StopRelayAsync();

        Assert.False(service.IsRunning);
        Assert.Equal(1, worker.StopCount);
        Assert.Equal(1, worker.DisposeCount);
    }

    [Fact]
    public async Task RestartAsync_does_not_start_channels_when_relay_is_stopped()
    {
        var factory = new RecordingWorkerFactory(() => [new RecordingWorker(AudioChannelId.Microphone)]);
        var service = new AppRuntimeService(factory, NullLogger<AppRuntimeService>.Instance);

        await service.RestartAsync();

        Assert.False(service.IsRunning);
        Assert.Equal(0, factory.CreateCalls);
    }

    [Fact]
    public async Task StartRelayAsync_reports_error_without_marking_relay_running_when_worker_fails()
    {
        var worker = new RecordingWorker(AudioChannelId.Microphone)
        {
            ThrowOnStart = true
        };
        var states = new List<ChannelRuntimeState>();
        var service = new AppRuntimeService(
            new RecordingWorkerFactory(() => [worker]),
            NullLogger<AppRuntimeService>.Instance);
        service.ChannelStateChanged += (_, state) => states.Add(state);

        await service.StartRelayAsync();

        Assert.False(service.IsRunning);
        Assert.Equal(1, worker.StartCount);
        Assert.Equal(1, worker.DisposeCount);
        Assert.Contains(states, state =>
            state.ChannelId == AudioChannelId.Microphone &&
            state.Status == ChannelRuntimeStatus.Error);
    }

    private sealed class RecordingWorkerFactory : ITranslationChannelWorkerFactory
    {
        private readonly Func<IReadOnlyList<ITranslationChannelWorker>> _createWorkers;

        public RecordingWorkerFactory(Func<IReadOnlyList<ITranslationChannelWorker>> createWorkers)
        {
            _createWorkers = createWorkers;
        }

        public int CreateCalls { get; private set; }

        public IReadOnlyList<ITranslationChannelWorker> CreateWorkers()
        {
            CreateCalls++;
            return _createWorkers();
        }
    }

    private sealed class RecordingWorker : ITranslationChannelWorker, IAsyncDisposable
    {
        public RecordingWorker(AudioChannelId channelId)
        {
            ChannelId = channelId;
        }

        public AudioChannelId ChannelId { get; }

        public bool ThrowOnStart { get; init; }

        public int StartCount { get; private set; }

        public int StopCount { get; private set; }

        public int DisposeCount { get; private set; }

        public event EventHandler? StateChanged;

        public Task StartAsync(CancellationToken cancellationToken = default)
        {
            StartCount++;
            StateChanged?.Invoke(this, EventArgs.Empty);
            if (ThrowOnStart)
            {
                throw new InvalidOperationException("start failed");
            }

            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken = default)
        {
            StopCount++;
            StateChanged?.Invoke(this, EventArgs.Empty);
            return Task.CompletedTask;
        }

        public ValueTask DisposeAsync()
        {
            DisposeCount++;
            return ValueTask.CompletedTask;
        }
    }
}
