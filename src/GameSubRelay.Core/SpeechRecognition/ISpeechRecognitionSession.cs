using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using GameSubRelay.Core.Audio;

namespace GameSubRelay.Core.SpeechRecognition;

public interface ISpeechRecognitionSession : IAsyncDisposable
{
    ValueTask SendAudioAsync(AudioFrame frame, CancellationToken cancellationToken);

    ValueTask CompleteAsync(CancellationToken cancellationToken);

    IAsyncEnumerable<SpeechRecognitionSegment> ReadSegmentsAsync(CancellationToken cancellationToken);
}
