// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Net.WebSockets;
using System.Xml.Serialization;
using Microsoft.Extensions.Logging;

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

        [LoggerMessage(4, LogLevel.Error, "WSS operation in messageLoop has been cancelled")]
        internal static partial void MessageLoopCancellation(ILogger logger);

        [LoggerMessage(5, LogLevel.Debug, "Client state changed to {state}")]
        internal static partial void ClientStateChanged(ILogger logger, WebSocketState state);

        [LoggerMessage(6, LogLevel.Debug, "ParseMessage passed null or empty message")]
        internal static partial void ParseMessageGotNullOrEmptyMessage(ILogger logger);

        [LoggerMessage(7, LogLevel.Error, "ParseMessage could not parse {json}")]
        internal static partial void ParseMessageCouldNotProcessAsJson(ILogger logger, string json, Exception ex);

        [LoggerMessage(8, LogLevel.Error, "ParseMessage threw exception")]
        internal static partial void ParseMessageThrewException(ILogger logger, Exception ex);

        [LoggerMessage(9, LogLevel.Error, "ParseMessage json deserializer returned null for {json}")]
        internal static partial void ParseMessageDeserializationReturnedNull(ILogger logger, string json);

        [LoggerMessage(10, LogLevel.Debug, "ReceiveLoop could not convert message to string")]
        internal static partial void MessageLoopFailedToConvert(ILogger logger);

        [LoggerMessage(11, LogLevel.Error, "Exception when calling _client.Dispose() in ConnectAsync()")]
        internal static partial void ErrorDisposingClientInConnectAsync(ILogger logger, Exception ex);

        [LoggerMessage(12, LogLevel.Warning, "{fault} logged. Current fault count is {currentCount}")]
        internal static partial void FaultLogged(ILogger logger, string fault, int currentCount);

        [LoggerMessage(13, LogLevel.Debug, "ClientWebSocket created")]
        internal static partial void InternalClientWebSocketCreated(ILogger logger);

        [LoggerMessage(14, LogLevel.Debug, "Sent OptionsUpdate message")]
        internal static partial void OptionsUpdateMessageSent(ILogger logger);
    }
}
