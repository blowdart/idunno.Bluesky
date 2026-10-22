// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Extensions.Logging;

namespace idunno.Bluesky.Labeler.Storage;

internal static partial class StoreLog
{
    [LoggerMessage(1, LogLevel.Information, "Label storage schema initialization completed for {Provider}.")]
    internal static partial void Initialized(ILogger logger, string provider);

    [LoggerMessage(2, LogLevel.Warning, "Label storage {Operation} on {Provider} encountered a transient failure; retry {Attempt}.")]
    internal static partial void Retry(ILogger logger, string operation, string provider, int attempt);

    [LoggerMessage(3, LogLevel.Error, "Label storage {Operation} on {Provider} failed in category {Category}.")]
    internal static partial void Failed(ILogger logger, string operation, string provider, string category);

    [LoggerMessage(4, LogLevel.Warning, "Label storage {Operation} on {Provider} rejected a conflicting state or command.")]
    internal static partial void Conflict(ILogger logger, string operation, string provider);

    [LoggerMessage(5, LogLevel.Information, "Label storage lifecycle transitioned to {Phase} at revision {Revision}.")]
    internal static partial void Transition(ILogger logger, LabelerLifecyclePhase phase, long revision);
}
