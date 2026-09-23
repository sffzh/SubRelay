using System;
using System.Collections.Generic;

namespace GameSubRelay.Infrastructure.SpeechRecognition.Volcengine;

public sealed record VolcengineStreamingAsrOptions
{
    public const string BigAsr1DurationResourceId = "volc.bigasr.sauc.duration";
    public const string BigAsr1ConcurrentResourceId = "volc.bigasr.sauc.concurrent";
    public const string SeedAsr2DurationResourceId = "volc.seedasr.sauc.duration";
    public const string SeedAsr2ConcurrentResourceId = "volc.seedasr.sauc.concurrent";

    public static Uri BidirectionalEndpoint { get; } =
        new("wss://openspeech.bytedance.com/api/v3/sauc/bigmodel");

    public static Uri OptimizedBidirectionalEndpoint { get; } =
        new("wss://openspeech.bytedance.com/api/v3/sauc/bigmodel_async");

    public static Uri StreamingInputEndpoint { get; } =
        new("wss://openspeech.bytedance.com/api/v3/sauc/bigmodel_nostream");

    public VolcengineStreamingAsrOptions(
        string AppKey,
        string AccessKey,
        string ResourceId = BigAsr1DurationResourceId,
        Uri? Endpoint = null)
    {
        this.AppKey = AppKey.Trim();
        this.AccessKey = AccessKey.Trim();
        this.ResourceId = ResourceId.Trim();
        this.Endpoint = Endpoint ?? OptimizedBidirectionalEndpoint;
    }

    public string AppKey { get; init; }

    public string AccessKey { get; init; }

    public string ResourceId { get; init; }

    public Uri Endpoint { get; init; }

    public IReadOnlyDictionary<string, string> CreateHeaders(string connectId)
    {
        EnsureCredentialsPresent();

        if (string.IsNullOrWhiteSpace(connectId))
        {
            throw new ArgumentException("Connect id is required for streaming ASR tracing.", nameof(connectId));
        }

        return new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["X-Api-Key"] = AppKey,
            ["X-Api-App-Key"] = AppKey,
            ["X-Api-Access-Key"] = AccessKey,
            ["X-Api-Resource-Id"] = ResourceId,
            ["X-Api-Connect-Id"] = connectId
        };
    }

    public void EnsureCredentialsPresent()
    {
        if (string.IsNullOrWhiteSpace(AppKey))
        {
            throw new InvalidOperationException("Volcengine streaming ASR app key is required.");
        }

        if (string.IsNullOrWhiteSpace(AccessKey))
        {
            throw new InvalidOperationException("Volcengine streaming ASR access key is required.");
        }

        if (string.IsNullOrWhiteSpace(ResourceId))
        {
            throw new InvalidOperationException("Volcengine streaming ASR resource id is required.");
        }
    }
}
