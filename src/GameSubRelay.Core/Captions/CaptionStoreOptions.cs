namespace GameSubRelay.Core.Captions;

public sealed record CaptionStoreOptions(int MaxLines)
{
    public const int DefaultMaxLines = 6;
    public const int MinMaxLines = 1;
    public const int MaxMaxLines = 12;

    public static CaptionStoreOptions Default => new(DefaultMaxLines);

    public CaptionStoreOptions Normalize()
    {
        return this with
        {
            MaxLines = Math.Clamp(MaxLines, MinMaxLines, MaxMaxLines)
        };
    }
}
