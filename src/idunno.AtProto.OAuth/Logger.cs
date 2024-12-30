// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace idunno.AtProto.OAuth
{
    internal static partial class Logger
    {
        [LoggerMessage(1, LogLevel.Debug, "ListeningOn {listeningOn}")]
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
    }
}
