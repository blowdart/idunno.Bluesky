// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Net.WebSockets;
using Microsoft.Extensions.Logging;
using ZstdSharp;

namespace idunno.AtProto.Jetstream
{
    internal static partial class JetStreamLogger
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

        [LoggerMessage(12, LogLevel.Warning, "{fault} logged. Current fault count is {currentCount}")]
        internal static partial void FaultLogged(ILogger logger, string fault, int currentCount);

        [LoggerMessage(13, LogLevel.Debug, "WSS: WebSocket created")]
        internal static partial void InternalClientWebSocketCreated(ILogger logger);

        [LoggerMessage(14, LogLevel.Debug, "Sent OptionsUpdate message")]
        internal static partial void OptionsUpdateMessageSent(ILogger logger);

        [LoggerMessage(15, LogLevel.Warning, "WSS: Unexpected close message received")]
        internal static partial void CloseMessageReceived(ILogger logger);

        [LoggerMessage(16, LogLevel.Warning, "WSS: Unexpected message type {webSocketMessageType}")]
        internal static partial void UnexpectedMessageType(ILogger logger, WebSocketMessageType webSocketMessageType);

        [LoggerMessage(17, LogLevel.Information, "Attempting to reconnect {retry} of {maximumReconnectionRetries} to the jetstream")]
        internal static partial void AttemptingReconnect(ILogger logger, int retry, int maximumReconnectionRetries);

        [LoggerMessage(18, LogLevel.Error, "WSS: Exception thrown")]
        internal static partial void WebSocketException(ILogger logger, WebSocketException exception);

        [LoggerMessage(19, LogLevel.Information, "Reconnection cancelled")]
        internal static partial void ReconnectionCancelled(ILogger logger);

        [LoggerMessage(20, LogLevel.Information, "Reconnected")]
        internal static partial void Reconnected(ILogger logger);

        [LoggerMessage(21, LogLevel.Error, "Reconnection failed {state}")]
        internal static partial void ReconnectionFailed(ILogger logger, WebSocketState state);

        [LoggerMessage(22, LogLevel.Error, "Reconnection threw")]
        internal static partial void ReconnectionThrew(ILogger logger, Exception ex);

        [LoggerMessage(23, LogLevel.Error, "Decompression failed")]
        internal static partial void DecompressionException(ILogger logger, ZstdException ex);

    }
}
