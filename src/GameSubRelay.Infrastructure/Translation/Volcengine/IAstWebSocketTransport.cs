using System;
using System.Collections.Generic;
using System.IO;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;

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

    public ClientWebSocketAstTransport()
        : this(new ClientWebSocket())
    {
    }

    public ClientWebSocketAstTransport(ClientWebSocket webSocket)
    {
        _webSocket = webSocket;
    }

    public async Task ConnectAsync(
        Uri endpoint,
        IReadOnlyDictionary<string, string> headers,
        CancellationToken cancellationToken)
    {
        foreach (var header in headers)
        {
            _webSocket.Options.SetRequestHeader(header.Key, header.Value);
        }

        await _webSocket.ConnectAsync(endpoint, cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask SendAsync(ReadOnlyMemory<byte> payload, CancellationToken cancellationToken)
    {
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
                return stream.ToArray();
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
    }
}
