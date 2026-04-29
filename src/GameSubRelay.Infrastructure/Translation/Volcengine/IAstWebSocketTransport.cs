using System;
using System.Collections.Generic;
using System.IO;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace GameSubRelay.Infrastructure.Translation.Volcengine;

public interface IAstWebSocketTransport : IAsyncDisposable
{
    Task ConnectAsync(
        Uri endpoint,
        IReadOnlyDictionary<string, string> headers,
        CancellationToken cancellationToken);

    ValueTask SendAsync(ReadOnlyMemory<byte> payload, CancellationToken cancellationToken);

    ValueTask<byte[]?> ReceiveAsync(CancellationToken cancellationToken);
}

public sealed class ClientWebSocketAstTransport : IAstWebSocketTransport
{
    private readonly ClientWebSocket _webSocket;
    private readonly ILogger<ClientWebSocketAstTransport>? _logger;

    public ClientWebSocketAstTransport()
        : this(new ClientWebSocket())
    {
    }

    public ClientWebSocketAstTransport(ILogger<ClientWebSocketAstTransport>? logger)
        : this(new ClientWebSocket(), logger)
    {
    }

    public ClientWebSocketAstTransport(
        ClientWebSocket webSocket,
        ILogger<ClientWebSocketAstTransport>? logger = null)
    {
        _webSocket = webSocket;
        _logger = logger;
    }

    public async Task ConnectAsync(
        Uri endpoint,
        IReadOnlyDictionary<string, string> headers,
        CancellationToken cancellationToken)
    {
        _logger?.LogInformation(
            "AST WebSocket connecting to {Endpoint}; headers: {Headers}",
            endpoint,
            VolcengineLogFormatter.FormatHeaders(headers));

        foreach (var header in headers)
        {
            _webSocket.Options.SetRequestHeader(header.Key, header.Value);
        }

        try
        {
            await _webSocket.ConnectAsync(endpoint, cancellationToken).ConfigureAwait(false);
            _logger?.LogInformation(
                "AST WebSocket connected to {Endpoint}; state={State}",
                endpoint,
                _webSocket.State);
        }
        catch (Exception ex)
        {
            _logger?.LogError(
                ex,
                "AST WebSocket handshake failed for {Endpoint}; hint={Hint}; headers: {Headers}",
                endpoint,
                VolcengineLogFormatter.FormatHandshakeFailureHint(ex),
                VolcengineLogFormatter.FormatHeaders(headers));
            throw;
        }
    }

    public async ValueTask SendAsync(ReadOnlyMemory<byte> payload, CancellationToken cancellationToken)
    {
        _logger?.LogDebug("AST WebSocket sending {PayloadBytes} bytes.", payload.Length);
        await _webSocket
            .SendAsync(payload, WebSocketMessageType.Binary, endOfMessage: true, cancellationToken)
            .ConfigureAwait(false);
    }

    public async ValueTask<byte[]?> ReceiveAsync(CancellationToken cancellationToken)
    {
        var buffer = new byte[16 * 1024];
        using var stream = new MemoryStream();

        while (true)
        {
            var result = await _webSocket
                .ReceiveAsync(buffer, cancellationToken)
                .ConfigureAwait(false);

            if (result.MessageType == WebSocketMessageType.Close)
            {
                _logger?.LogInformation(
                    "AST WebSocket close received: status={CloseStatus}, description={CloseDescription}.",
                    result.CloseStatus,
                    result.CloseStatusDescription);
                return null;
            }

            if (result.MessageType != WebSocketMessageType.Binary)
            {
                throw VolcengineAstProviderException.Protocol(
                    $"Volcengine AST returned unsupported WebSocket message type {result.MessageType}.");
            }

            stream.Write(buffer, 0, result.Count);

            if (result.EndOfMessage)
            {
                var payload = stream.ToArray();
                _logger?.LogDebug("AST WebSocket received {PayloadBytes} bytes.", payload.Length);
                return payload;
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_webSocket.State is WebSocketState.Open or WebSocketState.CloseReceived)
        {
            try
            {
                await _webSocket
                    .CloseAsync(WebSocketCloseStatus.NormalClosure, "disposing", CancellationToken.None)
                    .ConfigureAwait(false);
            }
            catch (WebSocketException)
            {
                // Closing during network teardown is best-effort.
            }
        }

        _webSocket.Dispose();
        _logger?.LogInformation("AST WebSocket disposed.");
    }
}
