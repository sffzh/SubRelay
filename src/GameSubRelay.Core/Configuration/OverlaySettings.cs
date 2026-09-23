namespace GameSubRelay.Core.Configuration;

public sealed record OverlaySettings(
    int Left,
    int Top,
    int Width,
    int Height,
    double Opacity,
    int FontSize,
    int MaxLines,
    bool Visible,
    bool OriginalEnabled ,
    bool SystemAudioEnabled
    )
{
    public static OverlaySettings Default => new(
        Left: 120,
        Top: 720,
        Width: 760,
        Height: 220,
        Opacity: 0.65,
        FontSize: 22,
        MaxLines: 6,
        Visible: true,
        OriginalEnabled: true,
        SystemAudioEnabled: true);

    public OverlaySettings Normalize()
    {
        return this with
        {
            Opacity = Math.Clamp(Opacity, 0.2, 0.95),
            MaxLines = Math.Clamp(MaxLines, 1, 12)
        };
    }
}
