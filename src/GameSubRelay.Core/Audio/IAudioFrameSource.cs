using System.Collections.Generic;
using System.Threading;
using GameSubRelay.Core.Audio;

namespace GameSubRelay.Core.Audio;

public interface IAudioFrameSource : IAsyncDisposable
{
    AudioChannelId ChannelId { get; }

    IAsyncEnumerable<AudioFrame> GetFramesAsync(CancellationToken cancellationToken = default);

    ValueTask StartAsync(CancellationToken cancellationToken = default);

    ValueTask StopAsync();
}
