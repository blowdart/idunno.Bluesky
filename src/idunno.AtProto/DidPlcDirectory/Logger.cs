// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Net;
using idunno.AtProto;
using Microsoft.Extensions.Logging;

namespace idunno.DidPlcDirectory
{
    internal static partial class Logger
    {
        [LoggerMessage(1, LogLevel.Debug, "ResolveDidDocument called for {did} on {directory}")]
        internal static partial void ResolveDidDocumentCalled(ILogger logger, Did did, Uri directory);

        [LoggerMessage(2, LogLevel.Error, "ResolveDidDocument failed for {did} on {directory} with {statusCode}")]
        internal static partial void ResolveDidDocumentFailed(ILogger logger, Did did, Uri directory, HttpStatusCode statusCode);
    }
}
