using System;
using System.Collections.Generic;

namespace GameSubRelay.Core.Runtime;

public sealed record AppStatus(
    bool IsRunning,
    IReadOnlyList<ChannelRuntimeState> ChannelStates,
    DateTimeOffset? LastUpdatedAt = null);
