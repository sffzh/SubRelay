using GameSubRelay.Core.Audio;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace GameSubRelay.Infrastructure.Runtime;

public interface ITranslationChannelWorker
{
    AudioChannelId ChannelId { get; }

    event EventHandler? StateChanged;

    Task StartAsync(CancellationToken cancellationToken = default);

    Task StopAsync(CancellationToken cancellationToken = default);
}

