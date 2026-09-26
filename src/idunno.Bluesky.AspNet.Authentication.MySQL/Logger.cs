// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using idunno.AtProto;

using Microsoft.Extensions.Logging;

namespace idunno.Bluesky.AspNet.Authentication.MySQL;

/// <summary>
/// Logging for <see cref="MySqlIdentityStore"/>.
/// </summary>
/// <remarks>
/// <para>
///   The event IDs match the equivalent messages in idunno.Bluesky.AspNet.Authentication, so that an operator filtering on a
///   refresh lock event sees the same ID whichever identity store an application is configured with.
/// </para>
/// </remarks>
internal static partial class Logger
{
    [LoggerMessage(261, LogLevel.Debug, "StartRefresh denied for {did}, refresh already in progress.")]
    public static partial void StartRefreshDenied(this ILogger logger, Did did);

    [LoggerMessage(262, LogLevel.Debug, "EndRefresh finished for {did}")]
    public static partial void EndRefreshFinished(this ILogger logger, Did did);

    [LoggerMessage(264, LogLevel.Warning, "EndRefresh for {did} did not release the refresh lock as it is now held by another caller.")]
    public static partial void EndRefreshLockNotOwned(this ILogger logger, Did did);

    [LoggerMessage(270, LogLevel.Debug, "StartRefresh entered for {did}")]
    public static partial void StartRefreshEntered(this ILogger logger, Did did);

    [LoggerMessage(273, LogLevel.Debug, "Swept {rowsDeleted} expired rows from {table}.")]
    public static partial void ExpiredEntriesSwept(this ILogger logger, int rowsDeleted, string table);

    [LoggerMessage(274, LogLevel.Warning, "Sweeping expired rows from {table} failed. The operation which triggered the sweep was unaffected, but expired rows remain in the table.")]
    public static partial void ExpiredEntrySweepFailed(this ILogger logger, string table, Exception exception);
}
