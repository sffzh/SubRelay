namespace GameSubRelay.Core.Runtime;

public enum ChannelRuntimeStatus
{
    Stopped,
    Starting,
    Capturing,
    Translating,
    Reconnecting,
    Error
}
