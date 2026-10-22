// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Data.Common;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;

using idunno.AtProto;
using idunno.AtProto.Labels;

using Microsoft.Extensions.Logging;

namespace idunno.Bluesky.Labeler.Storage;

/// <summary>Serializes durable label mutations and decision receipts under one provider-owned stream lock.</summary>
/// <remarks><para>Linked into each provider assembly. SQL, client types and transaction fault hooks stay
/// internal; application services submit typed commands rather than holding database connections.</para></remarks>
internal abstract class RelationalLabelStore(ILogger logger, StoreMetrics metrics) : ILabelStore
{
    private const int SchemaVersion = 2;
    private static readonly UTF8Encoding s_strictUtf8 = new(false, true);
    /// <summary>Gets or sets the test hook for failures known to occur before commit submission.</summary>
    internal Func<DbConnection, DbTransaction, CancellationToken, Task>? BeforeCommitAsync { get; set; }
    /// <summary>Gets or sets the test hook for a lost acknowledgement after the database accepted a commit.</summary>
    internal Func<DbConnection, CancellationToken, Task>? AfterCommitAsync { get; set; }
    /// <summary>Gets the stable provider tag used in safe logs and metrics.</summary>
    protected abstract string Provider { get; }
    /// <summary>Gets the row-lock clause, or empty text when the transaction itself holds the SQLite writer lock.</summary>
    protected abstract string LockSuffix { get; }
    /// <summary>Gets the provider expression for database UTC time in Unix milliseconds.</summary>
    protected abstract string ClockSql { get; }
    /// <summary>Creates an unopened provider connection owned by one storage operation.</summary>
    protected abstract DbConnection CreateConnection();
    /// <summary>Begins the transaction used to serialize access to the stream state.</summary>
    protected abstract Task<DbTransaction> BeginAsync(DbConnection connection, CancellationToken token);
    /// <summary>Checks durability prerequisites and configures the session before beginning an operation.</summary>
    protected abstract Task PrepareAsync(DbConnection connection, CancellationToken token);
    /// <summary>Installs or resumes schema setup using the provider's DDL locking and transaction rules.</summary>
    protected abstract Task MigrateAsync(DbConnection connection, CancellationToken token);
    /// <summary>Identifies contention failures eligible for a bounded retry before commit submission.</summary>
    protected abstract bool IsTransient(DbException exception);

    /// <summary>Applies repeatable additive schema steps and records their completion without rebuilding signed history.</summary>
    protected static async Task ApplySchemaAsync(DbConnection connection, DbTransaction? transaction, CancellationToken token)
    {
        using Stream stream = typeof(RelationalLabelStore).Assembly.GetManifestResourceStream("LabelSchema")
            ?? throw new InvalidOperationException("The packaged label schema is missing.");
        using StreamReader reader = new(stream);
        string schema = await reader.ReadToEndAsync(token).ConfigureAwait(false);
        int firstStatement = schema.IndexOf(';', StringComparison.Ordinal);
        await ExecuteAsync(
            connection: connection,
            transaction: transaction,
            sql: schema[..(firstStatement + 1)],
            token: token).ConfigureAwait(false);
        object? version = await ScalarAsync(
            connection: connection,
            transaction: transaction,
            sql: "SELECT version FROM label_schema WHERE id=1",
            token: token).ConfigureAwait(false);
        if (version is not null && Integer(version) is not (1 or SchemaVersion))
        {
            throw new InvalidDataException("The label database schema version is not supported.");
        }

        await ExecuteAsync(
            connection: connection,
            transaction: transaction,
            sql: schema,
            token: token).ConfigureAwait(false);
        // Version one has no service receipts. The additive DDL above can safely resume on MySQL;
        // mark completion only after every table/index exists, without rebuilding signed history.
        await ExecuteAsync(
            connection: connection,
            transaction: transaction,
            sql: "UPDATE label_schema SET version=2 WHERE id=1",
            token: token).ConfigureAwait(false);
    }

    /// <summary>Creates a transaction-bound command, binding caller values as parameters rather than SQL text.</summary>
    protected static DbCommand Command(
        DbConnection connection,
        DbTransaction? transaction,
        string sql,
        params (string Name, object? Value)[] values)
    {
        DbCommand command = connection.CreateCommand();
        command.Transaction = transaction;
#pragma warning disable CA2100 // SQL is provider-owned; all user values are bound parameters.
        command.CommandText = sql;
#pragma warning restore CA2100
        command.CommandTimeout = 15;
        foreach ((string name, object? value) in values)
        {
            DbParameter parameter = command.CreateParameter();
            parameter.ParameterName = name;
            parameter.Value = value ?? DBNull.Value;
            command.Parameters.Add(parameter);
        }

        return command;
    }

    /// <summary>Executes a provider-owned statement and disposes its command before the next operation.</summary>
    protected static async Task ExecuteAsync(
        DbConnection connection,
        DbTransaction? transaction,
        string sql,
        CancellationToken token,
        params (string Name, object? Value)[] values)
    {
        DbCommand command = Command(
            connection: connection,
            transaction: transaction,
            sql: sql,
            values);
        await using ConfiguredAsyncDisposable commandDisposal = command.ConfigureAwait(false);
        await command.ExecuteNonQueryAsync(token).ConfigureAwait(false);
    }

    /// <summary>Reads a single database value, translating SQL NULL into a nullable result.</summary>
    protected static async Task<object?> ScalarAsync(
        DbConnection connection,
        DbTransaction? transaction,
        string sql,
        CancellationToken token,
        params (string Name, object? Value)[] values)
    {
        DbCommand command = Command(
            connection: connection,
            transaction: transaction,
            sql: sql,
            values);
        await using ConfiguredAsyncDisposable commandDisposal = command.ConfigureAwait(false);
        object? result = await command.ExecuteScalarAsync(token).ConfigureAwait(false);
        return result is DBNull ? null : result;
    }

    /// <summary>Converts required numeric metadata consistently across provider-specific result types.</summary>
    private static long Integer(object? value) => Convert.ToInt64(
        value ?? throw new InvalidDataException("Required label store metadata is missing."),
        CultureInfo.InvariantCulture);

