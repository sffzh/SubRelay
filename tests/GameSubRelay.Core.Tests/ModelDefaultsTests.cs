using System;
using Xunit;
using GameSubRelay.Core.Audio;
using GameSubRelay.Core.Captions;
using GameSubRelay.Core.Configuration;

namespace GameSubRelay.Core.Tests;

public class ModelDefaultsTests
{
    [Fact]
    public void AudioChannelId_values_should_remain_stable()
    {
        Assert.Equal(1, (int)AudioChannelId.Microphone);
        Assert.Equal(2, (int)AudioChannelId.Monitor);
    }

    [Fact]
    public void SegmentStability_should_define_two_states()
    {
        Assert.Equal(0, (int)SegmentStability.Interim);
        Assert.Equal(1, (int)SegmentStability.Final);
    }

    [Fact]
    public void Speech_translation_defaults_should_be_stable()
    {
        var appSettings = AppSettings.Default;

        Assert.Equal("en", appSettings.Translation.SourceLanguage);
        Assert.Equal("zh", appSettings.Translation.TargetLanguage);
        Assert.Equal("cn-north-1", appSettings.Translation.Region);
        Assert.Equal("en", appSettings.SpeechRecognition.Language);
        Assert.Equal("cn-north-1", appSettings.SpeechRecognition.Region);
    }

    [Fact]
    public void Overlay_defaults_should_be_normalized_on_demand()
    {
        var overlay = new OverlaySettings(-1, -1, 1, 1, 0.01, 1, 100, true).Normalize();

        Assert.Equal(0.2, overlay.Opacity);
        Assert.Equal(12, overlay.MaxLines);
        Assert.Equal(1, overlay.FontSize);
        Assert.Equal(-1, overlay.Left);
        Assert.Equal(-1, overlay.Top);
        Assert.Equal(1, overlay.Width);
        Assert.Equal(1, overlay.Height);
    }
}
