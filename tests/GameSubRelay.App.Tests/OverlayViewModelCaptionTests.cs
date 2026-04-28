using GameSubRelay.App.ViewModels;
using GameSubRelay.Core.Audio;
using GameSubRelay.Core.Captions;
using GameSubRelay.Core.Translation;
using Xunit;

namespace GameSubRelay.App.Tests;

public sealed class OverlayViewModelCaptionTests
{
    [Fact]
    public void ApplyCaptionSnapshot_replaces_overlay_caption_lines()
    {
        var viewModel = new OverlayViewModel();
        var line = new CaptionLine(
            Guid.NewGuid(),
            AudioChannelId.Microphone,
            "Mic",
            "hello",
            "你好",
            SegmentStability.Final,
            DateTimeOffset.UtcNow);

        viewModel.ApplyCaptionSnapshot([line]);

        Assert.Single(viewModel.Captions);
        Assert.Equal("Mic", viewModel.Captions[0].ChannelLabel);
        Assert.Equal("hello", viewModel.Captions[0].SourceText);
        Assert.Equal("你好", viewModel.Captions[0].TranslatedText);
        Assert.True(viewModel.Captions[0].IsFinal);
    }
}
