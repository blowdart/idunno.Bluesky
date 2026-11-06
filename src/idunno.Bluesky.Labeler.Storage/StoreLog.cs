// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using idunno.Bluesky.Labeler.Maintenance;

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

    /// <summary>Records bounded policy observation diagnostics without declaration contents or subject data.</summary>
    [LoggerMessage(6, LogLevel.Warning, "Policy observation rejected: {Reason}; expected revision {ExpectedRevision}, current revision {CurrentRevision}, time difference {DifferenceMilliseconds} ms.")]
    internal static partial void PolicyObservationRejected(
        ILogger logger,
        string reason,
        long expectedRevision,
        long? currentRevision,
        long differenceMilliseconds);

    /// <summary>Records a tolerated database-clock rollback without including declaration data.</summary>
    [LoggerMessage(7, LogLevel.Warning, "Policy observation tolerated a database-clock rollback of {DifferenceMilliseconds} ms.")]
    internal static partial void PolicyClockRollbackTolerated(ILogger logger, long differenceMilliseconds);
}
