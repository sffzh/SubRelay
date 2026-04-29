using System.Collections.Generic;

namespace GameSubRelay.Infrastructure.Runtime;

public interface IAudioChannelWorkerFactory
{
    IReadOnlyList<IAudioChannelWorker> CreateWorkers();
}
