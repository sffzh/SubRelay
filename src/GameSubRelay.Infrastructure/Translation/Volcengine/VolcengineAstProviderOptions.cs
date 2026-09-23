using System;
using System.Collections.Generic;

namespace GameSubRelay.Infrastructure.Translation.Volcengine;

public sealed record VolcengineAstProviderOptions
{
    public const string DefaultResourceId = "volc.service_type.10053";
    public const string AppKeyEnvironmentVariable = "VOLCENGINE_AST_APP_KEY";
    public const string AccessKeyEnvironmentVariable = "VOLCENGINE_AST_ACCESS_KEY";
    public const string ResourceIdEnvironmentVariable = "VOLCENGINE_AST_RESOURCE_ID";

    public static Uri DefaultEndpoint { get; } =
        new("wss://openspeech.bytedance.com/api/v4/ast/v2/translate");

    public VolcengineAstProviderOptions(
        string AppKey,
        string AccessKey,
        string? ResourceId = null,
        Uri? Endpoint = null)
    {
        this.AppKey = AppKey.Trim();
        this.AccessKey = AccessKey.Trim();
        this.ResourceId = string.IsNullOrWhiteSpace(ResourceId)
            ? DefaultResourceId
            : ResourceId.Trim();
        this.Endpoint = Endpoint ?? DefaultEndpoint;
    }

    public string AppKey { get; init; }

    public string AccessKey { get; init; }

    public string ResourceId { get; init; }

    public Uri Endpoint { get; init; }

    public static VolcengineAstProviderOptions FromEnvironment()
    {
        return new VolcengineAstProviderOptions(
            Environment.GetEnvironmentVariable(AppKeyEnvironmentVariable) ?? string.Empty,
            Environment.GetEnvironmentVariable(AccessKeyEnvironmentVariable) ?? string.Empty,
            Environment.GetEnvironmentVariable(ResourceIdEnvironmentVariable));
    }

    public IReadOnlyDictionary<string, string> CreateHeaders(string connectId)
    {
        EnsureCredentialsPresent();
        if (string.IsNullOrWhiteSpace(connectId))
        {
            throw new ArgumentException("Connect id is required for AST tracing.", nameof(connectId));
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
            throw new InvalidOperationException(
                $"Volcengine AST app key is required. Set {AppKeyEnvironmentVariable} or pass it through settings/secrets.");
        }

        if (string.IsNullOrWhiteSpace(AccessKey))
        {
            throw new InvalidOperationException(
                $"Volcengine AST access key is required. Set {AccessKeyEnvironmentVariable} or pass it through settings/secrets.");
        }

        if (string.IsNullOrWhiteSpace(ResourceId))
        {
            throw new InvalidOperationException("Volcengine AST resource id is required.");
        }
    }
}
