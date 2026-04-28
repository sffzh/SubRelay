namespace GameSubRelay.Infrastructure.Runtime;

public sealed class NoopChannelExecutionStrategy
{
    public string ChannelName { get; }

    public NoopChannelExecutionStrategy(string channelName)
    {
        ChannelName = channelName;
    }
}

