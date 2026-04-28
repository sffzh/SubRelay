using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace GameSubRelay.Core.Tts;

public interface ITtsProvider
{
    Task<byte[]> SynthesizeAsync(TtsRequest request, CancellationToken cancellationToken = default);
}
