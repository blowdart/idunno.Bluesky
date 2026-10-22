// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Data.Common;
using System.Runtime.CompilerServices;

using idunno.Bluesky.Labeler.Storage;

using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;

namespace idunno.Bluesky.Labeler;

/// <summary>Applies SQLite durability settings and immediate writer transactions to shared decision storage.</summary>
/// <remarks><para>The immediate transaction is the cross-process stream lock. Schema setup and additive
/// upgrades commit transactionally; ordinary operations never run DDL.</para></remarks>
internal sealed class SqliteLabelStore(string connectionString, ILogger<SqliteLabelStore> logger, StoreMetrics metrics)
    : RelationalLabelStore(logger, metrics)
{
    protected override string Provider => "sqlite";
    protected override string LockSuffix => string.Empty;
    protected override string ClockSql => "SELECT CAST(unixepoch('subsec') * 1000 AS INTEGER)";
    /// <summary>Creates an operation-owned connection to the configured local database.</summary>
    protected override DbConnection CreateConnection() => new SqliteConnection(connectionString);
    /// <summary>Recognizes SQLite busy and locked responses as precommit contention, not durable outcomes.</summary>
    protected override bool IsTransient(DbException exception) => exception is SqliteException { SqliteErrorCode: 5 or 6 };

    /// <summary>Acquires the writer reservation immediately so decision reads and writes share one serialization point.</summary>
    protected override Task<DbTransaction> BeginAsync(DbConnection connection, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
#pragma warning disable CA1849 // An immediate SQLite writer transaction requires this synchronous overload.
        return Task.FromResult<DbTransaction>(((SqliteConnection)connection).BeginTransaction(deferred: false));
#pragma warning restore CA1849
    }

    /// <summary>Requires WAL and full synchronization so acknowledged commits retain the promised durability.</summary>
    protected override async Task PrepareAsync(DbConnection connection, CancellationToken token)
    {
        object? mode = await ScalarAsync(
            connection: connection,
            transaction: null,
            sql: "PRAGMA journal_mode=WAL",
            token: token).ConfigureAwait(false);
        if (!string.Equals(mode?.ToString(), "wal", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("SQLite label storage requires WAL mode.");
        }

        await ExecuteAsync(
            connection: connection,
            transaction: null,
            sql: "PRAGMA synchronous=FULL; PRAGMA foreign_keys=ON",
            token: token).ConfigureAwait(false);
    }

    /// <summary>Commits schema changes together while holding the same immediate writer lock used for data operations.</summary>
    protected override async Task MigrateAsync(DbConnection connection, CancellationToken token)
    {
        DbTransaction transaction = await BeginAsync(connection, token).ConfigureAwait(false);
        await using ConfiguredAsyncDisposable transactionDisposal = transaction.ConfigureAwait(false);
        await ApplySchemaAsync(connection, transaction, token).ConfigureAwait(false);
        await transaction.CommitAsync(token).ConfigureAwait(false);
    }
}
