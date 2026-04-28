using System;
using System.Linq;
using Xunit;
using GameSubRelay.Core.Audio;
using GameSubRelay.Core.Captions;
using GameSubRelay.Core.Translation;

namespace GameSubRelay.Core.Tests;

public class CaptionStoreTests
{
    [Fact]
    public void Interim_and_final_segments_for_same_sequence_are_aggregated()
    {
        var store = new CaptionStore(new CaptionStoreOptions(6));
        var channel = AudioChannelId.Microphone;

        var interim = new TranslationSegment(channel, 11, "en", "zh", "hello", string.Empty, SegmentStability.Interim, TimeSpan.FromSeconds(0), TimeSpan.FromSeconds(1));
        var final = new TranslationSegment(channel, 11, "en", "zh", "hello", "你好", SegmentStability.Final, TimeSpan.FromSeconds(0), TimeSpan.FromSeconds(1));

        store.ApplySegment(interim);
        store.ApplySegment(final);

        var lines = store.GetSnapshot();
        Assert.Single(lines);
        Assert.Equal("hello", lines[0].SourceText);
        Assert.Equal("你好", lines[0].TranslatedText);
        Assert.Equal(SegmentStability.Final, lines[0].Stability);
    }

    [Fact]
    public void Can_arrive_translation_before_source_and_be_upgraded_when_source_arrives()
    {
        var store = new CaptionStore(new CaptionStoreOptions(6));
        var channel = AudioChannelId.Microphone;

        var translationInterim = new TranslationSegment(channel, 12, "en", "zh", string.Empty, "正在翻译", SegmentStability.Interim, TimeSpan.Zero, TimeSpan.FromSeconds(1));
        var sourceFinal = new TranslationSegment(channel, 12, "en", "zh", "loading", string.Empty, SegmentStability.Final, TimeSpan.Zero, TimeSpan.FromSeconds(1));

        store.ApplySegment(translationInterim);
        store.ApplySegment(sourceFinal);

        var lines = store.GetSnapshot();
        Assert.Single(lines);
        Assert.Equal("loading", lines[0].SourceText);
        Assert.Equal("正在翻译", lines[0].TranslatedText);
        Assert.Equal(SegmentStability.Final, lines[0].Stability);
    }

    [Fact]
    public void Updating_same_sequence_does_not_change_display_count()
    {
        var store = new CaptionStore(new CaptionStoreOptions(6));
        var channel = AudioChannelId.Monitor;

        var interim = new TranslationSegment(channel, 1, "en", "zh", "a", "b", SegmentStability.Interim, TimeSpan.Zero, TimeSpan.FromMilliseconds(500));
        var final = new TranslationSegment(channel, 1, "en", "zh", "a", "b updated", SegmentStability.Final, TimeSpan.Zero, TimeSpan.FromMilliseconds(500));

        store.ApplySegment(interim);
        store.ApplySegment(final);

        Assert.Single(store.GetSnapshot());
    }

    [Fact]
    public void Max_lines_clip_oldest_lines()
    {
        var store = new CaptionStore(new CaptionStoreOptions(2));

        store.ApplySegment(new TranslationSegment(AudioChannelId.Microphone, 1, "en", "zh", "one", "一", SegmentStability.Final, TimeSpan.Zero, TimeSpan.FromMilliseconds(250)));
        store.ApplySegment(new TranslationSegment(AudioChannelId.Microphone, 2, "en", "zh", "two", "二", SegmentStability.Final, TimeSpan.Zero, TimeSpan.FromMilliseconds(250)));
        store.ApplySegment(new TranslationSegment(AudioChannelId.Monitor, 3, "en", "zh", "three", "三", SegmentStability.Final, TimeSpan.Zero, TimeSpan.FromMilliseconds(250)));

        var lines = store.GetSnapshot();

        Assert.Equal(2, lines.Count);
        Assert.Equal("two", lines[0].SourceText);
        Assert.Equal("三", lines[1].TranslatedText);
    }

    [Fact]
    public void Clear_emits_snapshot_and_resets_store()
    {
        var store = new CaptionStore(new CaptionStoreOptions(2));
        var snapshots = 0;
        store.CaptionLinesChanged += (_, args) =>
        {
            _ = args; // keep behavior explicit in test
            snapshots++;
        };

        store.ApplySegment(new TranslationSegment(AudioChannelId.Monitor, 9, "en", "zh", "hey", "嗨", SegmentStability.Final, TimeSpan.Zero, TimeSpan.FromMilliseconds(200)));
        store.Clear();

        Assert.Empty(store.GetSnapshot());
        Assert.Equal(2, snapshots);
    }
}
