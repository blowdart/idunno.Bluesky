// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Net.WebSockets;

using Microsoft.Extensions.Logging;

using ZstdSharp;

namespace idunno.AtProto.Jetstream;

internal static partial class JetStreamLogger
{
    [LoggerMessage(1, LogLevel.Information, "Connecting to {uri}")]
    internal static partial void ConnectingTo(ILogger logger, Uri uri);

    [LoggerMessage(2, LogLevel.Error, "Error on client close")]
    internal static partial void CloseError(ILogger logger, Exception ex);

    [LoggerMessage(3, LogLevel.Error, "Error in message loop")]
    internal static partial void MessageLoopError(ILogger logger, Exception ex);

    [LoggerMessage(5, LogLevel.Debug, "Client state changed to {state}")]
    internal static partial void ClientStateChanged(ILogger logger, WebSocketState state);

    [LoggerMessage(6, LogLevel.Warning, "ParseMessage passed null or empty message")]
    internal static partial void ParseMessageGotNullOrEmptyMessage(ILogger logger);

    [LoggerMessage(7, LogLevel.Warning, "ParseMessage could not parse {json}")]
    internal static partial void ParseMessageCouldNotProcessAsJson(ILogger logger, string json, Exception ex);

    [LoggerMessage(8, LogLevel.Error, "ParseMessage threw exception")]
    internal static partial void ParseMessageThrewException(ILogger logger, Exception ex);

    [LoggerMessage(9, LogLevel.Warning, "ParseMessage json deserializer returned null for {json}")]
    internal static partial void ParseMessageDeserializationReturnedNull(ILogger logger, string json);

    [LoggerMessage(10, LogLevel.Debug, "ReceiveLoop could not convert message to string")]
    internal static partial void MessageLoopFailedToConvert(ILogger logger);

    [LoggerMessage(11, LogLevel.Error, "Exception when calling _client.Dispose() in ConnectAsync()")]
    internal static partial void ErrorDisposingClientInConnectAsync(ILogger logger, Exception ex);

    [LoggerMessage(13, LogLevel.Debug, "WSS: WebSocket created")]
    internal static partial void InternalClientWebSocketCreated(ILogger logger);

    [LoggerMessage(14, LogLevel.Debug, "Sent OptionsUpdate message")]
    internal static partial void OptionsUpdateMessageSent(ILogger logger);

    [LoggerMessage(15, LogLevel.Warning, "WSS: Unexpected close message received")]
    internal static partial void CloseMessageReceived(ILogger logger);

    [LoggerMessage(16, LogLevel.Warning, "WSS: Unexpected message type {webSocketMessageType}")]
    internal static partial void UnexpectedMessageType(ILogger logger, WebSocketMessageType webSocketMessageType);

    [LoggerMessage(18, LogLevel.Error, "WSS: Exception thrown")]
    internal static partial void WebSocketException(ILogger logger, WebSocketException exception);

    [LoggerMessage(23, LogLevel.Error, "Decompression failed")]
    internal static partial void DecompressionException(ILogger logger, ZstdException ex);

    [LoggerMessage(24, LogLevel.Error, "Connection failed {state}")]
    internal static partial void ConnectionFailed(ILogger logger, WebSocketState state);

    [LoggerMessage(25, LogLevel.Warning, "The close handshake was not answered within {closeTimeout}, aborting the connection")]
    internal static partial void CloseTimedOut(ILogger logger, TimeSpan closeTimeout);

    [LoggerMessage(26, LogLevel.Warning, "The reply to a server initiated close could not be sent within {sendTimeout}, aborting the connection")]
    internal static partial void CloseReplyTimedOut(ILogger logger, TimeSpan sendTimeout);

    [LoggerMessage(27, LogLevel.Warning, "An options update message could not be sent within {sendTimeout} and was abandoned")]
    internal static partial void OptionsUpdateMessageTimedOut(ILogger logger, TimeSpan sendTimeout);

    [LoggerMessage(28, LogLevel.Error, "A RecordReceived handler threw an exception")]
    internal static partial void RecordReceivedHandlerThrew(ILogger logger, Exception ex);

    [LoggerMessage(29, LogLevel.Error, "A message parser could not be started, so the message was dropped")]
    internal static partial void CouldNotStartMessageParser(ILogger logger, Exception ex);

    [LoggerMessage(30, LogLevel.Error, "The receive loop failed {maximumConsecutiveFailures} times in a row, ending the connection")]
    internal static partial void TooManyConsecutiveReceiveFailures(ILogger logger, int maximumConsecutiveFailures);

    [LoggerMessage(31, LogLevel.Warning, "Connecting to {uri} without transport security. The dids and collections being subscribed to will be sent in plain text.")]
    internal static partial void ConnectingWithoutTransportSecurity(ILogger logger, Uri uri);
}