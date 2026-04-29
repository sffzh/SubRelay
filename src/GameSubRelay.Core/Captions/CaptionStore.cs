using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using GameSubRelay.Core.Audio;
using GameSubRelay.Core.SpeechRecognition;
using GameSubRelay.Core.Translation;

namespace GameSubRelay.Core.Captions;

public sealed class CaptionStore
{
    private readonly object _sync = new();
    private readonly Dictionary<CaptionKey, CaptionAggregate> _aggregates = [];
    private readonly List<CaptionKey> _orderedKeys = [];
    private readonly ChannelLabelProvider _channelLabelProvider;
    private readonly int _maxLines;

    public event EventHandler<CaptionStoreSnapshotChangedEventArgs>? CaptionLinesChanged;

    public CaptionStore(CaptionStoreOptions? options = null, ChannelLabelProvider? channelLabelProvider = null)
    {
        var normalized = options?.Normalize() ?? CaptionStoreOptions.Default;
        _maxLines = normalized.MaxLines;
        _channelLabelProvider = channelLabelProvider ?? ChannelLabelProvider.Default;
    }

    public int MaxLines => _maxLines;

    public int Count
    {
        get
        {
            lock (_sync)
            {
                return _orderedKeys.Count;
            }
        }
    }

    public IReadOnlyList<CaptionLine> GetSnapshot()
    {
        lock (_sync)
        {
            var lines = new List<CaptionLine>(_orderedKeys.Count);
            foreach (var key in _orderedKeys)
            {
                lines.Add(_aggregates[key].ToLine(_channelLabelProvider));
            }

            return new ReadOnlyCollection<CaptionLine>(lines);
        }
    }

    public void ApplySegment(TranslationSegment segment, DateTimeOffset? observedAt = null)
    {
        bool shouldNotify;
        lock (_sync)
        {
            var key = new CaptionKey(segment.ChannelId, segment.ProviderSequence);
            var now = observedAt ?? DateTimeOffset.UtcNow;
            var aggregate = _aggregates.GetValueOrDefault(key);
            var isNew = false;

            if (aggregate is null)
            {
                aggregate = new CaptionAggregate(segment.ChannelId);
                _aggregates[key] = aggregate;
                _orderedKeys.Add(key);
                isNew = true;
            }

            aggregate.ApplySegment(segment, now);

            if (isNew)
            {
                TrimToMaxLines();
            }

            shouldNotify = true;
        }

        if (shouldNotify)
        {
            NotifyChanged();
        }
    }

    public void ApplyRecognitionSegment(SpeechRecognitionSegment segment, DateTimeOffset? observedAt = null)
    {
        bool shouldNotify;
        lock (_sync)
        {
            var key = new CaptionKey(segment.ChannelId, segment.ProviderSequence);
            var now = observedAt ?? DateTimeOffset.UtcNow;
            var aggregate = _aggregates.GetValueOrDefault(key);
            var isNew = false;

            if (aggregate is null)
            {
                aggregate = new CaptionAggregate(segment.ChannelId);
                _aggregates[key] = aggregate;
                _orderedKeys.Add(key);
                isNew = true;
            }

            aggregate.ApplyRecognitionSegment(segment, now);

            if (isNew)
            {
                TrimToMaxLines();
            }

            shouldNotify = true;
        }

        if (shouldNotify)
        {
            NotifyChanged();
        }
    }

    public void Clear()
    {
        bool changed;
        lock (_sync)
        {
            changed = _aggregates.Count > 0;
            _aggregates.Clear();
            _orderedKeys.Clear();
        }

        if (changed)
        {
            NotifyChanged();
        }
    }

    private void TrimToMaxLines()
    {
        while (_orderedKeys.Count > _maxLines)
        {
            var removedKey = _orderedKeys[0];
            _orderedKeys.RemoveAt(0);
            _aggregates.Remove(removedKey);
        }
    }

    private void NotifyChanged()
    {
        var snapshot = GetSnapshot();
        CaptionLinesChanged?.Invoke(this, new CaptionStoreSnapshotChangedEventArgs(snapshot));
    }

    private readonly record struct CaptionKey(AudioChannelId ChannelId, long Sequence);

    private sealed class CaptionAggregate
    {
        private readonly AudioChannelId _channelId;

        public CaptionAggregate(AudioChannelId channelId)
        {
            _channelId = channelId;
        }

        public Guid Id { get; } = Guid.NewGuid();
        public string SourceText { get; private set; } = string.Empty;
        public string TranslatedText { get; private set; } = string.Empty;
        public SegmentStability SourceStability { get; private set; } = SegmentStability.Interim;
        public SegmentStability TranslatedStability { get; private set; } = SegmentStability.Interim;
        public DateTimeOffset LastUpdatedAt { get; private set; } = DateTimeOffset.UtcNow;

        public void ApplySegment(TranslationSegment segment, DateTimeOffset observedAt)
        {
            var sourceIncoming = segment.SourceText;
            var translatedIncoming = segment.TranslatedText;

            if (!string.IsNullOrEmpty(sourceIncoming))
            {
                SourceText = sourceIncoming;
                SourceStability = segment.Stability == SegmentStability.Final || SourceStability == SegmentStability.Final
                    ? SegmentStability.Final
                    : SegmentStability.Interim;
            }

            if (!string.IsNullOrEmpty(translatedIncoming))
            {
                TranslatedText = translatedIncoming;
                TranslatedStability = segment.Stability == SegmentStability.Final || TranslatedStability == SegmentStability.Final
                    ? SegmentStability.Final
                    : SegmentStability.Interim;
            }

            if (sourceIncoming is not null && translatedIncoming is not null)
            {
                if (segment.Stability == SegmentStability.Final)
                {
                    SourceStability = SegmentStability.Final;
                    TranslatedStability = SegmentStability.Final;
                }
            }

            LastUpdatedAt = observedAt;
        }

        public void ApplyRecognitionSegment(SpeechRecognitionSegment segment, DateTimeOffset observedAt)
        {
            if (!string.IsNullOrEmpty(segment.Text))
            {
                SourceText = segment.Text;
                SourceStability = segment.Stability == SegmentStability.Final || SourceStability == SegmentStability.Final
                    ? SegmentStability.Final
                    : SegmentStability.Interim;
            }

            LastUpdatedAt = observedAt;
        }

        public CaptionLine ToLine(ChannelLabelProvider provider)
        {
            return new CaptionLine(
                Id,
                _channelId,
                provider.GetLabel(_channelId),
                SourceText,
                TranslatedText,
                ResolveStability(),
                LastUpdatedAt);
        }

        private SegmentStability ResolveStability()
            => SourceStability == SegmentStability.Final || TranslatedStability == SegmentStability.Final
                ? SegmentStability.Final
                : SegmentStability.Interim;
    }
}

public sealed class CaptionStoreSnapshotChangedEventArgs : EventArgs
{
    public IReadOnlyList<CaptionLine> Lines { get; }

    public CaptionStoreSnapshotChangedEventArgs(IReadOnlyList<CaptionLine> lines)
    {
        Lines = lines;
    }
}
