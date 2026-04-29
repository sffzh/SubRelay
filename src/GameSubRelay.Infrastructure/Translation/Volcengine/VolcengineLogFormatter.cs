using System.Text;

namespace GameSubRelay.Infrastructure.Translation.Volcengine;

internal static class VolcengineLogFormatter
{
    private const int TextPreviewLimit = 80;

    public static string FormatHeaders(IReadOnlyDictionary<string, string> headers)
    {
        var builder = new StringBuilder();
        foreach (var header in headers.OrderBy(header => header.Key, StringComparer.Ordinal))
        {
            if (builder.Length > 0)
            {
                builder.Append(", ");
            }

            builder.Append(header.Key);
            builder.Append('=');
            builder.Append(IsSensitiveHeader(header.Key) ? Redact(header.Value) : header.Value);
        }

        return builder.ToString();
    }

    public static string FormatText(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return "chars=0";
        }

        if (!ShouldLogSubtitleText())
        {
            return $"chars={text.Length}";
        }

        var preview = text.Length <= TextPreviewLimit
            ? text
            : text[..TextPreviewLimit] + "...";

        return $"chars={text.Length}, preview=\"{Escape(preview)}\"";
    }

    public static string FormatAstServerMessage(AstServerMessage message)
    {
        return $"event={message.Event}, status={message.ResponseMeta?.StatusCode.ToString() ?? "<none>"}, " +
            $"message={message.ResponseMeta?.Message ?? "<none>"}, text={FormatText(message.Text)}, " +
            $"dataBytes={message.Data?.Length ?? 0}, startMs={message.StartTimeMs?.ToString() ?? "<none>"}, " +
            $"endMs={message.EndTimeMs?.ToString() ?? "<none>"}, mutedMs={message.MutedDurationMs?.ToString() ?? "<none>"}";
    }

    public static string FormatHandshakeFailureHint(Exception exception)
    {
        var message = exception.ToString();
        if (message.Contains("401", StringComparison.Ordinal))
        {
            return "authentication failed; verify App Key, Access Key, and Resource ID for the AST/ASR product.";
        }

        if (message.Contains("403", StringComparison.Ordinal))
        {
            return "authorization failed; verify the account has access to the selected resource and endpoint.";
        }

        if (message.Contains("429", StringComparison.Ordinal))
        {
            return "rate limited; wait for quota recovery or reduce concurrent channels.";
        }

        if (message.Contains("404", StringComparison.Ordinal))
        {
            return "endpoint or resource not found; verify endpoint URL and resource ID.";
        }

        if (message.Contains("500", StringComparison.Ordinal) ||
            message.Contains("502", StringComparison.Ordinal) ||
            message.Contains("503", StringComparison.Ordinal) ||
            message.Contains("504", StringComparison.Ordinal))
        {
            return "server-side or gateway failure; retry later and keep the connection trace id from logs.";
        }

        return "handshake failed before protocol session start; inspect endpoint, network, proxy, and credentials.";
    }

    public static string Redact(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return "<empty>";
        }

        if (value.Length <= 8)
        {
            return $"***({value.Length})";
        }

        return $"{value[..4]}***{value[^4..]}({value.Length})";
    }

    private static bool ShouldLogSubtitleText()
    {
        var value = Environment.GetEnvironmentVariable("GAMESUBRELAY_LOG_SUBTITLE_TEXT");
        return string.Equals(value, "1", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(value, "true", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(value, "yes", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsSensitiveHeader(string headerName)
    {
        return headerName.Contains("Key", StringComparison.OrdinalIgnoreCase) ||
            headerName.Contains("Token", StringComparison.OrdinalIgnoreCase);
    }

    private static string Escape(string value)
    {
        return value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal)
            .Replace("\r", "\\r", StringComparison.Ordinal)
            .Replace("\n", "\\n", StringComparison.Ordinal);
    }
}
