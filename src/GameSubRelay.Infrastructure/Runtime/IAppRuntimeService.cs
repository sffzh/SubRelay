using System;
using System.Threading;
using System.Threading.Tasks;
using GameSubRelay.Core.Runtime;

namespace GameSubRelay.Infrastructure.Runtime;

public interface IAppRuntimeService
{
    event EventHandler<ChannelRuntimeState> ChannelStateChanged;

    bool IsRunning { get; }

    Task StartRelayAsync(CancellationToken cancellationToken = default);

    Task StopRelayAsync(CancellationToken cancellationToken = default);

    Task RestartAsync(CancellationToken cancellationToken = default);
}
