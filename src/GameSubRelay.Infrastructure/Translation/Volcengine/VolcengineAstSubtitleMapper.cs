using System;
using System.Collections.Generic;
using GameSubRelay.Core.Audio;
using GameSubRelay.Core.Captions;
using GameSubRelay.Core.Translation;

namespace GameSubRelay.Infrastructure.Translation.Volcengine;

public sealed class VolcengineAstSubtitleMapper
{
    private readonly AudioChannelId _channelId;
    private readonly string _sourceLanguage;
    private readonly string _targetLanguage;
    private long _lastSequence;
    private SentenceState? _current;

    public VolcengineAstSubtitleMapper(
        AudioChannelId channelId,
        string sourceLanguage,
        string targetLanguage)
    {
        _channelId = channelId;
        _sourceLanguage = sourceLanguage;
        _targetLanguage = targetLanguage;
    }

    public IReadOnlyList<TranslationSegment> Apply(AstServerMessage message)
    {
        if (message.Event == AstServerEventType.SessionFailed)
        {
            throw VolcengineAstProviderException.FromSessionFailed(message.ResponseMeta);
        }

        return message.Event switch
        {
            AstServerEventType.SourceSubtitleStart => ApplySourceStart(message),
            AstServerEventType.SourceSubtitleResponse => ApplySourceResponse(message),
            AstServerEventType.SourceSubtitleEnd => ApplySourceEnd(message),
            AstServerEventType.TranslationSubtitleStart => ApplyTranslationStart(message),
            AstServerEventType.TranslationSubtitleResponse => ApplyTranslationResponse(message),
            AstServerEventType.TranslationSubtitleEnd => ApplyTranslationEnd(message),
            _ => Array.Empty<TranslationSegment>()
        };
    }

    private IReadOnlyList<TranslationSegment> ApplySourceStart(AstServerMessage message)
    {
        var state = GetCurrentSentence();
        state.SourceStartMs = message.StartTimeMs ?? state.SourceStartMs;
        return Array.Empty<TranslationSegment>();
    }

    private IReadOnlyList<TranslationSegment> ApplySourceResponse(AstServerMessage message)
    {
        var state = GetCurrentSentence();
        state.SourceText = NormalizeText(message.Text, state.SourceText);
        return EmitIfReady(state);
    }

    private IReadOnlyList<TranslationSegment> ApplySourceEnd(AstServerMessage message)
    {
        var state = GetCurrentSentence();
        state.SourceStartMs = message.StartTimeMs ?? state.SourceStartMs;
        state.SourceEndMs = message.EndTimeMs ?? state.SourceEndMs;
        state.SourceText = NormalizeText(message.Text, state.SourceText);
        state.SourceEnded = true;
        return EmitIfReady(state);
    }

    private IReadOnlyList<TranslationSegment> ApplyTranslationStart(AstServerMessage message)
    {
        var state = GetCurrentSentence();
        state.TranslationStartMs = message.StartTimeMs ?? state.TranslationStartMs;
        return Array.Empty<TranslationSegment>();
    }

    private IReadOnlyList<TranslationSegment> ApplyTranslationResponse(AstServerMessage message)
    {
        var state = GetCurrentSentence();
        state.TranslationText = NormalizeText(message.Text, state.TranslationText);
        return EmitIfReady(state);
    }

    private IReadOnlyList<TranslationSegment> ApplyTranslationEnd(AstServerMessage message)
    {
        var state = GetCurrentSentence();
        state.TranslationStartMs = message.StartTimeMs ?? state.TranslationStartMs;
        state.TranslationEndMs = message.EndTimeMs ?? state.TranslationEndMs;
        state.TranslationText = NormalizeText(message.Text, state.TranslationText);
        state.TranslationEnded = true;
        return EmitIfReady(state);
    }

    private SentenceState GetCurrentSentence()
    {
        if (_current is null || _current.IsComplete)
        {
            _current = new SentenceState(++_lastSequence);
        }

        return _current;
    }

    private IReadOnlyList<TranslationSegment> EmitIfReady(SentenceState state)
    {
        if (string.IsNullOrWhiteSpace(state.SourceText) ||
            string.IsNullOrWhiteSpace(state.TranslationText))
        {
            return Array.Empty<TranslationSegment>();
        }

        var beginMs = state.SourceStartMs
            ?? state.TranslationStartMs
            ?? 0;
        var endMs = Math.Max(
            state.SourceEndMs ?? beginMs,
            state.TranslationEndMs ?? beginMs);

        var stability = state.IsComplete
            ? SegmentStability.Final
            : SegmentStability.Interim;

        return new[]
        {
            new TranslationSegment(
                _channelId,
                state.Sequence,
                _sourceLanguage,
                _targetLanguage,
                state.SourceText,
                state.TranslationText,
                stability,
                TimeSpan.FromMilliseconds(beginMs),
                TimeSpan.FromMilliseconds(endMs))
        };
    }

    private static string NormalizeText(string? text, string fallback)
    {
        return string.IsNullOrWhiteSpace(text) ? fallback : text.Trim();
    }

    private sealed class SentenceState
    {
        public SentenceState(long sequence)
        {
            Sequence = sequence;
        }

        public long Sequence { get; }

        public string SourceText { get; set; } = string.Empty;

        public string TranslationText { get; set; } = string.Empty;

        public int? SourceStartMs { get; set; }

        public int? SourceEndMs { get; set; }

        public int? TranslationStartMs { get; set; }

        public int? TranslationEndMs { get; set; }

        public bool SourceEnded { get; set; }

        public bool TranslationEnded { get; set; }

        public bool IsComplete => SourceEnded && TranslationEnded;
    }
}
