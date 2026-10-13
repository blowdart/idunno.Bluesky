// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Data;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

using idunno.AtProto;
using idunno.AtProto.Jetstream.Archive;

using Npgsql;

using NpgsqlTypes;

namespace idunno.Bluesky.BotSdk.DataProviders;

/// <summary>Persists bot progress, actions, authentication, and rate accounting in PostgreSQL.</summary>
/// <param name="source">The host-owned connection pool for a dedicated bot database.</param>
/// <param name="options">The bot configuration used for replay binding and durable rate limits.</param>
/// <param name="time">The clock used for application timestamps.</param>
/// <param name="logger">The logger for storage diagnostics.</param>
/// <remarks>
/// <para>The database stores actions before advancing replay progress. An action need not have been
/// sent, but everything needed to send it must already be committed. PostgreSQL transactions cannot
/// include a Bluesky write; the sender reconciles that separate boundary.</para>
/// <para>PostgreSQL's clock and transactional lease checks fence competing processes.
/// The host owns and disposes <paramref name="source"/>; this provider does not dispose it.</para>
/// </remarks>
public sealed class PostgreSqlDatabaseProvider(
    NpgsqlDataSource source,
    BotOptions options,
    TimeProvider time,
    ILogger<PostgreSqlDatabaseProvider> logger) : IDatabaseProvider, IBotDatabaseDiagnostics
{
    private static readonly JsonTypeInfo<SnapshotRequest> s_snapshotRequestTypeInfo =
        BotJson.SnapshotRequest;
    private static readonly JsonTypeInfo<SnapshotCheckpoint> s_snapshotCheckpointTypeInfo =
        BotJson.SnapshotCheckpoint;

    private static readonly Action<ILogger, int, Exception?> s_pruned =
        LoggerMessage.Define<int>(
            LogLevel.Information,
            new EventId(14, "Pruned"),
            "Pruned {Count} expired rate reservations, skip audits, and safely covered terminal actions.");

    /// <summary>Gets the unique identity of this provider's process owner.</summary>
    public Guid Owner { get; } = Guid.NewGuid();

    /// <summary>Determines whether an exception originated from the PostgreSQL client.</summary>
    /// <param name="exception">The exception to classify.</param>
    /// <returns><see langword="true"/> for a PostgreSQL exception; otherwise, <see langword="false"/>.</returns>
    public bool IsDatabaseException(Exception exception) => exception is NpgsqlException;

    /// <summary>Determines whether a PostgreSQL failure permits retry under the recovery policy.</summary>
    /// <param name="exception">The exception to classify.</param>
    /// <returns><see langword="true"/> for a transient PostgreSQL failure; otherwise, <see langword="false"/>.</returns>
    public bool IsTransientDatabaseException(Exception exception) => exception is NpgsqlException { IsTransient: true };

    /// <summary>Initializes PostgreSQL storage without deleting existing state.</summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        NpgsqlConnection connection = await source.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using ConfiguredAsyncDisposable connectionDisposal = connection.ConfigureAwait(false);
        NpgsqlTransaction transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        await using ConfiguredAsyncDisposable transactionDisposal = transaction.ConfigureAwait(false);

        // Rolling deployments may initialize together. A transaction-scoped advisory lock
        // serializes these idempotent schema changes and is released even if initialization fails.
        NpgsqlCommand command = new(
            """
            SELECT pg_advisory_xact_lock(684416309);
            CREATE TABLE IF NOT EXISTS bot_ownership (
                id boolean PRIMARY KEY DEFAULT true CHECK (id),
                owner uuid NOT NULL,
                lease_until timestamptz NOT NULL,
                ready_at timestamptz NOT NULL
            );
            CREATE TABLE IF NOT EXISTS bot_state (
                id boolean PRIMARY KEY DEFAULT true CHECK (id),
                started_at timestamptz NOT NULL,
                service text NOT NULL,
                bot_did text NOT NULL,
                developer_mode boolean NOT NULL,
                request jsonb,
                checkpoint jsonb,
                processed_through bigint NOT NULL DEFAULT 0,
                credentials bytea
            );
            ALTER TABLE bot_state ADD COLUMN IF NOT EXISTS archive_cooldown timestamptz NOT NULL DEFAULT '-infinity';
            CREATE TABLE IF NOT EXISTS reply_work (
                source_uri text NOT NULL,
                action_kind text NOT NULL DEFAULT 'reply' CHECK (length(action_kind) BETWEEN 1 AND 128),
                action_key text NOT NULL DEFAULT 'default' CHECK (length(action_key) BETWEEN 1 AND 128),
                payload_version integer NOT NULL DEFAULT 1 CHECK (payload_version > 0),
                source_sequence bigint NOT NULL,
                source_did text NOT NULL,
                record_key text NOT NULL,
                record jsonb NOT NULL,
                status text NOT NULL DEFAULT 'pending'
                    CHECK (status IN ('pending', 'uncertain', 'sent', 'not-sent', 'simulated')),
                attempts integer NOT NULL DEFAULT 0,
                next_retry timestamptz NOT NULL DEFAULT now(),
                outcome text,
                reply_uri text,
                completed_at timestamptz,
                PRIMARY KEY (source_uri, action_kind, action_key)
            );
            ALTER TABLE reply_work ADD COLUMN IF NOT EXISTS target_deleted boolean NOT NULL DEFAULT false;
            ALTER TABLE reply_work ADD COLUMN IF NOT EXISTS action_kind text NOT NULL DEFAULT 'reply' CHECK (length(action_kind) BETWEEN 1 AND 128);
            ALTER TABLE reply_work ADD COLUMN IF NOT EXISTS action_key text NOT NULL DEFAULT 'default' CHECK (length(action_key) BETWEEN 1 AND 128);
            ALTER TABLE reply_work ADD COLUMN IF NOT EXISTS payload_version integer NOT NULL DEFAULT 1 CHECK (payload_version > 0);
            DO $migration$
            DECLARE legacy_primary_key text;
                    legacy_record_key_index record;
            BEGIN
                SELECT conname INTO legacy_primary_key FROM pg_constraint
                WHERE conrelid = 'reply_work'::regclass AND contype = 'p' AND cardinality(conkey) = 1;
                IF legacy_primary_key IS NOT NULL THEN
                    EXECUTE format('ALTER TABLE reply_work DROP CONSTRAINT %I', legacy_primary_key);
                    ALTER TABLE reply_work ADD PRIMARY KEY (source_uri, action_kind, action_key);
                END IF;
                FOR legacy_record_key_index IN
                    SELECT c.conname FROM pg_constraint c
                    JOIN pg_attribute a ON a.attrelid = c.conrelid AND a.attnum = c.conkey[1]
                    WHERE c.conrelid = 'reply_work'::regclass AND c.contype = 'u'
                        AND cardinality(c.conkey) = 1 AND a.attname = 'record_key'
                LOOP
                    EXECUTE format('ALTER TABLE reply_work DROP CONSTRAINT %I', legacy_record_key_index.conname);
                END LOOP;
                FOR legacy_record_key_index IN
                    SELECT i.indexrelid::regclass AS index_name FROM pg_index i
                    JOIN pg_attribute a ON a.attrelid = i.indrelid AND a.attnum = i.indkey[0]
                    WHERE i.indrelid = 'reply_work'::regclass AND i.indisunique AND NOT i.indisprimary
                        AND i.indnkeyatts = 1 AND a.attname = 'record_key'
                LOOP
                    EXECUTE format('DROP INDEX %s', legacy_record_key_index.index_name);
                END LOOP;
            END;
            $migration$;
            CREATE INDEX IF NOT EXISTS reply_due ON reply_work (next_retry, source_sequence)
                WHERE status IN ('pending', 'uncertain');
            CREATE TABLE IF NOT EXISTS rate_accounting (
                id bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
                requested_at timestamptz NOT NULL,
                points integer NOT NULL,
                session_create boolean NOT NULL
            );
            CREATE INDEX IF NOT EXISTS rate_time ON rate_accounting (requested_at);
            CREATE TABLE IF NOT EXISTS rate_state (
                id boolean PRIMARY KEY DEFAULT true CHECK (id),
                next_request timestamptz NOT NULL DEFAULT '-infinity',
                cooldown_until timestamptz NOT NULL DEFAULT '-infinity'
            );
            INSERT INTO rate_state (id) VALUES (true) ON CONFLICT DO NOTHING;
            CREATE TABLE IF NOT EXISTS skipped_data (
                id bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
                recorded_at timestamptz NOT NULL DEFAULT now(),
                sequence bigint,
                scope text NOT NULL,
                reason text NOT NULL
            );
            """,
            connection,
            transaction);
        await using ConfiguredAsyncDisposable commandDisposal = command.ConfigureAwait(false);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Checks PostgreSQL connectivity without changing durable state.</summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    public async Task CheckConnectionAsync(CancellationToken cancellationToken)
    {
        NpgsqlConnection connection = await source.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using ConfiguredAsyncDisposable connectionDisposal = connection.ConfigureAwait(false);
        NpgsqlCommand command = new("SELECT 1", connection);
        await using ConfiguredAsyncDisposable commandDisposal = command.ConfigureAwait(false);
        _ = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Acquires or renews ownership using database time and a deployment drain period.</summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The time at which this owner may work, or <see langword="null"/> when another process owns the lease.</returns>
    public async Task<DateTimeOffset?> AcquireOrRenewAsync(CancellationToken cancellationToken)
    {
        // PostgreSQL's clock decides lease expiry, so competing processes cannot disagree because
        // their local clocks differ. Renewals preserve ready_at; a new owner gets a drain period
        // longer than the outbound HTTP timeout, allowing the previous deployment's requests to end.
        NpgsqlConnection connection = await source.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using ConfiguredAsyncDisposable connectionDisposal = connection.ConfigureAwait(false);
        NpgsqlCommand command = new(
            """
            INSERT INTO bot_ownership (id, owner, lease_until, ready_at)
            VALUES (true, @owner, now() + interval '90 seconds', now() + interval '40 seconds')
            ON CONFLICT (id) DO UPDATE SET
                owner = EXCLUDED.owner,
                lease_until = EXCLUDED.lease_until,
                ready_at = CASE WHEN bot_ownership.owner = @owner AND bot_ownership.lease_until > now()
                    THEN bot_ownership.ready_at ELSE EXCLUDED.ready_at END
            WHERE bot_ownership.owner = @owner OR bot_ownership.lease_until <= now()
            RETURNING ready_at
            """,
            connection);
        await using ConfiguredAsyncDisposable commandDisposal = command.ConfigureAwait(false);
        command.Parameters.AddWithValue("owner", Owner);

        object? result = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);

        return result is DateTime readyAt ? new DateTimeOffset(readyAt) : null;
    }

    /// <summary>Releases this provider's lease without releasing another owner's lease.</summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    public async Task ReleaseAsync(CancellationToken cancellationToken)
    {
        NpgsqlConnection connection = await source.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using ConfiguredAsyncDisposable connectionDisposal = connection.ConfigureAwait(false);
        NpgsqlCommand command = new(
            "UPDATE bot_ownership SET lease_until = now() WHERE owner = @owner",
            connection);
        await using ConfiguredAsyncDisposable commandDisposal = command.ConfigureAwait(false);
        command.Parameters.AddWithValue("owner", Owner);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Commits a storage operation only while this process holds a usable ownership lease.</summary>
    /// <typeparam name="T">The operation's result type.</typeparam>
    /// <param name="action">The operation using the same connection and transaction as the lease check.</param>
    /// <param name="cancellationToken">The cancellation token for database work and commit.</param>
    /// <returns>The result of the committed operation.</returns>
    /// <exception cref="OwnershipLostException">This process no longer holds a lease with sufficient time remaining.</exception>
    /// <remarks><para>The lease row stays locked until commit. An exception disposes the uncommitted
    /// transaction, rolling back all changes rather than saving partial event effects.</para></remarks>
    private async Task<T> OwnedAsync<T>(
        Func<NpgsqlConnection, NpgsqlTransaction, Task<T>> action,
        CancellationToken cancellationToken)
    {
        // Keep the lease check and mutation in the same transaction. A check performed earlier
        // in application code could become stale before the mutation reaches the database.
        NpgsqlConnection connection = await source.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using ConfiguredAsyncDisposable connectionDisposal = connection.ConfigureAwait(false);
        NpgsqlTransaction transaction = await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken).ConfigureAwait(false);
        await using ConfiguredAsyncDisposable transactionDisposal = transaction.ConfigureAwait(false);
        NpgsqlCommand guard = OwnershipGuard(connection, transaction);
        await using ConfiguredAsyncDisposable guardDisposal = guard.ConfigureAwait(false);

        if (await guard.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false) is not true)
        {
            throw new OwnershipLostException();
        }

        T result = await action(connection, transaction).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);

        return result;
    }

    /// <summary>Builds the lease check that prevents takeover during an owned transaction.</summary>
    /// <param name="connection">The open connection used by the storage operation.</param>
    /// <param name="transaction">The transaction that retains the lease row's shared lock.</param>
    /// <returns>The command returning <see langword="true"/> for a ready lease with at least 35 seconds remaining, or no row.</returns>
    /// <remarks><para>Database time decides readiness and expiry. The reserve leaves room for an
    /// admitted outbound request, but cannot cancel a remote request that has already started.</para></remarks>
    private NpgsqlCommand OwnershipGuard(NpgsqlConnection connection, NpgsqlTransaction transaction)
    {
        // FOR SHARE prevents takeover from changing this lease row during an owned transaction.
        // The 35-second reserve also leaves room for a 30-second HTTP request admitted by the
        // outbound handler. This fences database work; it cannot revoke a request already at a PDS.
        NpgsqlCommand command = new(
            """
            SELECT true FROM bot_ownership
            WHERE owner = @owner AND lease_until > now() + interval '35 seconds' AND ready_at <= now()
            FOR SHARE
            """,
            connection,
            transaction);
        command.Parameters.AddWithValue("owner", Owner);

        return command;
    }

    /// <summary>Loads or initializes durable replay state and validates its account and service identity.</summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The complete saved replay state.</returns>
    /// <exception cref="InvalidOperationException">Stored replay state differs from the configured account or service.</exception>
    public Task<ReplayState> LoadStateAsync(CancellationToken cancellationToken) =>
        OwnedAsync(
            async (connection, transaction) =>
        {
            // The first owner records eligibility before contacting Jetstream. Later starts keep
            // that time, so a deployment or outage does not silently move the start to "now".
            NpgsqlCommand insert = new(
                """
                INSERT INTO bot_state (id, started_at, service, bot_did, developer_mode)
                VALUES (true, @started, @service, @did, @mode) ON CONFLICT DO NOTHING
                """,
                connection,
                transaction);
            await using ConfiguredAsyncDisposable insertDisposal = insert.ConfigureAwait(false);
            insert.Parameters.AddWithValue("started", time.GetUtcNow());
            insert.Parameters.AddWithValue("service", options.JetstreamService);
            insert.Parameters.AddWithValue("did", options.Did);
            insert.Parameters.AddWithValue("mode", options.DeveloperMode);
            await insert.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);

            NpgsqlCommand select = new(
                """
                SELECT started_at, service, bot_did, developer_mode, request::text, checkpoint::text, processed_through
                FROM bot_state WHERE id
                """,
                connection,
                transaction);
            await using ConfiguredAsyncDisposable selectDisposal = select.ConfigureAwait(false);

            NpgsqlDataReader reader = await select.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            await using ConfiguredAsyncDisposable readerDisposal = reader.ConfigureAwait(false);

            if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                throw new InvalidOperationException("Missing singleton bot state.");
            }

            ReplayState state = new(
                await reader.GetFieldValueAsync<DateTimeOffset>(0, cancellationToken).ConfigureAwait(false),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetBoolean(3),
                await reader.IsDBNullAsync(4, cancellationToken).ConfigureAwait(false) ? null : reader.GetString(4),
                await reader.IsDBNullAsync(5, cancellationToken).ConfigureAwait(false) ? null : reader.GetString(5),
                reader.GetInt64(6));

            // Checkpoints belong to a service/request, and simulated work is not real work.
            // Refuse to reinterpret one account's or dry run's history under new settings.
            if (state.Service != options.JetstreamService || state.BotDid != options.Did || state.DeveloperMode != options.DeveloperMode)
            {
                throw new InvalidOperationException("Stored service, bot DID, or developer mode differs. Use a separate database; do not reinterpret existing progress or work.");
            }

            return state;
        },
            cancellationToken);

    /// <summary>Reads a point-in-time operational snapshot without changing durable state.</summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>Durable progress and counts for pending, uncertain, and retained terminal actions.</returns>
    /// <exception cref="InvalidOperationException">The operational snapshot query returned no row.</exception>
    public async Task<BotOperationalSnapshot> GetOperationalSnapshotAsync(CancellationToken cancellationToken)
    {
        NpgsqlConnection connection = await source.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using ConfiguredAsyncDisposable connectionDisposal = connection.ConfigureAwait(false);
        NpgsqlCommand command = new(
            """
            SELECT
                COALESCE((SELECT processed_through FROM bot_state WHERE id), 0),
                count(*) FILTER (WHERE status = 'pending'),
                count(*) FILTER (WHERE status = 'uncertain'),
                count(*) FILTER (WHERE status IN ('pending', 'uncertain') AND next_retry <= now()),
                count(*) FILTER (WHERE status = 'sent'),
                count(*) FILTER (WHERE status = 'not-sent'),
                count(*) FILTER (WHERE status = 'simulated')
            FROM reply_work
            """,
            connection);
        await using ConfiguredAsyncDisposable commandDisposal = command.ConfigureAwait(false);
        NpgsqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        await using ConfiguredAsyncDisposable readerDisposal = reader.ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            throw new InvalidOperationException("Operational snapshot query returned no row.");
        }

        return new(
            reader.GetInt64(0),
            reader.GetInt64(1),
            reader.GetInt64(2),
            reader.GetInt64(3),
            reader.GetInt64(4),
            reader.GetInt64(5),
            reader.GetInt64(6));
    }

    /// <summary>Saves the original replay request only when no request has already been saved.</summary>
    /// <param name="request">The complete original request.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The number of affected rows.</returns>
    public Task<int> SaveRequestAsync(SnapshotRequest request, CancellationToken cancellationToken) =>
        OwnedAsync(
            async (connection, transaction) =>
        {
            if (request.AfterSeq is not > 0)
            {
                throw new InvalidOperationException("Initial replay must have a positive, deliberately chosen sequence boundary.");
            }

            NpgsqlCommand command = new(
                """
                UPDATE bot_state SET request = @request, processed_through = @after WHERE id AND request IS NULL
                """,
                connection,
                transaction);
            await using ConfiguredAsyncDisposable commandDisposal = command.ConfigureAwait(false);
            command.Parameters.Add("request", NpgsqlDbType.Jsonb).Value = JsonSerializer.Serialize(request, s_snapshotRequestTypeInfo);
            command.Parameters.AddWithValue("after", request.AfterSeq.Value);

            return await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        },
            cancellationToken);

    /// <summary>Commits event effects and processed-through progress in one transaction.</summary>
    /// <param name="sequence">The event sequence, not a timestamp.</param>
    /// <param name="actions">The complete actions to enqueue, or an empty list for an event without actions.</param>
    /// <param name="malformedReason">The reason for auditing a malformed post, or <see langword="null"/>.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <param name="deletedUri">The deleted source URI to mark, or <see langword="null"/>.</param>
    /// <returns>The number of newly inserted actions, excluding existing composite identities.</returns>
    /// <exception cref="ArgumentException">An action kind or key is empty or longer than 128 characters, its payload version is not positive, or a new action has a different source sequence.</exception>
    public Task<int> HandleAsync(
        long sequence,
        IReadOnlyList<BotAction> actions,
        string? malformedReason,
        CancellationToken cancellationToken,
        AtUri? deletedUri = null) =>
        OwnedAsync(
            async (connection, transaction) =>
        {
            // Archive generation changes can replay events, even after their completed action rows
            // have been pruned. The monotonic boundary is the long-lived duplicate suppression.
            // Deliberately restoring an older database backup falls outside that guarantee.
            NpgsqlCommand boundary = new("SELECT processed_through FROM bot_state WHERE id FOR UPDATE", connection, transaction);
            await using ConfiguredAsyncDisposable boundaryDisposal = boundary.ConfigureAwait(false);
            long processed = (long)(await boundary.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false)
                ?? throw new InvalidOperationException("Missing progress boundary."));

            if (sequence <= processed)
            {
                return 0;
            }

            int inserted = 0;

            if (deletedUri is not null)
            {
                // Keep uncertain work for reconciliation: the action may already exist remotely.
                // Mark both direct targets and thread roots so the sender does not start new writes.
                NpgsqlCommand deleted = new(
                    """
                    UPDATE reply_work SET target_deleted = true
                    WHERE (source_uri = @uri OR (action_kind = 'reply' AND record #>> '{reply,root,uri}' = @uri))
                        AND status IN ('pending', 'uncertain')
                    """,
                    connection,
                    transaction);
                await using ConfiguredAsyncDisposable deletedDisposal = deleted.ConfigureAwait(false);
                deleted.Parameters.AddWithValue("uri", deletedUri.ToString());
                await deleted.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }

            foreach (BotAction work in actions)
            {
                ValidateAction(work);
                NpgsqlCommand existing = new(
                    """
                    SELECT 1 FROM reply_work
                    WHERE source_uri = @uri AND action_kind = @kind AND action_key = @action
                    """,
                    connection,
                    transaction);
                await using ConfiguredAsyncDisposable existingDisposal = existing.ConfigureAwait(false);
                existing.Parameters.AddWithValue(
                    "uri",
                    work.SourceUri.ToString());
                existing.Parameters.AddWithValue(
                    "kind",
                    work.ActionKind);
                existing.Parameters.AddWithValue(
                    "action",
                    work.ActionKey);
                if (await existing.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false) is not null)
                {
                    continue;
                }

                if (work.SourceSequence != sequence)
                {
#pragma warning disable S3928 // The action list is the public argument validated in this transaction callback.
                    throw new ArgumentException(
                        "A new action must have the event's source sequence.",
                        nameof(actions));
#pragma warning restore S3928
                }

                // Composite identity suppresses replays. Multiple intents may share one fixed
                // remote key, such as list membership requested by several source posts.
                NpgsqlCommand enqueue = new(
                    """
                    INSERT INTO reply_work (source_uri, source_sequence, source_did, record_key, record,
                        action_kind, action_key, payload_version, status, attempts, target_deleted)
                    VALUES (@uri, @seq, @did, @key, @record, @kind, @action, @version, @status, @attempts, @deleted)
                    ON CONFLICT (source_uri, action_kind, action_key) DO NOTHING
                    """,
                    connection,
                    transaction);
                await using ConfiguredAsyncDisposable enqueueDisposal = enqueue.ConfigureAwait(false);
                enqueue.Parameters.AddWithValue("uri", work.SourceUri.ToString());
                enqueue.Parameters.AddWithValue("seq", sequence);
                enqueue.Parameters.AddWithValue("did", work.SourceDid);
                enqueue.Parameters.AddWithValue("key", work.RecordKey);
                enqueue.Parameters.Add("record", NpgsqlDbType.Jsonb).Value = work.RecordJson;
                enqueue.Parameters.AddWithValue(
                    "kind",
                    work.ActionKind);
                enqueue.Parameters.AddWithValue(
                    "action",
                    work.ActionKey);
                enqueue.Parameters.AddWithValue(
                    "version",
                    work.PayloadVersion);
                enqueue.Parameters.AddWithValue(
                    "status",
                    work.Status);
                enqueue.Parameters.AddWithValue(
                    "attempts",
                    work.Attempts);
                enqueue.Parameters.AddWithValue(
                    "deleted",
                    work.TargetDeleted);
                inserted += await enqueue.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }

            if (malformedReason is not null)
            {
                NpgsqlCommand skip = SkipCommand(connection, transaction, sequence, "post", malformedReason);
                await using ConfiguredAsyncDisposable skipDisposal = skip.ConfigureAwait(false);
                await skip.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }

            // Enqueue, deletion flags, skip audits, and progress succeed or roll back together.
            // Advancing first could permanently lose actions if the process crashed before enqueue.
            NpgsqlCommand advance = new("UPDATE bot_state SET processed_through = @seq WHERE id", connection, transaction);
            await using ConfiguredAsyncDisposable advanceDisposal = advance.ConfigureAwait(false);
            advance.Parameters.AddWithValue("seq", sequence);
            await advance.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);

            return inserted;
        },
            cancellationToken);

    /// <summary>Rejects action metadata that cannot be represented by the durable identity contract.</summary>
    /// <param name="work">The action whose kind, identity key, and payload version are checked.</param>
    /// <exception cref="ArgumentException">The kind or key is empty or exceeds 128 characters, or the payload version is not positive.</exception>
    /// <remarks><para>This check does not replace remote payload validation. Existing identities are
    /// checked separately before source-sequence validation so a repeated event retains its original action.</para></remarks>
    private static void ValidateAction(BotAction work)
    {
        if (string.IsNullOrEmpty(work.ActionKind) || work.ActionKind.Length > 128 ||
            string.IsNullOrEmpty(work.ActionKey) || work.ActionKey.Length > 128 ||
            work.PayloadVersion <= 0)
        {
            throw new ArgumentException(
                "Action kinds and keys require 1–128 characters and payload versions must be positive.",
                nameof(work));
        }
    }

    /// <summary>Applies an audited forward-only history skip while retaining queued work and request filters.</summary>
    /// <param name="cutoff">The fixed witnessed-time cutoff requested by the user.</param>
    /// <param name="replacement">The request containing the proposed starting boundary.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The resulting processed-through boundary.</returns>
    public Task<long> OverrideLookbackAsync(DateTimeOffset cutoff, SnapshotRequest replacement, CancellationToken cancellationToken) =>
        OwnedAsync(
            async (connection, transaction) =>
        {
            NpgsqlCommand read = new(
                "SELECT started_at, processed_through, request::text FROM bot_state WHERE id FOR UPDATE",
                connection,
                transaction);
            await using ConfiguredAsyncDisposable readDisposal = read.ConfigureAwait(false);
            DateTimeOffset started;
            long processed;
            SnapshotRequest original;
#pragma warning disable S1199 // Reader scopes must close before subsequent commands in the same transaction.
            {
                NpgsqlDataReader reader = await read.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                await using ConfiguredAsyncDisposable readerDisposal = reader.ConfigureAwait(false);
                if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false) ||
                    await reader.IsDBNullAsync(2, cancellationToken).ConfigureAwait(false))
                {
                    throw new InvalidOperationException("Lookback override requires initialized replay state. Start normally first.");
                }

                started = await reader.GetFieldValueAsync<DateTimeOffset>(0, cancellationToken).ConfigureAwait(false);
                processed = reader.GetInt64(1);
                original = JsonSerializer.Deserialize(reader.GetString(2), s_snapshotRequestTypeInfo)
                    ?? throw new InvalidDataException("Stored replay request is null.");
            }
