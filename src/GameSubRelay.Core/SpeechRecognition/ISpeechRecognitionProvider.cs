using System.Threading;
using System.Threading.Tasks;
using GameSubRelay.Core.Audio;

namespace GameSubRelay.Core.SpeechRecognition;

public interface ISpeechRecognitionProvider
{
    Task<ISpeechRecognitionSession> StartSessionAsync(
        AudioChannelId channelId,
        SpeechRecognitionSessionOptions options,
        CancellationToken cancellationToken = default);
}
