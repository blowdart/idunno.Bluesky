// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Data;
using System.Data.Common;
using System.Globalization;
using System.Runtime.CompilerServices;

using idunno.Bluesky.Labeler.Storage;

using Microsoft.Extensions.Logging;

using Npgsql;

#pragma warning disable IDE0130 // Providers share the labeler namespace across separate packages.
namespace idunno.Bluesky.Labeler;
#pragma warning restore IDE0130

/// <summary>Applies PostgreSQL durability checks and row locking to shared decision storage.</summary>
/// <remarks><para>Every operation locks the issuer's stream row. Setup additionally serializes DDL
/// with an advisory lock; upgrades preserve the existing signed event log.</para></remarks>
internal sealed class PostgreSqlLabelStore(string connectionString, ILogger<PostgreSqlLabelStore> logger, StoreMetrics metrics)
    : RelationalLabelStore(logger, metrics)
{
    protected override string Provider => "postgresql";
    protected override string LockSuffix => " FOR UPDATE";
    protected override string ClockSql => "SELECT CAST(FLOOR(EXTRACT(EPOCH FROM clock_timestamp()) * 1000) AS BIGINT)";
    /// <summary>Creates an operation-owned PostgreSQL connection without retaining session state between calls.</summary>
    protected override DbConnection CreateConnection() => new NpgsqlConnection(connectionString);
    /// <summary>Recognizes serialization, deadlock and lock-timeout failures eligible for precommit retry.</summary>
    protected override bool IsTransient(DbException exception) =>
        exception is PostgresException { SqlState: "40001" or "40P01" or "55P03" };
    /// <summary>Begins a read-committed transaction whose stream-row lock serializes subsequent work.</summary>
    protected override async Task<DbTransaction> BeginAsync(DbConnection connection, CancellationToken token) =>
        await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted, token).ConfigureAwait(false);

    /// <summary>Rejects unsupported durability settings and installs bounded session-level lock and statement waits.</summary>
    protected override async Task PrepareAsync(DbConnection connection, CancellationToken token)
    {
        long version = Convert.ToInt64(
            await ScalarAsync(
                connection: connection,
                transaction: null,
                sql: "SHOW server_version_num",
                token: token).ConfigureAwait(false),
            CultureInfo.InvariantCulture);
        object? durable = await ScalarAsync(
            connection: connection,
            transaction: null,
            sql: "SHOW fsync",
            token: token).ConfigureAwait(false);
        object? fullPages = await ScalarAsync(
            connection: connection,
            transaction: null,
            sql: "SHOW full_page_writes",
            token: token).ConfigureAwait(false);
        if (version < 170000 || !string.Equals(durable?.ToString(), "on", StringComparison.Ordinal) ||
            !string.Equals(fullPages?.ToString(), "on", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("PostgreSQL 17 or later with fsync and full_page_writes enabled is required.");
        }

        await ExecuteAsync(
            connection: connection,
            transaction: null,
            sql: "SET synchronous_commit=on; SET lock_timeout='15s'; SET statement_timeout='15s'",
            token: token).ConfigureAwait(false);
    }

    /// <summary>Serializes schema setup with an advisory transaction lock and commits DDL as one unit.</summary>
    protected override async Task MigrateAsync(DbConnection connection, CancellationToken token)
    {
        DbTransaction transaction = await BeginAsync(connection, token).ConfigureAwait(false);
        await using ConfiguredAsyncDisposable transactionDisposal = transaction.ConfigureAwait(false);
        await ExecuteAsync(
            connection: connection,
            transaction: transaction,
            sql: "SELECT pg_advisory_xact_lock(673009001)",
            token: token).ConfigureAwait(false);
        await ApplySchemaAsync(connection, transaction, token).ConfigureAwait(false);
        await transaction.CommitAsync(token).ConfigureAwait(false);
    }
}
