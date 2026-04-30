using GameSubRelay.App.ViewModels;
using GameSubRelay.Core.Audio;
using GameSubRelay.Core.Captions;
using GameSubRelay.Core.Translation;
using Xunit;

namespace GameSubRelay.App.Tests;

public sealed class OverlayViewModelCaptionTests
{
    [Fact]
    public void ApplyCaptionSnapshot_updates_fixed_channel_regions_with_latest_text()
    {
        var viewModel = new OverlayViewModel();
        var now = DateTimeOffset.UtcNow;

        viewModel.ApplyCaptionSnapshot(
        [
            new CaptionLine(
                Guid.NewGuid(),
                AudioChannelId.Microphone,
                "Mic",
                "older mic",
                "旧麦克风",
                SegmentStability.Final,
                now.AddSeconds(-1)),
            new CaptionLine(
                Guid.NewGuid(),
                AudioChannelId.Microphone,
                "Mic",
                "current mic",
                "当前麦克风",
                SegmentStability.Interim,
                now),
            new CaptionLine(
                Guid.NewGuid(),
                AudioChannelId.Monitor,
                "System",
                "current system voice",
                "当前系统声音",
                SegmentStability.Final,
                now.AddMilliseconds(-500))
        ]);

        Assert.Equal("麦克风", viewModel.MicrophoneCaption.ChannelLabel);
        Assert.Equal("current mic", viewModel.MicrophoneCaption.SourceText);
        Assert.Equal("当前麦克风", viewModel.MicrophoneCaption.TranslatedText);
        Assert.False(viewModel.MicrophoneCaption.IsFinal);
        Assert.Equal("系统声音", viewModel.MonitorCaption.ChannelLabel);
        Assert.Equal("current system voice", viewModel.MonitorCaption.SourceText);
        Assert.Equal("当前系统声音", viewModel.MonitorCaption.TranslatedText);
        Assert.Equal("current system voice", viewModel.MonitorCaption.PrimaryText);
        Assert.True(viewModel.MonitorCaption.IsFinal);
    }

    [Fact]
    public void PrimaryText_falls_back_to_translation_when_source_is_empty()
    {
        var line = new CaptionLine(
            Guid.NewGuid(),
            AudioChannelId.Monitor,
            "Game",
            string.Empty,
            "fallback text",
            SegmentStability.Interim,
            DateTimeOffset.UtcNow);
        var caption = new CaptionLineViewModel();

        caption.Apply(line);

        Assert.Empty(caption.SourceText);
        Assert.Equal("fallback text", caption.TranslatedText);
        Assert.Equal("fallback text", caption.PrimaryText);
    }

    [Fact]
    public void ClearCaptions_clears_fixed_channel_regions()
    {
        var viewModel = new OverlayViewModel();
        viewModel.ApplyCaptionSnapshot(
        [
            new CaptionLine(
                Guid.NewGuid(),
                AudioChannelId.Microphone,
                "Mic",
                "hello",
                "你好",
                SegmentStability.Final,
                DateTimeOffset.UtcNow)
        ]);

        viewModel.ClearCaptions();

        Assert.Empty(viewModel.MicrophoneCaption.SourceText);
        Assert.Empty(viewModel.MicrophoneCaption.TranslatedText);
        Assert.Empty(viewModel.MonitorCaption.SourceText);
        Assert.False(viewModel.MicrophoneCaption.IsFinal);
    }
}
