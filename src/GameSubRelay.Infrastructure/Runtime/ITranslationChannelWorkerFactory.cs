using System.Collections.Generic;

namespace GameSubRelay.Infrastructure.Runtime;

public interface ITranslationChannelWorkerFactory
{
    IReadOnlyList<ITranslationChannelWorker> CreateWorkers();
}
