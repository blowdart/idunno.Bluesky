// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Net.WebSockets;

using Microsoft.Extensions.Logging;

namespace idunno.AtProto.Firehose;

internal static partial class FirehoseLogger
{
    [LoggerMessage(1, LogLevel.Information, "Connecting to {uri}")]
    internal static partial void ConnectingTo(ILogger logger, Uri uri);

    [LoggerMessage(2, LogLevel.Debug, "Connected to {uri}")]
    internal static partial void Connected(ILogger logger, Uri uri);

    [LoggerMessage(3, LogLevel.Warning, "Firehose server refused the connection with status {statusCode}")]
    internal static partial void ConnectionRefused(ILogger logger, int statusCode);

    [LoggerMessage(4, LogLevel.Warning, "Firehose connection failed")]
    internal static partial void ConnectionFailed(ILogger logger, Exception exception);

    [LoggerMessage(5, LogLevel.Information, "Reconnecting to the firehose, attempt {attempt}, in {delay}")]
    internal static partial void Reconnecting(ILogger logger, int attempt, TimeSpan delay);

    [LoggerMessage(6, LogLevel.Error, "Firehose protocol error, dropping the connection")]
    internal static partial void ProtocolError(ILogger logger, Exception exception);

    [LoggerMessage(7, LogLevel.Warning, "Firehose server sent error {error}: {message}")]
    internal static partial void ErrorFrameReceived(ILogger logger, string? error, string? message);

    [LoggerMessage(8, LogLevel.Debug, "Skipped a frame with unknown operation {operation}")]
    internal static partial void UnknownOperation(ILogger logger, long operation);

    [LoggerMessage(9, LogLevel.Debug, "Received a message with unknown type {type}")]
    internal static partial void UnknownMessageType(ILogger logger, string type);

    [LoggerMessage(10, LogLevel.Warning, "Invalid {type} event at sequence {sequence}: {reason}")]
    internal static partial void InvalidEvent(ILogger logger, string type, long? sequence, string reason);

    [LoggerMessage(11, LogLevel.Warning, "No frame received from the firehose within {timeout}, reconnecting")]
    internal static partial void IdleTimeout(ILogger logger, TimeSpan timeout);

    [LoggerMessage(12, LogLevel.Warning, "Connecting to the firehose over an insecure connection, {uri}")]
    internal static partial void InsecureConnection(ILogger logger, Uri uri);

    [LoggerMessage(13, LogLevel.Debug, "Error closing the firehose connection")]
    internal static partial void CloseError(ILogger logger, Exception exception);

    [LoggerMessage(14, LogLevel.Debug, "Dropped the resumed event at cursor {sequence}")]
    internal static partial void CursorEventDropped(ILogger logger, long sequence);

    [LoggerMessage(15, LogLevel.Information, "Firehose server closed the connection, {closeStatus} {description}")]
    internal static partial void ServerClosed(ILogger logger, WebSocketCloseStatus? closeStatus, string? description);

    [LoggerMessage(16, LogLevel.Error, "Firehose server redirected the connection, status {statusCode}, and redirects are not followed")]
    internal static partial void ConnectionRedirected(ILogger logger, int? statusCode);

    [LoggerMessage(17, LogLevel.Debug, "Dropped a repeated event at sequence {sequence}")]
    internal static partial void RepeatedEventDropped(ILogger logger, long sequence);
}