#pragma warning restore S1199

            if (cutoff <= started || replacement.AfterSeq is not > 0)
            {
                throw new InvalidOperationException("Forward-only lookback must move the eligibility time forward and have a positive archive boundary.");
            }

            // The CLI override is an explicit history skip, never a rewind. Rewinding would need
            // remote reconciliation for completed work whose fixed keys may have been pruned.
            long after = Math.Max(processed, replacement.AfterSeq.Value);

            if (original.BeforeSeq is long upper && after >= upper)
            {
                throw new InvalidOperationException("Lookback override would exceed the original upper sequence bound.");
            }

            // Only the lower boundary changes. Preserve the original filters and upper bound,
            // discard the now-incompatible checkpoint, and retain existing queued replies.
            SnapshotRequest request = original with { AfterSeq = after };
            NpgsqlCommand update = new(
                """
                UPDATE bot_state SET started_at = @cutoff, request = @request, checkpoint = NULL,
                    processed_through = @after WHERE id;
                INSERT INTO skipped_data (sequence, scope, reason)
                    VALUES (@after, 'lookback override', @reason)
                """,
                connection,
                transaction);
            await using ConfiguredAsyncDisposable updateDisposal = update.ConfigureAwait(false);
            update.Parameters.AddWithValue("cutoff", cutoff);
            update.Parameters.AddWithValue("after", after);
            update.Parameters.Add("request", NpgsqlDbType.Jsonb).Value = JsonSerializer.Serialize(request, s_snapshotRequestTypeInfo);
            update.Parameters.AddWithValue("reason", $"User-directed forward-only override to {cutoff:O}; queued work retained.");
            await update.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);

            return after;
        },
            cancellationToken);

    // Replay's checkpoint/error callbacks are synchronous. These bounded SQL calls must finish before returning to the SDK.
    /// <summary>Saves the complete replay checkpoint after preceding events have been durably handled.</summary>
    /// <param name="checkpoint">The complete SDK checkpoint.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <param name="consumedThrough">The consumed boundary to coalesce with durable progress, or <see langword="null"/>.</param>
    /// <exception cref="ArgumentNullException"><paramref name="checkpoint"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">The saved live checkpoint is ahead of durable event handling.</exception>
    /// <exception cref="OwnershipLostException">The current process no longer owns the database lease.</exception>
    public void SaveCheckpoint(SnapshotCheckpoint checkpoint, CancellationToken cancellationToken, long? consumedThrough = null)
    {
        ArgumentNullException.ThrowIfNull(checkpoint);
        using NpgsqlConnection connection = source.OpenConnection();
        using NpgsqlTransaction transaction = connection.BeginTransaction();
        using NpgsqlCommand guard = OwnershipGuard(connection, transaction);

        if (guard.ExecuteScalar() is not true)
        {
            throw new OwnershipLostException();
        }

        cancellationToken.ThrowIfCancellationRequested();

        if (consumedThrough is long consumed)
        {
            // Ignored events have no side effects to enqueue. Coalesce their progress with this
            // checkpoint, rather than paying for a transaction for every firehose event.
            using NpgsqlCommand progress = new(
                "UPDATE bot_state SET processed_through = greatest(processed_through, @consumed) WHERE id",
                connection,
                transaction);
            progress.Parameters.AddWithValue("consumed", consumed);
            progress.ExecuteNonQuery();
        }

        // Store the whole SDK checkpoint, including archive generation and fallback fields.
        // A live cursor ahead of handled events would skip work after a crash, so reject it.
        using NpgsqlCommand command = new(
            """
            UPDATE bot_state SET checkpoint = @checkpoint
            WHERE id AND (@live IS NULL OR processed_through >= @live)
            """,
            connection,
            transaction);
        ArgumentNullException.ThrowIfNull(checkpoint);
        command.Parameters.Add("checkpoint", NpgsqlDbType.Jsonb).Value = JsonSerializer.Serialize(checkpoint, s_snapshotCheckpointTypeInfo);
        command.Parameters.Add("live", NpgsqlDbType.Bigint).Value = checkpoint.LiveAfterSeq is long live ? live : DBNull.Value;

        if (command.ExecuteNonQuery() != 1)
        {
            throw new InvalidOperationException("Missing state or live checkpoint ahead of durable handling.");
        }

        transaction.Commit();
    }

    /// <summary>Audits an explicitly permitted archive skip as potential data loss.</summary>
    /// <param name="sequence">The skipped sequence, or <see langword="null"/> for a block.</param>
    /// <param name="scope">The scope of skipped data.</param>
    /// <param name="reason">The reason for skipping.</param>
    /// <exception cref="OwnershipLostException">The current process no longer owns the database lease.</exception>
    public void RecordSkip(long? sequence, string scope, string reason)
    {
        // The configured malformed-data policy permits data loss. Commit its audit before the
        // synchronous SDK error callback returns permission to skip a record or entire block.
        using NpgsqlConnection connection = source.OpenConnection();
        using NpgsqlTransaction transaction = connection.BeginTransaction();
        using NpgsqlCommand guard = OwnershipGuard(connection, transaction);

        if (guard.ExecuteScalar() is not true)
        {
            throw new OwnershipLostException();
        }

        using NpgsqlCommand command = SkipCommand(connection, transaction, sequence, scope, reason);
        command.ExecuteNonQuery();
        transaction.Commit();
    }

    /// <summary>Builds an audit insert for an explicitly accepted loss of replay data.</summary>
    /// <param name="connection">The open connection used for the audit.</param>
    /// <param name="transaction">The transaction that commits the audit with its related progress changes.</param>
    /// <param name="sequence">The affected event sequence, or <see langword="null"/> for a block-level skip.</param>
    /// <param name="scope">The description of the data being skipped.</param>
    /// <param name="reason">The diagnostic explanation for accepting the skip.</param>
    /// <returns>The parameterized command; the caller executes and disposes it.</returns>
    private static NpgsqlCommand SkipCommand(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        long? sequence,
        string scope,
        string reason)
    {
        NpgsqlCommand command = new("INSERT INTO skipped_data (sequence, scope, reason) VALUES (@seq, @scope, @reason)", connection, transaction);
        command.Parameters.Add("seq", NpgsqlDbType.Bigint).Value = sequence is null ? DBNull.Value : sequence.Value;
        command.Parameters.AddWithValue("scope", scope);
        command.Parameters.AddWithValue("reason", reason);

        return command;
    }

    /// <summary>Loads the next pending or uncertain action whose retry time has arrived.</summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The eligible work, or <see langword="null"/>.</returns>
    public Task<BotAction?> NextWorkAsync(CancellationToken cancellationToken) =>
        OwnedAsync<BotAction?>(
            async (connection, transaction) =>
        {
            // A single sender is protected by the lease, so this is not a multi-consumer claim.
            // Scheduled retries are skipped until due, allowing other eligible work to continue.
            NpgsqlCommand command = new(
                """
                SELECT source_uri, source_sequence, source_did, record_key, record::text, status, attempts,
                    target_deleted, action_kind, action_key, payload_version
                FROM reply_work WHERE status IN ('pending', 'uncertain') AND next_retry <= now()
                ORDER BY next_retry, source_sequence LIMIT 1
                """,
                connection,
                transaction);
            await using ConfiguredAsyncDisposable commandDisposal = command.ConfigureAwait(false);

            NpgsqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            await using ConfiguredAsyncDisposable readerDisposal = reader.ConfigureAwait(false);

            if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                return null;
            }

            return new(
                reader.GetString(0),
                reader.GetInt64(1),
                reader.GetString(2),
                reader.GetString(3),
                reader.GetString(4),
                reader.GetString(5),
                reader.GetInt32(6),
                reader.GetBoolean(7),
                reader.GetString(8),
                reader.GetString(9),
                reader.GetInt32(10));
        },
            cancellationToken);

    /// <summary>Marks one composite action identity uncertain and increments its attempt count before a remote write.</summary>
    /// <param name="work">The work being attempted.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The number of affected rows.</returns>
    public Task<int> MarkAttemptAsync(
        BotAction work,
        CancellationToken cancellationToken) =>
        OwnedAsync(
            async (connection, transaction) =>
        {
            // Mark uncertain BEFORE the remote write. A crash on either side of that call must
            // lead to reconciliation at the saved identity, not an assumption that nothing was posted.
            NpgsqlCommand command = new(
                """
                UPDATE reply_work SET status = 'uncertain', attempts = attempts + 1
                WHERE source_uri = @uri AND action_kind = @kind AND action_key = @action
                    AND status IN ('pending', 'uncertain') AND NOT target_deleted
                """,
                connection,
                transaction);
            await using ConfiguredAsyncDisposable commandDisposal = command.ConfigureAwait(false);
            command.Parameters.AddWithValue("uri", work.SourceUri.ToString());
            command.Parameters.AddWithValue(
                "kind",
                work.ActionKind);
            command.Parameters.AddWithValue(
                "action",
                work.ActionKey);

            int count = await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);

            return count;
        },
            cancellationToken);

    /// <summary>Saves a terminal action outcome for one composite identity.</summary>
    /// <param name="work">The completed work.</param>
    /// <param name="status">The terminal delivery status.</param>
    /// <param name="remoteReference">The remote action URI, or <see langword="null"/> when not sent.</param>
    /// <param name="reason">The outcome reason.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The number of affected rows.</returns>
    public Task<int> FinishAsync(
        BotAction work,
        string status,
        string? remoteReference,
        string reason,
        CancellationToken cancellationToken) =>
        OwnedAsync(
            async (connection, transaction) =>
        {
            NpgsqlCommand command = new(
                """
                UPDATE reply_work SET status = @status, completed_at = now(), reply_uri = @reply, outcome = @reason
                WHERE source_uri = @uri AND action_kind = @kind AND action_key = @action
                    AND status IN ('pending', 'uncertain')
                """,
                connection,
                transaction);
            await using ConfiguredAsyncDisposable commandDisposal = command.ConfigureAwait(false);
            command.Parameters.AddWithValue("uri", work.SourceUri.ToString());
            command.Parameters.AddWithValue(
                "kind",
                work.ActionKind);
            command.Parameters.AddWithValue(
                "action",
                work.ActionKey);
            command.Parameters.AddWithValue("status", status);
            command.Parameters.Add("reply", NpgsqlDbType.Text).Value = remoteReference is null ? DBNull.Value : remoteReference;
            command.Parameters.AddWithValue("reason", reason);

            return await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        },
            cancellationToken);

    /// <summary>Schedules another reconciliation or send attempt without changing its fixed record.</summary>
    /// <param name="work">The work to retry.</param>
    /// <param name="retryAt">The earliest next attempt time.</param>
    /// <param name="reason">The retry reason.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The number of affected rows.</returns>
    public Task<int> RetryAsync(
        BotAction work,
        DateTimeOffset retryAt,
        string reason,
        CancellationToken cancellationToken) =>
        OwnedAsync(
            async (connection, transaction) =>
        {
            NpgsqlCommand command = new(
                """
                UPDATE reply_work SET next_retry = @retry, outcome = @reason
                WHERE source_uri = @uri AND action_kind = @kind AND action_key = @action
                    AND status IN ('pending', 'uncertain')
                """,
                connection,
                transaction);
            await using ConfiguredAsyncDisposable commandDisposal = command.ConfigureAwait(false);
            command.Parameters.AddWithValue("uri", work.SourceUri.ToString());
            command.Parameters.AddWithValue(
                "kind",
                work.ActionKind);
            command.Parameters.AddWithValue(
                "action",
                work.ActionKey);
            command.Parameters.AddWithValue("retry", retryAt);
            command.Parameters.AddWithValue("reason", reason);

            return await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        },
            cancellationToken);

    /// <summary>Loads the encrypted refresh-credential envelope.</summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The encrypted envelope, or <see langword="null"/>.</returns>
    public Task<byte[]?> LoadCredentialsAsync(CancellationToken cancellationToken) =>
        OwnedAsync(
            async (connection, transaction) =>
        {
            NpgsqlCommand command = new("SELECT credentials FROM bot_state WHERE id", connection, transaction);
            await using ConfiguredAsyncDisposable commandDisposal = command.ConfigureAwait(false);

            return await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false) as byte[];
        },
            cancellationToken);

    /// <summary>Saves or clears the encrypted refresh-credential envelope.</summary>
    /// <param name="encrypted">The encrypted envelope, or <see langword="null"/> to clear it.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The number of affected rows.</returns>
    public Task<int> SaveCredentialsAsync(byte[]? encrypted, CancellationToken cancellationToken) =>
        OwnedAsync(
            async (connection, transaction) =>
        {
            NpgsqlCommand command = new("UPDATE bot_state SET credentials = @value WHERE id", connection, transaction);
            await using ConfiguredAsyncDisposable commandDisposal = command.ConfigureAwait(false);
            command.Parameters.Add("value", NpgsqlDbType.Bytea).Value = encrypted is null ? DBNull.Value : encrypted;

            return await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        },
            cancellationToken);

    /// <summary>Reserves durable outbound budget before a request or reports when budget becomes available.</summary>
    /// <param name="points">The repository write-point cost.</param>
    /// <param name="session"><see langword="true"/> for session creation; otherwise, <see langword="false"/>.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The required wait deadline, or <see langword="null"/> when the request was reserved.</returns>
    public Task<DateTimeOffset?> ReserveRequestAsync(int points, bool session, CancellationToken cancellationToken) =>
        OwnedAsync<DateTimeOffset?>(
            async (connection, transaction) =>
        {
            // Serialize reservations and pacing through the singleton rate row. Accounting is
            // durable and based on attempted requests, not successful replies, so restarts and
            // failed requests cannot reset budgets. Database time defines all rolling windows.
            NpgsqlCommand state = new("SELECT now(), next_request, cooldown_until FROM rate_state WHERE id FOR UPDATE", connection, transaction);
            await using ConfiguredAsyncDisposable stateDisposal = state.ConfigureAwait(false);
            DateTimeOffset now;
            DateTimeOffset waitUntil;
#pragma warning disable S1199 // Reader scopes must close before subsequent commands in the same transaction.
            {
                NpgsqlDataReader reader = await state.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                await using ConfiguredAsyncDisposable readerDisposal = reader.ConfigureAwait(false);
                if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    throw new InvalidOperationException("Missing rate state.");
                }

                now = await reader.GetFieldValueAsync<DateTimeOffset>(0, cancellationToken).ConfigureAwait(false);
                waitUntil = Max(
                    await reader.GetFieldValueAsync<DateTimeOffset>(1, cancellationToken).ConfigureAwait(false),
                    await reader.GetFieldValueAsync<DateTimeOffset>(2, cancellationToken).ConfigureAwait(false));
            }

            NpgsqlCommand budgets = new(
                """
                SELECT
                    count(*) FILTER (WHERE requested_at > now() - interval '5 minutes'),
                    coalesce(sum(points) FILTER (WHERE requested_at > now() - interval '1 hour'), 0),
                    coalesce(sum(points), 0),
                    count(*) FILTER (WHERE session_create AND requested_at > now() - interval '5 minutes'),
                    count(*) FILTER (WHERE session_create),
                    min(requested_at) FILTER (WHERE requested_at > now() - interval '5 minutes'),
                    min(requested_at) FILTER (WHERE points > 0 AND requested_at > now() - interval '1 hour'),
                    min(requested_at) FILTER (WHERE points > 0),
                    min(requested_at) FILTER (WHERE session_create AND requested_at > now() - interval '5 minutes'),
                    min(requested_at) FILTER (WHERE session_create)
                FROM rate_accounting WHERE requested_at > now() - interval '1 day'
                """,
                connection,
                transaction);
            await using ConfiguredAsyncDisposable budgetsDisposal = budgets.ConfigureAwait(false);
            {
                NpgsqlDataReader reader = await budgets.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                await using ConfiguredAsyncDisposable readerDisposal = reader.ConfigureAwait(false);
                await reader.ReadAsync(cancellationToken).ConfigureAwait(false);

                int[] thresholds = [options.RequestsPerFiveMinutes, options.HourlyWritePoints,
                    options.DailyWritePoints, options.SessionsPerFiveMinutes, options.SessionsPerDay];

                int[] additions = [1, points, points, session ? 1 : 0, session ? 1 : 0];
                TimeSpan[] windows = [TimeSpan.FromMinutes(5), TimeSpan.FromHours(1), TimeSpan.FromDays(1),
                    TimeSpan.FromMinutes(5), TimeSpan.FromDays(1)];

                // These arrays follow the query's column order: requests, hourly/daily write
                // points, and five-minute/daily session creates. Wait for the earliest relevant
                // reservation to expire, then recalculate; one expiry may not free enough points.
                for (int index = 0; index < thresholds.Length; index++)
                {
                    if (additions[index] > 0 && reader.GetInt64(index) + additions[index] > thresholds[index])
                    {
                        if (await reader.IsDBNullAsync(index + 5, cancellationToken).ConfigureAwait(false))
                        {
                            throw new InvalidOperationException("A request exceeds its entire configured rate budget.");
                        }

                        DateTimeOffset firstRequest = await reader.GetFieldValueAsync<DateTimeOffset>(index + 5, cancellationToken).ConfigureAwait(false);
                        waitUntil = Max(waitUntil, firstRequest + windows[index] + TimeSpan.FromSeconds(1));
                    }
                }
            }
#pragma warning restore S1199

            if (waitUntil > now)
            {
                return waitUntil;
            }

            NpgsqlCommand reserve = new(
                """
                INSERT INTO rate_accounting (requested_at, points, session_create) VALUES (now(), @points, @session);
                UPDATE rate_state SET next_request = now() + make_interval(secs => @pace) WHERE id;
                """,
                connection,
                transaction);
            await using ConfiguredAsyncDisposable reserveDisposal = reserve.ConfigureAwait(false);
            reserve.Parameters.AddWithValue("points", points);
            reserve.Parameters.AddWithValue("session", session);
            reserve.Parameters.AddWithValue("pace", (double)options.MinimumRequestIntervalSeconds);
            await reserve.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);

            return null;
        },
            cancellationToken);

    /// <summary>Extends the durable outbound cooldown without shortening an existing cooldown.</summary>
    /// <param name="until">The cooldown deadline.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The number of affected rows.</returns>
    public Task<int> CooldownAsync(DateTimeOffset until, CancellationToken cancellationToken) =>
        OwnedAsync(
            async (connection, transaction) =>
        {
            NpgsqlCommand command = new(
                """
                UPDATE rate_state SET cooldown_until = greatest(cooldown_until, @until) WHERE id
                """,
                connection,
                transaction);
            await using ConfiguredAsyncDisposable commandDisposal = command.ConfigureAwait(false);
            command.Parameters.AddWithValue("until", until);

            return await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        },
            cancellationToken);

    /// <summary>Loads the durable archive cooldown deadline.</summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The saved cooldown deadline.</returns>
    public Task<DateTimeOffset> ArchiveCooldownAsync(CancellationToken cancellationToken) =>
        OwnedAsync(
            async (connection, transaction) =>
        {
            NpgsqlCommand command = new("SELECT archive_cooldown FROM bot_state WHERE id", connection, transaction);
            await using ConfiguredAsyncDisposable commandDisposal = command.ConfigureAwait(false);

            return new DateTimeOffset((DateTime)(await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false)
                ?? throw new InvalidOperationException("Missing archive cooldown state.")));
        },
            cancellationToken);

    /// <summary>Extends the durable archive cooldown without shortening an existing cooldown.</summary>
    /// <param name="until">The cooldown deadline.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The number of affected rows.</returns>
    public Task<int> SaveArchiveCooldownAsync(DateTimeOffset until, CancellationToken cancellationToken) =>
        OwnedAsync(
            async (connection, transaction) =>
        {
            NpgsqlCommand command = new(
                "UPDATE bot_state SET archive_cooldown = greatest(archive_cooldown, @until) WHERE id",
                connection,
                transaction);
            await using ConfiguredAsyncDisposable commandDisposal = command.ConfigureAwait(false);
            command.Parameters.AddWithValue("until", until);

            return await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        },
            cancellationToken);

    /// <summary>Prunes terminal work only when retention and processed-through coverage permit it.</summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The number of affected rows.</returns>
    public Task<int> PruneAsync(CancellationToken cancellationToken) =>
        OwnedAsync(
            async (connection, transaction) =>
        {
            // Pending and uncertain replies have no expiry. Completed rows can go only when
            // the durable boundary covers their source, so ordinary replay cannot recreate them.
            // Keep reservations for the full longest rate window, independently of audit retention.
            NpgsqlCommand command = new(
                """
                DELETE FROM rate_accounting WHERE requested_at < now() - interval '1 day';
                DELETE FROM skipped_data WHERE recorded_at < now() - make_interval(days => @days);
                DELETE FROM reply_work WHERE status IN ('sent', 'not-sent', 'simulated')
                    AND completed_at < now() - make_interval(days => @days)
                    AND source_sequence <= (SELECT processed_through FROM bot_state WHERE id)
                """,
                connection,
                transaction);
            await using ConfiguredAsyncDisposable commandDisposal = command.ConfigureAwait(false);
            command.Parameters.AddWithValue("days", options.CompletedRetentionDays);

            int count = await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            s_pruned(logger, count, null);

            return count;
        },
            cancellationToken);

    /// <summary>Selects the later deadline so extending a cooldown never shortens it.</summary>
    /// <param name="first">The first deadline to compare.</param>
    /// <param name="second">The second deadline to compare.</param>
    /// <returns>The later deadline.</returns>
    private static DateTimeOffset Max(DateTimeOffset first, DateTimeOffset second) => first > second ? first : second;
}
