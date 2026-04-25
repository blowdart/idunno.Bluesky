// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Net.WebSockets;
using Microsoft.Extensions.Logging;

namespace idunno.AtProto.FireHose;

internal static partial class FireHoseLogger
{
    [LoggerMessage(1, LogLevel.Information, "Connecting to {uri}")]
    internal static partial void ConnectingTo(ILogger logger, Uri uri);

    [LoggerMessage(2, LogLevel.Error, "Error on client close")]
    internal static partial void CloseError(ILogger logger, Exception ex);

    [LoggerMessage(3, LogLevel.Error, "Error in message loop")]
    internal static partial void MessageLoopError(ILogger logger, Exception ex);

    [LoggerMessage(4, LogLevel.Information, "WSS operation in messageLoop has been cancelled")]
    internal static partial void MessageLoopCancellation(ILogger logger);

    [LoggerMessage(5, LogLevel.Debug, "Client state changed to {state}")]
    internal static partial void ClientStateChanged(ILogger logger, WebSocketState state);

    [LoggerMessage(11, LogLevel.Error, "Exception when calling _client.Dispose() in ConnectAsync()")]
    internal static partial void ErrorDisposingClientInConnectAsync(ILogger logger, Exception ex);

    [LoggerMessage(13, LogLevel.Debug, "WSS: WebSocket created")]
    internal static partial void InternalClientWebSocketCreated(ILogger logger);

    [LoggerMessage(15, LogLevel.Warning, "WSS: Unexpected close message received")]
    internal static partial void CloseMessageReceived(ILogger logger);

    [LoggerMessage(16, LogLevel.Warning, "WSS: Unexpected message type {webSocketMessageType}")]
    internal static partial void UnexpectedMessageType(ILogger logger, WebSocketMessageType webSocketMessageType);

    [LoggerMessage(18, LogLevel.Error, "WSS: Exception thrown")]
    internal static partial void WebSocketException(ILogger logger, WebSocketException exception);

    [LoggerMessage(24, LogLevel.Error, "Connection failed {state}")]
    internal static partial void ConnectionFailed(ILogger logger, WebSocketState state);

    [LoggerMessage(25, LogLevel.Warning, "WSS: Message size was 0.")]
    internal static partial void WebSocketMessageHadZeroLength(ILogger logger);

    [LoggerMessage(26, LogLevel.Warning, "CBOR: Cannot decode message.")]
    internal static partial void CborCannotDecodeMessage(ILogger logger, Exception ex);

    [LoggerMessage(27, LogLevel.Warning, "WSS: Message decode returned an unexpected number of objects ({count}).")]
    internal static partial void WebSocketMessageDecodeUnexpectedStructure(ILogger logger, int count);

    [LoggerMessage(28, LogLevel.Warning, "WSS: Message decode returned null.")]
    internal static partial void WebSocketMessageDecodeNullStructure(ILogger logger);

    [LoggerMessage(29, LogLevel.Warning, "WSS: Exception thrown when parsing.")]
    internal static partial void WebSocketParsingException(ILogger logger, Exception ex);

    [LoggerMessage(30, LogLevel.Warning, "Unknown operation type when parsing RepoMessage.")]
    internal static partial void RepoMessageUnknownOperation(ILogger logger);

    [LoggerMessage(31, LogLevel.Warning, "Unknown header type, {type}, when parsing RepoMessage frame.")]
    internal static partial void RepoMessageUnknownFrameHeaderType(ILogger logger, string type);

    [LoggerMessage(32, LogLevel.Warning, "RepoMessage header could not be decoded.")]
    internal static partial void RepoMessageFrameHeaderCouldNotDecoded(ILogger logger);

    [LoggerMessage(33, LogLevel.Warning, "RepoMessage frame header type is null.")]
    internal static partial void RepoMessageFrameHeaderTypeNull(ILogger logger);
}
