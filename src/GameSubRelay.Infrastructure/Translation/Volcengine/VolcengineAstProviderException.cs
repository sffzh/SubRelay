using System;

namespace GameSubRelay.Infrastructure.Translation.Volcengine;

public enum VolcengineAstErrorKind
{
    Unknown,
    BadRequest,
    Authentication,
    Authorization,
    RateLimited,
    ReconnectRequired,
    RetryableServer,
    Protocol
}

public sealed class VolcengineAstProviderException : Exception
{
    public VolcengineAstProviderException(
        VolcengineAstErrorKind kind,
        int? statusCode,
        string message,
        bool isRetryable,
        Exception? innerException = null)
        : base(message, innerException)
    {
        Kind = kind;
        StatusCode = statusCode;
        IsRetryable = isRetryable;
    }

    public VolcengineAstErrorKind Kind { get; }

    public int? StatusCode { get; }

    public bool IsRetryable { get; }

    public static VolcengineAstProviderException FromSessionFailed(AstResponseMeta? responseMeta)
    {
        var statusCode = responseMeta?.StatusCode;
        var serverMessage = string.IsNullOrWhiteSpace(responseMeta?.Message)
            ? "Volcengine AST session failed."
            : responseMeta!.Message;

        var (kind, retryable) = MapStatusCode(statusCode);
        return new VolcengineAstProviderException(kind, statusCode, serverMessage, retryable);
    }

    public static VolcengineAstProviderException Protocol(string message, Exception? innerException = null)
    {
        return new VolcengineAstProviderException(
            VolcengineAstErrorKind.Protocol,
            statusCode: null,
            message,
            isRetryable: false,
            innerException);
    }

    private static (VolcengineAstErrorKind Kind, bool IsRetryable) MapStatusCode(int? statusCode)
    {
        return statusCode switch
        {
            400 or -400 => (VolcengineAstErrorKind.BadRequest, false),
            401 or -401 => (VolcengineAstErrorKind.Authentication, false),
            403 or -403 => (VolcengineAstErrorKind.Authorization, false),
            429 or -429 => (VolcengineAstErrorKind.RateLimited, true),
            301 or -301 => (VolcengineAstErrorKind.ReconnectRequired, true),
            >= 500 => (VolcengineAstErrorKind.RetryableServer, true),
            <= -500 => (VolcengineAstErrorKind.RetryableServer, true),
            _ => (VolcengineAstErrorKind.Unknown, false)
        };
    }
}
