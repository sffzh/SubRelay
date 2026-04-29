using System;
using System.Threading;
using System.Threading.Tasks;
using GameSubRelay.Core.Audio;
using GameSubRelay.Core.Runtime;

namespace GameSubRelay.Infrastructure.Runtime;

public interface IAppRuntimeService
{
    event EventHandler<ChannelRuntimeState> ChannelStateChanged;

    bool IsRunning { get; }

    bool IsChannelRunning(AudioChannelId channelId);

    Task StartChannelAsync(AudioChannelId channelId, CancellationToken cancellationToken = default);

    Task StopChannelAsync(AudioChannelId channelId, CancellationToken cancellationToken = default);

    Task StartRelayAsync(CancellationToken cancellationToken = default);

    Task StopRelayAsync(CancellationToken cancellationToken = default);

    Task RestartAsync(CancellationToken cancellationToken = default);
}
