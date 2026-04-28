using System;
using GameSubRelay.Core.Audio;

namespace GameSubRelay.Core.Runtime;

public sealed record ChannelRuntimeState(
    AudioChannelId ChannelId,
    ChannelRuntimeStatus Status,
    bool IsEnabled,
    string? ErrorCode = null,
    DateTimeOffset? LastUpdatedAt = null,
    string? ErrorMessage = null);