    /// <summary>Validates the schema and locks the stream row before reading its durable boundaries and lifecycle.</summary>
    /// <remarks><para>Only initialization may read an unbound issuer. SQLite's immediate transaction
    /// supplies the lock that other providers acquire with their row-lock suffix.</para></remarks>
    private async Task<LabelStoreState> StateAsync(
        DbConnection connection,
        DbTransaction transaction,
        CancellationToken token,
        bool allowUnbound = false)
    {
        if (Integer(
            await ScalarAsync(
                connection: connection,
                transaction: transaction,
                sql: "SELECT version FROM label_schema WHERE id = 1",
                token: token).ConfigureAwait(false)) != SchemaVersion)
        {
            throw new InvalidDataException("The label database schema version is not supported.");
        }

        DbCommand command = Command(
            connection: connection,
            transaction: transaction,
            sql: "SELECT stream_id, issuer, revision, issued, published, replay_floor, phase, retirement_upper, retirement_through, grace_deadline FROM label_state WHERE id = 1" + LockSuffix);

        await using ConfiguredAsyncDisposable commandDisposal = command.ConfigureAwait(false);
        DbDataReader reader = await command.ExecuteReaderAsync(token).ConfigureAwait(false);
        await using ConfiguredAsyncDisposable readerDisposal = reader.ConfigureAwait(false);
        if (!await reader.ReadAsync(token).ConfigureAwait(false))
        {
            throw new InvalidOperationException("Initialize the label database before use.");
        }

        LabelStoreState state = new(
            StreamId: Guid.ParseExact(reader.GetString(0), "N"),
            Issuer: reader.GetString(1),
            Revision: reader.GetInt64(2),
            IssuedThrough: reader.GetInt64(3),
            PublishedThrough: reader.GetInt64(4),
            ReplayFloor: reader.GetInt64(5),
            Phase: (LabelerLifecyclePhase)reader.GetInt32(6),
            RetirementUpperBound: reader.GetInt64(7),
            RetirementThrough: reader.GetInt64(8),
            GraceDeadline: await reader.IsDBNullAsync(9, token).ConfigureAwait(false) ? null : DateTimeOffset.FromUnixTimeMilliseconds(reader.GetInt64(9)));
        if (!allowUnbound && state.Issuer.Length == 0)
        {
            throw new InvalidOperationException("Bind the database to an issuer with InitializeAsync before use.");
        }

        return state;
    }

    /// <summary>Persists updated boundaries and checkpoints inside the same transaction as their associated writes.</summary>
    private static Task SaveAsync(
        DbConnection connection,
        DbTransaction transaction,
        LabelStoreState state,
        CancellationToken token) =>
        ExecuteAsync(
            connection: connection,
            transaction: transaction,
            sql: """
            UPDATE label_state SET revision=@revision, issued=@issued, published=@published, replay_floor=@floor,
                phase=@phase, retirement_upper=@upper, retirement_through=@through, grace_deadline=@deadline WHERE id=1
            """,
            token: token,
            ("@revision", state.Revision),
            ("@issued", state.IssuedThrough),
            ("@published", state.PublishedThrough),
            ("@floor", state.ReplayFloor),
            ("@phase", (int)state.Phase),
            ("@upper", state.RetirementUpperBound),
            ("@through", state.RetirementThrough),
            ("@deadline", state.GraceDeadline?.ToUnixTimeMilliseconds()));

