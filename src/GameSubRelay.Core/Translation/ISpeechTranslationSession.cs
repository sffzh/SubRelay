using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using GameSubRelay.Core.Audio;

namespace GameSubRelay.Core.Translation;

public interface ISpeechTranslationSession : IAsyncDisposable
{
    ValueTask SendAudioAsync(AudioFrame frame, CancellationToken cancellationToken);

    ValueTask CompleteAsync(CancellationToken cancellationToken);

    IAsyncEnumerable<TranslationSegment> ReadSegmentsAsync(CancellationToken cancellationToken);
}
