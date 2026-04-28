using System;
using System.Threading;
using System.Threading.Tasks;
using GameSubRelay.Core.Runtime;

namespace GameSubRelay.Infrastructure.Runtime;

public interface IAppRuntimeService
{
    event EventHandler<ChannelRuntimeState> ChannelStateChanged;

    Task RestartAsync(CancellationToken cancellationToken = default);
}