    /// <summary>Runs an operation under the stream lock with bounded precommit retries and outcome telemetry.</summary>
    /// <remarks><para>The commit-submission flag separates safely retryable work from an uncertain
    /// acknowledgement. Typed decision commits retain that distinction for same-ID recovery.</para></remarks>
    private async Task<T> RunAsync<T>(
        string operation,
        Func<DbConnection, DbTransaction, LabelStoreState, Task<T>> action,
        CancellationToken token,
        bool decisionOperation = false)
    {
        long start = metrics.Start(operation);
        string outcome = "failure";
        try
        {
            int attempt = 0;
            while (true)
            {
                bool committing = false;
                try
                {
                    DbConnection connection = CreateConnection();
                    await using ConfiguredAsyncDisposable connectionDisposal = connection.ConfigureAwait(false);
                    await connection.OpenAsync(token).ConfigureAwait(false);
                    await PrepareAsync(connection, token).ConfigureAwait(false);
                    DbTransaction transaction = await BeginAsync(connection, token).ConfigureAwait(false);
                    await using ConfiguredAsyncDisposable transactionDisposal = transaction.ConfigureAwait(false);
                    LabelStoreState state = await StateAsync(
                        connection: connection,
                        transaction: transaction,
                        token: token).ConfigureAwait(false);
                    T result = await action(connection, transaction, state).ConfigureAwait(false);
                    if (BeforeCommitAsync is { } beforeCommit)
                    {
                        await beforeCommit(connection, transaction, token).ConfigureAwait(false);
                    }

                    token.ThrowIfCancellationRequested();
                    committing = true;
                    await transaction.CommitAsync(token).ConfigureAwait(false);
                    if (AfterCommitAsync is { } afterCommit)
                    {
                        await afterCommit(connection, token).ConfigureAwait(false);
                    }

                    outcome = "success";
                    return result;
                }
                catch (DbException exception) when (!committing && IsTransient(exception) && attempt < 2)
                {
                    metrics.Event(operation, "retry");
                    attempt++;
                    StoreLog.Retry(
                        logger: logger,
                        operation: operation,
                        provider: Provider,
                        attempt: attempt);
                    await Task.Delay(TimeSpan.FromMilliseconds(25 * attempt), token).ConfigureAwait(false);
                }
                catch (Exception exception) when (decisionOperation && committing &&
                    exception is DbException or OperationCanceledException)
                {
                    // A submitted commit cannot be retried as though it rolled back. Preserve this
                    // distinction for the service's same-ID recovery, including cancelled commits.
                    throw new DecisionCommitUncertainException(exception);
                }
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            outcome = "cancelled";
            throw;
        }
        catch (InvalidOperationException)
        {
            outcome = "conflict";
            StoreLog.Conflict(logger, operation, Provider);
            throw;
        }
        catch (DbException exception)
        {
            outcome = IsTransient(exception) ? "contention" : "database";
            StoreLog.Failed(
                logger: logger,
                operation: operation,
                provider: Provider,
                category: outcome);
            throw;
        }
        catch (InvalidDataException)
        {
            outcome = "integrity";
            StoreLog.Failed(
                logger: logger,
                operation: operation,
                provider: Provider,
                category: outcome);
            throw;
        }
        finally
        {
            metrics.Stop(operation, outcome, start);
        }
    }

    public async Task InitializeAsync(Did issuer, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(issuer);
        long start = metrics.Start("initialize");
        string outcome = "failure";
        try
        {
            DbConnection connection = CreateConnection();
            await using ConfiguredAsyncDisposable connectionDisposal = connection.ConfigureAwait(false);
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
            await PrepareAsync(connection, cancellationToken).ConfigureAwait(false);
            await MigrateAsync(connection, cancellationToken).ConfigureAwait(false);
            DbTransaction transaction = await BeginAsync(connection, cancellationToken).ConfigureAwait(false);
            await using ConfiguredAsyncDisposable transactionDisposal = transaction.ConfigureAwait(false);
            // The schema seeds the state row, so concurrent binders lock an existing identity.
            LabelStoreState state = await StateAsync(
                connection: connection,
                transaction: transaction,
                token: cancellationToken,
                allowUnbound: true).ConfigureAwait(false);
            if (state.Issuer.Length == 0)
            {
                await ExecuteAsync(
                    connection: connection,
                    transaction: transaction,
                    sql: "UPDATE label_state SET issuer=@issuer, stream_id=@stream WHERE id=1",
                    token: cancellationToken,
                    ("@issuer", issuer.Value),
                    ("@stream", Guid.NewGuid().ToString("N"))).ConfigureAwait(false);
            }
            else if (!string.Equals(state.Issuer, issuer.Value, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("The database belongs to another label issuer.");
            }

            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            outcome = "success";
            StoreLog.Initialized(logger, Provider);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            outcome = "cancelled";
            throw;
        }
        catch (Exception exception) when (exception is DbException or InvalidDataException or InvalidOperationException)
        {
            StoreLog.Failed(
                logger: logger,
                operation: "initialize",
                provider: Provider,
                category: "schema");
            throw;
        }
        finally
        {
            metrics.Stop("initialize", outcome, start);
        }
    }

    public Task<LabelStoreState> GetStateAsync(CancellationToken cancellationToken = default) =>
        RunAsync(
            operation: "state",
            action: (_, _, state) => Task.FromResult(state),
            token: cancellationToken);

    /// <summary>Rejects decisions made against an older revision when the caller requested optimistic concurrency.</summary>
    private static void Revision(LabelStoreState state, long? expected)
    {
        if (expected.HasValue && state.Revision != expected)
        {
            throw new InvalidOperationException("The label store revision changed; reload state before deciding again.");
        }
    }

    /// <summary>Enforces the common batch bound used by publication, reads and purge.</summary>
    private static void Limit(int limit)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(limit, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(limit, 1000);
    }

    /// <summary>Encodes exact signed batch input and its retirement checkpoint for legacy append idempotency.</summary>
    private static byte[] Request(SignedLabelSnapshot[] labels, long? retirementThrough)
    {
        using MemoryStream buffer = new();
        using BinaryWriter writer = new(buffer, Encoding.UTF8, leaveOpen: true);
        writer.Write(retirementThrough.HasValue);
        if (retirementThrough.HasValue)
        {
            writer.Write(retirementThrough.Value);
        }

        writer.Write(labels.Length);
        foreach (SignedLabelSnapshot label in labels)
        {
            byte[] json = label.GetJsonBytes();
            writer.Write(json.Length);
            writer.Write(json);
        }

        writer.Flush();
        return buffer.ToArray();
    }

    /// <summary>Validates operation keys before hashing or persistence so all providers accept the same identities.</summary>
    private static void OperationId(string operationId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(operationId);
        if (operationId.Contains('\0', StringComparison.Ordinal))
        {
            throw new ArgumentException("An operation key cannot contain U+0000.", nameof(operationId));
        }

        ArgumentOutOfRangeException.ThrowIfGreaterThan(s_strictUtf8.GetByteCount(operationId), 256);
    }

    /// <summary>Produces a fixed-size index key; receipt reads still compare the full identity to detect collisions.</summary>
    private static byte[] OperationHash(string operationId) => SHA256.HashData(Encoding.UTF8.GetBytes(operationId));

    /// <summary>Retrieves a legacy append receipt and its exact input so retries cannot substitute a newly signed batch.</summary>
    private static async Task<(LabelAppendReceipt Receipt, byte[] Request)?> ReceiptAsync(
        DbConnection connection,
        DbTransaction transaction,
        string operationId,
        CancellationToken token)
    {
        DbCommand command = Command(
            connection: connection,
            transaction: transaction,
            sql: "SELECT operation_id, first_seq, last_seq, revision, request FROM label_operations WHERE operation_hash=@hash",
            ("@hash", OperationHash(operationId)));
        await using ConfiguredAsyncDisposable commandDisposal = command.ConfigureAwait(false);
        DbDataReader reader = await command.ExecuteReaderAsync(token).ConfigureAwait(false);
        await using ConfiguredAsyncDisposable readerDisposal = reader.ConfigureAwait(false);
        if (!await reader.ReadAsync(token).ConfigureAwait(false))
        {
            return null;
        }

        if (!string.Equals(reader.GetString(0), operationId, StringComparison.Ordinal))
        {
            throw new InvalidDataException("An operation identity hash collision was detected.");
        }

        return (new LabelAppendReceipt(
            OperationId: operationId,
            FirstSequence: reader.GetInt64(1),
            LastSequence: reader.GetInt64(2),
            Revision: reader.GetInt64(3)), (byte[])reader.GetValue(4));
    }

    public async Task<LabelAppendReceipt> AppendAsync(
        string operationId,
        IReadOnlyList<SignedLabelSnapshot> labels,
        long? expectedRevision = null,
        long? retirementThrough = null,
        CancellationToken cancellationToken = default)
    {
        OperationId(operationId);
        ArgumentNullException.ThrowIfNull(labels);
        SignedLabelSnapshot[] batch = labels.ToArray();
        ArgumentOutOfRangeException.ThrowIfGreaterThan(batch.Length, 1000);
        if (batch.Any(label => label is null) || (batch.Length == 0 && retirementThrough is null))
        {
            throw new ArgumentException("A nonempty fixed batch or retirement checkpoint is required.", nameof(labels));
        }

        byte[] request = Request(batch, retirementThrough);
        bool inserted = false;
        LabelAppendReceipt receipt = await RunAsync(
            operation: "append",
            action: async (connection, transaction, state) =>
        {
            inserted = false;
            (LabelAppendReceipt Receipt, byte[] Request)? existing = await ReceiptAsync(
                connection: connection,
                transaction: transaction,
                operationId: operationId,
                token: cancellationToken).ConfigureAwait(false);
            if (existing.HasValue)
            {
                if (!existing.Value.Request.AsSpan().SequenceEqual(request))
                {
                    throw new InvalidOperationException("The operation key already identifies different signed input.");
                }

                metrics.Event("append", "idempotent");
                return existing.Value.Receipt;
            }

            Revision(state, expectedRevision);
            if (state.Issuer.Length == 0)
            {
                throw new InvalidOperationException("Bind the database to an issuer before appending.");
            }

            if (retirementThrough.HasValue)
            {
                if (state.Phase != LabelerLifecyclePhase.Retiring || retirementThrough <= state.RetirementThrough ||
                    retirementThrough > state.RetirementUpperBound || batch.Any(label => !label.Label.IsNegationLabel))
                {
                    throw new InvalidOperationException("Invalid retirement batch or checkpoint.");
                }
            }
            else if (state.Phase != LabelerLifecyclePhase.Active)
            {
                throw new InvalidOperationException("Ordinary issuance is closed during retirement.");
            }

            long last = checked(state.IssuedThrough + batch.Length);
            long now = Integer(
                await ScalarAsync(
                    connection: connection,
                    transaction: transaction,
                    sql: ClockSql,
                    token: cancellationToken).ConfigureAwait(false));
            for (int i = 0; i < batch.Length; i++)
            {
                Label label = batch[i].Label;
                if (!string.Equals(label.Source.Value, state.Issuer, StringComparison.Ordinal))
                {
                    throw new InvalidDataException("A signed label has the wrong issuer.");
                }

                await ExecuteAsync(
                    connection: connection,
                    transaction: transaction,
                    sql: "INSERT INTO label_entries(seq, signed_json, payload, format_version, queued_at) VALUES(@seq,@json,@payload,1,@now)",
                    token: cancellationToken,
                    ("@seq", checked(state.IssuedThrough + i + 1)),
                    ("@json", batch[i].GetJsonBytes()),
                    ("@payload", batch[i].GetCanonicalUnsignedBytes()),
                    ("@now", now)).ConfigureAwait(false);
            }

            LabelStoreState next = state with
            {
                Revision = checked(state.Revision + 1),
                IssuedThrough = last,
                RetirementThrough = retirementThrough ?? state.RetirementThrough,
                Phase = retirementThrough == state.RetirementUpperBound && retirementThrough.HasValue
                    ? LabelerLifecyclePhase.Distributing : state.Phase
            };
            await SaveAsync(
                connection: connection,
                transaction: transaction,
                state: next,
                token: cancellationToken).ConfigureAwait(false);
            LabelAppendReceipt result = new(
                OperationId: operationId,
                FirstSequence: batch.Length == 0 ? 0 : checked(state.IssuedThrough + 1),
                LastSequence: batch.Length == 0 ? 0 : last,
                Revision: next.Revision);
            await ExecuteAsync(
                connection: connection,
                transaction: transaction,
                sql: "INSERT INTO label_operations(operation_hash, operation_id, first_seq, last_seq, revision, request) VALUES(@hash,@id,@first,@last,@revision,@request)",
                token: cancellationToken,
                ("@hash", OperationHash(operationId)),
                ("@id", operationId),
                ("@first", result.FirstSequence),
                ("@last", result.LastSequence),
                ("@revision", result.Revision),
                ("@request", request)).ConfigureAwait(false);
            inserted = true;
            return result;
        },
            token: cancellationToken).ConfigureAwait(false);
        if (inserted)
        {
            metrics.Labels("append", batch.Length, metrics.BytesEnabled ? batch.Sum(label => (long)label.GetJsonBytes().Length) : 0);
        }

        return receipt;
    }

    public Task<LabelAppendReceipt?> GetReceiptAsync(string operationId, CancellationToken cancellationToken = default)
    {
        OperationId(operationId);
        return RunAsync<LabelAppendReceipt?>(
            operation: "receipt",
            action: async (connection, transaction, _) =>
            (await ReceiptAsync(
                connection: connection,
                transaction: transaction,
                operationId: operationId,
                token: cancellationToken).ConfigureAwait(false))?.Receipt,
            token: cancellationToken);
    }

    /// <summary>Returns an explicit storage failure without misclassifying corrupt data or programming errors.</summary>
    private static async Task<LabelServiceResult<T>> DecisionReadAsync<T>(
        Func<Task<LabelServiceResult<T>>> action) where T : class
    {
        try
        {
            return await action().ConfigureAwait(false);
        }
        catch (DbException exception)
        {
            return LabelServiceResult<T>.Failure(
                new(
                    code: LabelServiceErrorCode.StorageUnavailable,
                    message: "Label storage is unavailable; retry the original request when storage recovers.",
                    exception: exception));
        }
    }

    /// <summary>Reads an operation independently of signed append receipts and checks full identities after hashing.</summary>
    private static async Task<LabelOperationReceipt?> DecisionReceiptAsync(
        DbConnection connection,
        DbTransaction transaction,
        string operationId,
        CancellationToken token)
    {
        DbCommand command = Command(
            connection: connection,
            transaction: transaction,
            sql: "SELECT operation_id, receipt FROM label_decisions WHERE operation_hash=@hash",
            ("@hash", OperationHash(operationId)));
        await using ConfiguredAsyncDisposable commandDisposal = command.ConfigureAwait(false);
        DbDataReader reader = await command.ExecuteReaderAsync(token).ConfigureAwait(false);
        await using ConfiguredAsyncDisposable readerDisposal = reader.ConfigureAwait(false);
        if (!await reader.ReadAsync(token).ConfigureAwait(false))
        {
            return null;
        }

        if (!string.Equals(operationId, reader.GetString(0), StringComparison.Ordinal))
        {
            throw new InvalidDataException("A service operation identity hash collision was detected.");
        }

        LabelOperationReceipt receipt = DecisionReceiptEncoding.Decode((byte[])reader.GetValue(1));
        if (!string.Equals(receipt.Request.OperationId, operationId, StringComparison.Ordinal))
        {
            throw new InvalidDataException("The service receipt has a different operation identity.");
        }

        return receipt;
    }

    public Task<LabelServiceResult<LabelOperationReceipt>> GetOperationAsync(
        string operationId,
        CancellationToken cancellationToken = default)
    {
        OperationId(operationId);
        return DecisionReadAsync(
            () => RunAsync(
                operation: "decision-receipt",
                action: async (connection, transaction, _) =>
        {
            LabelOperationReceipt? receipt = await DecisionReceiptAsync(
                connection: connection,
                transaction: transaction,
                operationId: operationId,
                token: cancellationToken).ConfigureAwait(false);
            return receipt is null ? LabelServiceResult<LabelOperationReceipt>.Failure(
                new(
                    code: LabelServiceErrorCode.OperationNotFound,
                    message: "No retained service receipt exists for this operation.")) : LabelServiceResult<LabelOperationReceipt>.Success(receipt);
        },
                token: cancellationToken));
    }

    /// <summary>Finds the protocol-current decision from authoritative history, including the unpublished tail.</summary>
    /// <remarks><para>No projection can become stale when legacy or retirement appends write labels. This
    /// initial implementation scans history under the existing stream lock; it favors consistent semantics
    /// across providers over an independently maintained state index.</para></remarks>
    private static async Task<LabelServiceResult<LabelDecisionSnapshot>> DecisionSnapshotAsync(
        DbConnection connection,
        DbTransaction transaction,
        LabelStoreState state,
        LabelTarget target,
        string value,
        CancellationToken token)
    {
        StoredLabel? current = null;
        bool ambiguous = false;
        DbCommand command = Command(
            connection: connection,
            transaction: transaction,
            sql: "SELECT seq, signed_json, payload, format_version FROM label_entries ORDER BY seq");
        await using ConfiguredAsyncDisposable commandDisposal = command.ConfigureAwait(false);
        DbDataReader reader = await command.ExecuteReaderAsync(token).ConfigureAwait(false);
        await using ConfiguredAsyncDisposable readerDisposal = reader.ConfigureAwait(false);
        while (await reader.ReadAsync(token).ConfigureAwait(false))
        {
            SignedLabelSnapshot signed = Restore(reader);
            Label label = signed.Label;
            if (!string.Equals(label.Uri, target.Subject, StringComparison.Ordinal) ||
                !string.Equals(label.Value, value, StringComparison.Ordinal))
            {
                continue;
            }

            int order = current is null ? 1 : LabelDecisionRules.CompareCreation(signed, current.SignedLabel);
            if (order > 0)
            {
                current = new(reader.GetInt64(0), signed);
                ambiguous = false;
            }
            else if (order == 0 && current is not null &&
                !signed.GetCanonicalUnsignedBytes().AsSpan().SequenceEqual(current.SignedLabel.GetCanonicalUnsignedBytes()))
            {
                ambiguous = true;
            }
        }

        return ambiguous ? LabelServiceResult<LabelDecisionSnapshot>.Failure(
            new(
                code: LabelServiceErrorCode.AmbiguousHistory,
                message: "Conflicting labels have the same latest creation timestamp; inspect history before deciding."))
            : LabelServiceResult<LabelDecisionSnapshot>.Success(new(state, current));
    }

    public Task<LabelServiceResult<LabelDecisionSnapshot>> ReadDecisionAsync(
        LabelTarget resolvedTarget,
        string value,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(resolvedTarget);
        LabelInput.ValidateText(value, nameof(value), 128);
        if (resolvedTarget.Repository is not Did)
        {
            throw new ArgumentException("Decision reads require a stable DID target.", nameof(resolvedTarget));
        }

        return DecisionReadAsync(
            () => RunAsync(
                operation: "decision-read",
                action: (connection, transaction, state) =>
            DecisionSnapshotAsync(
                connection: connection,
                transaction: transaction,
                state: state,
                target: resolvedTarget,
                value: value,
                token: cancellationToken),
                token: cancellationToken));
    }

    public async Task<LabelServiceResult<LabelOperationReceipt>> CommitDecisionAsync(
        LabelDecisionCommit decision,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(decision);
        try
        {
            return await DecisionReadAsync(
                () => RunAsync(
                    operation: "decision-commit",
                    action: async (connection, transaction, state) =>
            {
                LabelOperationRequest request = decision.Request;
                LabelOperationReceipt? existing = await DecisionReceiptAsync(
                    connection: connection,
                    transaction: transaction,
                    operationId: request.OperationId,
                    token: cancellationToken).ConfigureAwait(false);
                if (existing is not null)
                {
                    return existing.Request.SemanticallyEquals(request) ? LabelServiceResult<LabelOperationReceipt>.Success(existing)
                        : LabelServiceResult<LabelOperationReceipt>.Failure(
                            new(
                                code: LabelServiceErrorCode.IdempotencyConflict,
                                message: "This operation ID already identifies different intent; recover the original receipt."));
                }

                if (state.Revision != decision.ExpectedRevision)
                {
                    return LabelServiceResult<LabelOperationReceipt>.Failure(
                        new(
                            code: LabelServiceErrorCode.StaleDecision,
                            message: "The store revision changed; reload and reauthorize the original decision."));
                }

                if (state.Phase != LabelerLifecyclePhase.Active)
                {
                    return LabelServiceResult<LabelOperationReceipt>.Failure(
                        new(
                            code: LabelServiceErrorCode.IssuanceClosed,
                            message: "Ordinary issuance and negation are closed by the labeler lifecycle."));
                }

                LabelServiceResult<LabelDecisionSnapshot> snapshot = await DecisionSnapshotAsync(
                    connection: connection,
                    transaction: transaction,
                    state: state,
                    target: decision.ResolvedTarget,
                    value: request.Value,
                    token: cancellationToken).ConfigureAwait(false);
                if (!snapshot.Succeeded)
                {
                    return LabelServiceResult<LabelOperationReceipt>.Failure(snapshot.Error);
                }

                StoredLabel? current = snapshot.Result.CurrentLabel;
                if (request.Kind == LabelOperationKind.Negate && request.Target.Cid is not null &&
                    (current is null || current.SignedLabel.Label.Cid != request.Target.Cid))
                {
                    return LabelServiceResult<LabelOperationReceipt>.Failure(
                        new(
                            code: LabelServiceErrorCode.StaleDecision,
                            message: "The requested withdrawal CID is not the current stored decision."));
                }

                if (request.Kind == LabelOperationKind.Negate && decision.ResolvedTarget.Cid != current?.SignedLabel.Label.Cid)
                {
                    throw new ArgumentException("The resolved withdrawal scope must copy the current decision CID.");
                }

                bool changed = decision.SignedLabel is not null;
                if (changed != LabelDecisionRules.NeedsLabel(request, current, decision.Audit.EvaluatedAt))
                {
                    throw new ArgumentException("The signed decision does not match the current state or no-op intent.");
                }

                StoredLabel? outcome = current;
                if (decision.SignedLabel is { } signed)
                {
                    Label label = signed.Label;
                    Cid? expectedCid = request.Kind == LabelOperationKind.Issue ? request.Target.Cid : current!.SignedLabel.Label.Cid;
                    if (label.Source.Value != state.Issuer || label.Uri != decision.ResolvedTarget.Subject ||
                        label.Value != request.Value || label.Cid != expectedCid ||
                        label.IsNegationLabel != (request.Kind == LabelOperationKind.Negate) ||
                        !LabelDecisionRules.HasExpiry(signed, request.ExpiresAt) ||
                        label.CreationTimestamp < decision.Audit.EvaluatedAt ||
                        (label.ExpiresAt is { } expiry && expiry <= label.CreationTimestamp) ||
                        (current is not null && !LabelDecisionRules.IsLater(signed, current.SignedLabel)))
                    {
                        throw new ArgumentException("The signed label does not represent the authorized decision.");
                    }

                    long sequence = checked(state.IssuedThrough + 1);
                    long now = Integer(
                        await ScalarAsync(
                            connection: connection,
                            transaction: transaction,
                            sql: ClockSql,
                            token: cancellationToken).ConfigureAwait(false));
                    await ExecuteAsync(
                        connection: connection,
                        transaction: transaction,
                        sql: "INSERT INTO label_entries(seq, signed_json, payload, format_version, queued_at) VALUES(@seq,@json,@payload,1,@now)",
                        token: cancellationToken,
                        ("@seq", sequence),
                        ("@json", signed.GetJsonBytes()),
                        ("@payload", signed.GetCanonicalUnsignedBytes()),
                        ("@now", now)).ConfigureAwait(false);
                    outcome = new(sequence, signed);
                }

                LabelStoreState next = state with
                {
                    Revision = checked(state.Revision + 1),
                    IssuedThrough = changed ? checked(state.IssuedThrough + 1) : state.IssuedThrough
                };
                LabelOperationReceipt receipt = new(
                    request: request,
                    resolvedTarget: decision.ResolvedTarget,
                    changed: changed,
                    label: outcome,
                    stateAtCommit: next,
                    audit: decision.Audit);
                await SaveAsync(
                    connection: connection,
                    transaction: transaction,
                    state: next,
                    token: cancellationToken).ConfigureAwait(false);
                await ExecuteAsync(
                    connection: connection,
                    transaction: transaction,
                    sql: "INSERT INTO label_decisions(operation_hash,operation_id,revision,receipt) VALUES(@hash,@id,@revision,@receipt)",
                    token: cancellationToken,
                    ("@hash", OperationHash(request.OperationId)),
                    ("@id", request.OperationId),
                    ("@revision", next.Revision),
                    ("@receipt", DecisionReceiptEncoding.Encode(receipt))).ConfigureAwait(false);
                return LabelServiceResult<LabelOperationReceipt>.Success(receipt);
            },
                    token: cancellationToken,
                    decisionOperation: true)).ConfigureAwait(false);
        }
        catch (DecisionCommitUncertainException exception)
        {
            StoreLog.Failed(
                logger: logger,
                operation: "decision-commit",
                provider: Provider,
                category: "outcome-unknown");
            return LabelServiceResult<LabelOperationReceipt>.Failure(
                new(
                    code: LabelServiceErrorCode.OutcomeUnknown,
                    message: "The commit acknowledgement is uncertain; recover the original operation ID before retrying.",
                    exception: exception.InnerException));
        }
    }

    public async Task<LabelStoreState> PublishAsync(int limit = 100, CancellationToken cancellationToken = default)
    {
        Limit(limit);
        long count = 0;
        LabelStoreState result = await RunAsync(
            operation: "publish",
            action: async (connection, transaction, state) =>
        {
            count = Math.Min(limit, state.IssuedThrough - state.PublishedThrough);
            if (count == 0)
            {
                return state;
            }

            LabelStoreState next = state with
            {
                Revision = checked(state.Revision + 1),
                PublishedThrough = checked(state.PublishedThrough + count)
            };
            await SaveAsync(
                connection: connection,
                transaction: transaction,
                state: next,
                token: cancellationToken).ConfigureAwait(false);
            return next;
        },
            token: cancellationToken).ConfigureAwait(false);
        metrics.Labels("publish", count, 0);
        return result;
    }

    /// <summary>Restores exact signed JSON and checks it still agrees with the stored canonical signing payload.</summary>
    private static SignedLabelSnapshot Restore(DbDataReader reader)
    {
        if (reader.GetInt32(3) != 1)
        {
            throw new InvalidDataException("The stored label representation version is not supported.");
        }

        byte[] json = (byte[])reader.GetValue(1);
        SignedLabelSnapshot signed = new(json);
        if (!signed.GetCanonicalUnsignedBytes().AsSpan().SequenceEqual((byte[])reader.GetValue(2)))
        {
            throw new InvalidDataException("The stored signed label representation is inconsistent.");
        }

        return signed;
    }

    /// <summary>Binds a query continuation to its original ordered patterns and issuer filter.</summary>
    private static string Fingerprint(string[] patterns, Did? source)
    {
        using MemoryStream buffer = new();
        using BinaryWriter writer = new(buffer, s_strictUtf8);
        writer.Write(source?.Value ?? string.Empty);
        foreach (string pattern in patterns)
        {
            writer.Write(pattern);
        }

        writer.Flush();
        return Convert.ToHexString(SHA256.HashData(buffer.ToArray()));
    }

    /// <summary>Encodes a versioned continuation with a fixed published tip and the last scanned sequence.</summary>
    private static string Cursor(
        Guid stream,
        long tip,
        long last,
        string fingerprint) =>
        Convert.ToBase64String(Encoding.UTF8.GetBytes(string.Create(CultureInfo.InvariantCulture, $"1|{stream:N}|{tip}|{last}|{fingerprint}")));

    /// <summary>Rejects continuations from another query, stream or unavailable range before resuming a scan.</summary>
    private static (long Tip, long Last) ReadCursor(string cursor, LabelStoreState state, string fingerprint)
    {
        if (cursor.Length > 512)
        {
            throw new InvalidDataException("The label cursor is too long.");
        }

        try
        {
            string[] fields = Encoding.UTF8.GetString(Convert.FromBase64String(cursor)).Split('|');
            if (fields.Length != 5 || fields[0] != "1" || fields[1] != state.StreamId.ToString("N") || fields[4] != fingerprint)
            {
                throw new InvalidDataException("The label cursor has a different format, stream or filter.");
            }

            long tip = long.Parse(fields[2], NumberStyles.None, CultureInfo.InvariantCulture);
            long last = long.Parse(fields[3], NumberStyles.None, CultureInfo.InvariantCulture);
            Boundaries(state, last, tip);
            return (tip, last);
        }
        catch (Exception exception) when (exception is FormatException or OverflowException)
        {
            throw new InvalidDataException("The label cursor is malformed.", exception);
        }
    }

    /// <summary>Prevents replay of purged history, reversed ranges or events not yet published.</summary>
    private static void Boundaries(LabelStoreState state, long after, long through)
    {
        if (after < state.ReplayFloor || through < after || through > state.PublishedThrough)
        {
            throw new InvalidDataException("The replay boundaries are pruned, reversed or beyond the published tip.");
        }
    }

    /// <summary>Scans bounded sequence chunks and applies ordinal filters without relying on database collations.</summary>
    /// <remarks><para>The continuation tracks the last scanned event, not just the last match, so sparse
    /// filters can advance without skipping matching events or exposing labels beyond the captured tip.</para></remarks>
    private static async Task<LabelPage> PageAsync(
        DbConnection connection,
        DbTransaction transaction,
        LabelStoreState state,
        long after,
        long through,
        int limit,
        string[] patterns,
        Did? source,
        string? fingerprint,
        CancellationToken token)
    {
        List<StoredLabel> labels = [];
        long scanned = after;
        while (scanned < through && labels.Count < limit)
        {
            int scanLimit = patterns.Length == 0 && source is null ? limit - labels.Count : 1000;
            DbCommand command = Command(
                connection: connection,
                transaction: transaction,
                sql: "SELECT seq, signed_json, payload, format_version FROM label_entries WHERE seq>@after AND seq<=@through ORDER BY seq LIMIT @limit",
                ("@after", scanned),
                ("@through", through),
                ("@limit", scanLimit));
            await using ConfiguredAsyncDisposable commandDisposal = command.ConfigureAwait(false);
            DbDataReader reader = await command.ExecuteReaderAsync(token).ConfigureAwait(false);
            await using ConfiguredAsyncDisposable readerDisposal = reader.ConfigureAwait(false);
            bool found = false;
            while (labels.Count < limit && await reader.ReadAsync(token).ConfigureAwait(false))
            {
                found = true;
                scanned = reader.GetInt64(0);
                SignedLabelSnapshot signed = Restore(reader);
                Label label = signed.Label;
                bool matches = patterns.Length == 0 || patterns.Any(
                    pattern => pattern.EndsWith('*')
                    ? label.Uri.StartsWith(pattern[..^1], StringComparison.Ordinal)
                    : string.Equals(label.Uri, pattern, StringComparison.Ordinal));
                if (matches && (source is null || source == label.Source))
                {
                    labels.Add(new StoredLabel(scanned, signed));
                }
            }

            if (!found)
            {
                scanned = through;
            }
        }

        return new LabelPage(
            StreamId: state.StreamId,
            Through: through,
            Labels: labels.AsReadOnly(),
            Cursor: fingerprint is not null && scanned < through ? Cursor(
                stream: state.StreamId,
                tip: through,
                last: scanned,
                fingerprint: fingerprint) : null);
    }

    public async Task<LabelPage> QueryAsync(
        IReadOnlyList<string> uriPatterns,
        Did? source = null,
        string? cursor = null,
        int limit = 100,
        CancellationToken cancellationToken = default)
    {
        Limit(limit);
        ArgumentNullException.ThrowIfNull(uriPatterns);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(uriPatterns.Count, 100);
        string[] patterns = uriPatterns.ToArray();
        foreach (string pattern in patterns)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(pattern);
            if (pattern.IndexOf('*', StringComparison.Ordinal) is int wildcard && wildcard >= 0 && wildcard != pattern.Length - 1)
            {
                throw new ArgumentException("Only a trailing URI wildcard is supported.", nameof(uriPatterns));
            }
        }

        string fingerprint = Fingerprint(patterns, source);
        LabelPage page = await RunAsync(
            operation: "query",
            action: (connection, transaction, state) =>
        {
            (long tip, long last) = cursor is null ? (state.PublishedThrough, state.ReplayFloor) : ReadCursor(cursor, state, fingerprint);
            return PageAsync(
                connection: connection,
                transaction: transaction,
                state: state,
                after: last,
                through: tip,
                limit: limit,
                patterns: patterns,
                source: source,
                fingerprint: fingerprint,
                token: cancellationToken);
        },
            token: cancellationToken).ConfigureAwait(false);
        metrics.Labels("query", page.Labels.Count, metrics.BytesEnabled ? page.Labels.Sum(label => (long)label.SignedLabel.GetJsonBytes().Length) : 0);
        return page;
    }

    public async Task<LabelPage> ReplayAsync(
        Guid streamId,
        long after,
        long through,
        int limit = 100,
        CancellationToken cancellationToken = default)
    {
        Limit(limit);
        LabelPage page = await RunAsync(
            operation: "replay",
            action: (connection, transaction, state) =>
        {
            if (streamId != state.StreamId)
            {
                throw new InvalidDataException("The replay stream identity differs.");
            }

            Boundaries(state, after, through);
            return PageAsync(
                connection: connection,
                transaction: transaction,
                state: state,
                after: after,
                through: through,
                limit: limit,
                patterns: [],
                source: null,
                fingerprint: null,
                token: cancellationToken);
        },
            token: cancellationToken).ConfigureAwait(false);
        metrics.Labels("replay", page.Labels.Count, metrics.BytesEnabled ? page.Labels.Sum(label => (long)label.SignedLabel.GetJsonBytes().Length) : 0);
        return page;
    }

    /// <summary>Commits a lifecycle transition using locked state and database time, then reports the new phase.</summary>
    private async Task<LabelStoreState> TransitionAsync(
        string operation,
        long expectedRevision,
        Func<LabelStoreState, long, LabelStoreState> transition,
        CancellationToken token)
    {
        LabelStoreState result = await RunAsync(
            operation: operation,
            action: async (connection, transaction, state) =>
        {
            Revision(state, expectedRevision);
            long now = Integer(
                await ScalarAsync(
                    connection: connection,
                    transaction: transaction,
                    sql: ClockSql,
                    token: token).ConfigureAwait(false));
            LabelStoreState next = transition(state, now) with { Revision = checked(state.Revision + 1) };
            await SaveAsync(
                connection: connection,
                transaction: transaction,
                state: next,
                token: token).ConfigureAwait(false);
            return next;
        },
            token: token).ConfigureAwait(false);
        StoreLog.Transition(logger, result.Phase, result.Revision);
        return result;
    }

    public Task<LabelStoreState> BeginRetirementAsync(long expectedRevision, CancellationToken cancellationToken = default) =>
        TransitionAsync(
            operation: "retirement",
            expectedRevision: expectedRevision,
            transition: (state, _) =>
        {
            if (state.Phase != LabelerLifecyclePhase.Active)
            {
                throw new InvalidOperationException("Retirement has already begun.");
            }

            return state with
            {
                Phase = state.IssuedThrough == 0 ? LabelerLifecyclePhase.Distributing : LabelerLifecyclePhase.Retiring,
                RetirementUpperBound = state.IssuedThrough
            };
        },
            token: cancellationToken);

    public Task<LabelStoreState> BeginGracePeriodAsync(long expectedRevision, TimeSpan gracePeriod, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(gracePeriod, TimeSpan.Zero);
        return TransitionAsync(
            operation: "grace",
            expectedRevision: expectedRevision,
            transition: (state, now) =>
            state.Phase == LabelerLifecyclePhase.Distributing && state.IssuedThrough == state.PublishedThrough
                ? state with
                {
                    Phase = LabelerLifecyclePhase.Grace,
                    // The clock sample is truncated to milliseconds; include that quantization interval.
                    GraceDeadline = DateTimeOffset.FromUnixTimeMilliseconds(checked(now + (long)Math.Ceiling(gracePeriod.TotalMilliseconds) + 1))
                }
                : throw new InvalidOperationException("Retirement enumeration and publication must finish before grace begins."),
            token: cancellationToken);
    }

    public Task<LabelStoreState> CompleteRetirementAsync(long expectedRevision, CancellationToken cancellationToken = default) =>
        TransitionAsync(
            operation: "retired",
            expectedRevision: expectedRevision,
            transition: (state, now) =>
            state.Phase == LabelerLifecyclePhase.Grace && state.GraceDeadline <= DateTimeOffset.FromUnixTimeMilliseconds(now)
                ? state with { Phase = LabelerLifecyclePhase.Retired }
                : throw new InvalidOperationException("The distribution grace deadline has not completed."),
            token: cancellationToken);

    public Task<LabelStoreState> PurgeAsync(
        long expectedRevision,
        bool confirmed,
        int limit = 100,
        CancellationToken cancellationToken = default)
    {
        Limit(limit);
        if (!confirmed)
        {
            throw new ArgumentException("Local purge requires explicit confirmation.", nameof(confirmed));
        }

        return RunAsync(
            operation: "purge",
            action: async (connection, transaction, state) =>
        {
            Revision(state, expectedRevision);
            if (state.Phase is not (LabelerLifecyclePhase.Retired or LabelerLifecyclePhase.Purging))
            {
                throw new InvalidOperationException("Only a completely retired labeler can be purged.");
            }

            long nextFloor = state.ReplayFloor + Math.Min(limit, state.IssuedThrough - state.ReplayFloor);
            await ExecuteAsync(
                connection: connection,
                transaction: transaction,
                sql: "DELETE FROM label_entries WHERE seq<=@floor",
                token: cancellationToken,
                ("@floor", nextFloor)).ConfigureAwait(false);
            // Receipts are bounded separately, including zero-label retirement checkpoint receipts.
            DbCommand keys = Command(
                connection: connection,
                transaction: transaction,
                sql: "SELECT operation_hash FROM label_operations ORDER BY revision LIMIT @limit",
                ("@limit", limit));
            await using ConfiguredAsyncDisposable keysDisposal = keys.ConfigureAwait(false);
            List<byte[]> hashes = [];
            DbDataReader reader = await keys.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            await using (reader.ConfigureAwait(false))
            {
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    hashes.Add((byte[])reader.GetValue(0));
                }
            }

            foreach (byte[] hash in hashes)
            {
                await ExecuteAsync(
                    connection: connection,
                    transaction: transaction,
                    sql: "DELETE FROM label_operations WHERE operation_hash=@hash",
                    token: cancellationToken,
                    ("@hash", hash)).ConfigureAwait(false);
            }

            object? remaining = await ScalarAsync(
                connection: connection,
                transaction: transaction,
                sql: "SELECT 1 FROM label_operations LIMIT 1",
                token: cancellationToken).ConfigureAwait(false);
            // Service receipts include no-op decisions with no sequence. Purge them independently in
            // bounded revision order so neither those receipts nor their audit context survive purge.
            await ExecuteAsync(
                connection: connection,
                transaction: transaction,
                sql: "DELETE FROM label_decisions WHERE revision IN (SELECT revision FROM (SELECT revision FROM label_decisions ORDER BY revision LIMIT @limit) AS purge_decisions)",
                token: cancellationToken,
                ("@limit", limit)).ConfigureAwait(false);
            object? decisionsRemaining = await ScalarAsync(
                connection: connection,
                transaction: transaction,
                sql: "SELECT 1 FROM label_decisions LIMIT 1",
                token: cancellationToken).ConfigureAwait(false);
            LabelStoreState next = state with
            {
                Revision = checked(state.Revision + 1),
                ReplayFloor = nextFloor,
                Phase = nextFloor == state.IssuedThrough && remaining is null && decisionsRemaining is null
                    ? LabelerLifecyclePhase.Purged : LabelerLifecyclePhase.Purging
            };
            await SaveAsync(
                connection: connection,
                transaction: transaction,
                state: next,
                token: cancellationToken).ConfigureAwait(false);
            return next;
        },
            token: cancellationToken);
    }

    public async Task<LabelStoreSnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default)
    {
        LabelStoreSnapshot snapshot = await RunAsync(
            operation: "snapshot",
            action: async (connection, transaction, state) =>
        {
            object? oldest = await ScalarAsync(
                connection: connection,
                transaction: transaction,
                sql: "SELECT MIN(queued_at) FROM label_entries WHERE seq>@published",
                token: cancellationToken,
                ("@published", state.PublishedThrough)).ConfigureAwait(false);
            long now = Integer(
                await ScalarAsync(
                    connection: connection,
                    transaction: transaction,
                    sql: ClockSql,
                    token: cancellationToken).ConfigureAwait(false));
            return new LabelStoreSnapshot(
                state,
                state.IssuedThrough - state.PublishedThrough,
                oldest is null ? TimeSpan.Zero : TimeSpan.FromMilliseconds(Math.Max(0, now - Integer(oldest))));
        },
            token: cancellationToken).ConfigureAwait(false);
        metrics.Snapshot(snapshot);
        return snapshot;
    }
}
