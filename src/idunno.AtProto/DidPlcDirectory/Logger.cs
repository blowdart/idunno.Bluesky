// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Net;
using idunno.AtProto;
using Microsoft.Extensions.Logging;

namespace idunno.DidPlcDirectory
{
    internal static partial class Logger
    {
        // Agent logging
        [LoggerMessage(1, LogLevel.Debug, "ResolveDidDocument called for {did} on {directory}")]
        internal static partial void ResolveDidDocumentCalled(ILogger logger, Did did, Uri directory);

        [LoggerMessage(2, LogLevel.Error, "ResolveDidDocument failed for {did} on {directory} with {statusCode}")]
        internal static partial void ResolveDidDocumentFailed(ILogger logger, Did did, Uri directory, HttpStatusCode statusCode);

        // Server logging
        [LoggerMessage(10, LogLevel.Debug, "Resolving DidDoc for {did} as plc client on {directory}")]
        internal static partial void ResolvingPlcDid(ILogger logger, Did did, Uri directory);

        [LoggerMessage(11, LogLevel.Debug, "Resolving DidDoc for {did} as plc client on {directory}")]
        internal static partial void ResolvingWebDid(ILogger logger, Did did, Uri directory);

        [LoggerMessage(12, LogLevel.Error, "Cannot resolve DidDoc, unknown {did} type")]
        internal static partial void UnknownDidType(ILogger logger, Did did);
    }
}
