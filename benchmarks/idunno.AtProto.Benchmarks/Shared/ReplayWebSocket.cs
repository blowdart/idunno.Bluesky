// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Net.WebSockets;

namespace idunno.AtProto.Benchmarks;

/// <summary>
/// A <see cref="WebSocket"/> which replays a captured message as fragments no larger than the caller's buffer or
/// <see cref="FragmentSize"/>, in the way a <see cref="ClientWebSocket"/> returns a large message.
/// </summary>
internal sealed class ReplayWebSocket(WebSocketMessageType messageType = WebSocketMessageType.Binary) : WebSocket
{
    public const int FragmentSize = 8096;

    private byte[] _message = [];
    private int _offset;

    public void Load(byte[] message)
    {
        _message = message;
        _offset = 0;
    }

    public override WebSocketCloseStatus? CloseStatus => null;

    public override string? CloseStatusDescription => null;

    public override WebSocketState State => WebSocketState.Open;

    public override string? SubProtocol => null;

    public override void Abort()
    {
    }

    public override Task CloseAsync(WebSocketCloseStatus closeStatus, string? statusDescription, CancellationToken cancellationToken) => Task.CompletedTask;

    public override Task CloseOutputAsync(WebSocketCloseStatus closeStatus, string? statusDescription, CancellationToken cancellationToken) => Task.CompletedTask;

    public override void Dispose()
    {
    }

    public override Task SendAsync(ArraySegment<byte> buffer, WebSocketMessageType messageType, bool endOfMessage, CancellationToken cancellationToken) => Task.CompletedTask;

    public override Task<WebSocketReceiveResult> ReceiveAsync(ArraySegment<byte> buffer, CancellationToken cancellationToken)
    {
        int count = Next(buffer.AsSpan(), out bool endOfMessage);
        return Task.FromResult(new WebSocketReceiveResult(count, messageType, endOfMessage));
    }

    public override ValueTask<ValueWebSocketReceiveResult> ReceiveAsync(Memory<byte> buffer, CancellationToken cancellationToken)
    {
        int count = Next(buffer.Span, out bool endOfMessage);
        return new(new ValueWebSocketReceiveResult(count, messageType, endOfMessage));
    }

    private int Next(Span<byte> destination, out bool endOfMessage)
    {
        int count = Math.Min(Math.Min(destination.Length, FragmentSize), _message.Length - _offset);
        _message.AsSpan(_offset, count).CopyTo(destination);
        _offset += count;
        endOfMessage = _offset == _message.Length;
        return count;
    }
}