using System.Threading;
using System.Threading.Tasks;
using GameSubRelay.Core.Audio;

namespace GameSubRelay.Core.Translation;

public interface ISpeechTranslationProvider
{
    Task<ISpeechTranslationSession> StartSessionAsync(
        AudioChannelId channelId,
        SpeechTranslationSessionOptions options,
        CancellationToken cancellationToken = default);
}
