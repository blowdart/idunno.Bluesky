// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Extensions.Logging;

namespace idunno.AtProto;

internal static partial class DidHandleCacheLogger
{
    [LoggerMessage(1, LogLevel.Debug, "Resolution of the handle for {did} failed")]
    internal static partial void ResolutionFailed(ILogger logger, Exception exception, Did did);

    [LoggerMessage(2, LogLevel.Debug, "Resolution of the handle for {did} timed out")]
    internal static partial void ResolutionTimedOut(ILogger logger, Did did);

    [LoggerMessage(3, LogLevel.Debug, "No verified handle for {did}")]
    internal static partial void HandleNotVerified(ILogger logger, Did did);
}
