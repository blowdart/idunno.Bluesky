// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

using System.Net;

namespace idunno.AtProto.OAuthCallback;

internal static partial class Logger
{
    [LoggerMessage(1, LogLevel.Debug, "Callback listener statred on {listeningOn}")]
    internal static partial void ListeningOn(ILogger logger, Uri listeningOn);

    [LoggerMessage(2, LogLevel.Debug, "Awaiting callback for {timeout} seconds")]
    internal static partial void AwaitingCallback(ILogger logger, int timeout);

    [LoggerMessage(3, LogLevel.Debug, "Received callback")]
    internal static partial void ReceivedCallback(ILogger logger);

    [LoggerMessage(4, LogLevel.Error, "Received callback with no querystring")]
    internal static partial void ReceivedCallbackWithNoQuerystring(ILogger logger);

    [LoggerMessage(5, LogLevel.Error, "BadRequest made to {path}")]
    internal static partial void BadRequest(ILogger logger, PathString path);

    [LoggerMessage(7, LogLevel.Error, "PUT request made to {path}")]
    internal static partial void MethodNotAllowed(ILogger logger, PathString path);

    [LoggerMessage(10, LogLevel.Error, "Internal listener is null, cancelling task")]
    internal static partial void ListenerIsNull(ILogger logger);

    [LoggerMessage(11, LogLevel.Error, "Callback listener terminated unexpectedly")]
    internal static partial void ListenerFaulted(ILogger logger, Exception exception);

    [LoggerMessage(12, LogLevel.Debug, "Callback is already being awaited, returning the existing task")]
    internal static partial void CallbackAlreadyAwaited(ILogger logger);

    [LoggerMessage(13, LogLevel.Warning, "Timed out waiting for callback")]
    internal static partial void CallbackTimedOut(ILogger logger);

    [LoggerMessage(14, LogLevel.Error, "Rejected callback request from non loopback address {remoteAddress}")]
    internal static partial void NonLoopbackRequestRejected(ILogger logger, IPAddress remoteAddress);
}