// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Data.Common;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

using idunno.AtProto;
using idunno.AtProto.Labels;

using Microsoft.Extensions.Logging;

namespace idunno.Bluesky.Labeler.Storage;

// Linked into each provider assembly; neither SQL nor client types enter the contracts assembly.
internal abstract class RelationalLabelStore(ILogger logger, StoreMetrics metrics) : ILabelStore
{
    private const int SchemaVersion = 1;
    private static readonly UTF8Encoding s_strictUtf8 = new(false, true);
    internal Func<DbConnection, DbTransaction, CancellationToken, Task>? BeforeCommitAsync { get; set; }
    internal Func<DbConnection, CancellationToken, Task>? AfterCommitAsync { get; set; }
    protected abstract string Provider { get; }
    protected abstract string LockSuffix { get; }
    protected abstract string ClockSql { get; }
    protected abstract DbConnection CreateConnection();
    protected abstract Task<DbTransaction> BeginAsync(DbConnection connection, CancellationToken token);
    protected abstract Task PrepareAsync(DbConnection connection, CancellationToken token);
    protected abstract Task MigrateAsync(DbConnection connection, CancellationToken token);
    protected abstract bool IsTransient(DbException exception);

    protected static async Task ApplySchemaAsync(DbConnection connection, DbTransaction? transaction, CancellationToken token)
    {
        using Stream stream = typeof(RelationalLabelStore).Assembly.GetManifestResourceStream("LabelSchema")
            ?? throw new InvalidOperationException("The packaged label schema is missing.");
        using StreamReader reader = new(stream);
        string schema = await reader.ReadToEndAsync(token).ConfigureAwait(false);
        int firstStatement = schema.IndexOf(';', StringComparison.Ordinal);
        await ExecuteAsync(connection, transaction, schema[..(firstStatement + 1)], token).ConfigureAwait(false);
        object? version = await ScalarAsync(connection, transaction, "SELECT version FROM label_schema WHERE id=1", token).ConfigureAwait(false);
        if (version is not null && Integer(version) != SchemaVersion)
        {
            throw new InvalidDataException("The label database schema version is not supported.");
        }

        await ExecuteAsync(connection, transaction, schema, token).ConfigureAwait(false);
    }

    protected static DbCommand Command(DbConnection connection, DbTransaction? transaction, string sql,
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

    protected static async Task ExecuteAsync(DbConnection connection, DbTransaction? transaction, string sql,
        CancellationToken token, params (string Name, object? Value)[] values)
    {
        DbCommand command = Command(connection, transaction, sql, values);
        await using var commandDisposal = command.ConfigureAwait(false);
        await command.ExecuteNonQueryAsync(token).ConfigureAwait(false);
    }

    protected static async Task<object?> ScalarAsync(DbConnection connection, DbTransaction? transaction, string sql,
        CancellationToken token, params (string Name, object? Value)[] values)
    {
        DbCommand command = Command(connection, transaction, sql, values);
        await using var commandDisposal = command.ConfigureAwait(false);
        object? result = await command.ExecuteScalarAsync(token).ConfigureAwait(false);
        return result is DBNull ? null : result;
    }

    private static long Integer(object? value) => Convert.ToInt64(
        value ?? throw new InvalidDataException("Required label store metadata is missing."), CultureInfo.InvariantCulture);

    private async Task<LabelStoreState> StateAsync(DbConnection connection, DbTransaction transaction, CancellationToken token, bool allowUnbound = false)
    {
        if (Integer(await ScalarAsync(connection, transaction, "SELECT version FROM label_schema WHERE id = 1", token).ConfigureAwait(false)) != SchemaVersion)
        {
            throw new InvalidDataException("The label database schema version is not supported.");
        }

        DbCommand command = Command(connection, transaction,
            "SELECT stream_id, issuer, revision, issued, published, replay_floor, phase, retirement_upper, retirement_through, grace_deadline FROM label_state WHERE id = 1" + LockSuffix);

        await using var commandDisposal = command.ConfigureAwait(false);
        DbDataReader reader = await command.ExecuteReaderAsync(token).ConfigureAwait(false);
        await using var readerDisposal = reader.ConfigureAwait(false);
        if (!await reader.ReadAsync(token).ConfigureAwait(false))
        {
            throw new InvalidOperationException("Initialize the label database before use.");
        }

        LabelStoreState state = new(Guid.ParseExact(reader.GetString(0), "N"), reader.GetString(1), reader.GetInt64(2),
            reader.GetInt64(3), reader.GetInt64(4), reader.GetInt64(5), (LabelerLifecyclePhase)reader.GetInt32(6),
            reader.GetInt64(7), reader.GetInt64(8),
            await reader.IsDBNullAsync(9, token).ConfigureAwait(false) ? null : DateTimeOffset.FromUnixTimeMilliseconds(reader.GetInt64(9)));
        if (!allowUnbound && state.Issuer.Length == 0)
        {
            throw new InvalidOperationException("Bind the database to an issuer with InitializeAsync before use.");
        }

        return state;
    }

    private static Task SaveAsync(DbConnection connection, DbTransaction transaction, LabelStoreState state, CancellationToken token) =>
        ExecuteAsync(connection, transaction,
            """
            UPDATE label_state SET revision=@revision, issued=@issued, published=@published, replay_floor=@floor,
                phase=@phase, retirement_upper=@upper, retirement_through=@through, grace_deadline=@deadline WHERE id=1
            """, token, ("@revision", state.Revision), ("@issued", state.IssuedThrough), ("@published", state.PublishedThrough),
            ("@floor", state.ReplayFloor), ("@phase", (int)state.Phase), ("@upper", state.RetirementUpperBound),
            ("@through", state.RetirementThrough), ("@deadline", state.GraceDeadline?.ToUnixTimeMilliseconds()));

    private async Task<T> RunAsync<T>(string operation,
        Func<DbConnection, DbTransaction, LabelStoreState, Task<T>> action, CancellationToken token)
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
                    await using var connectionDisposal = connection.ConfigureAwait(false);
                    await connection.OpenAsync(token).ConfigureAwait(false);
                    await PrepareAsync(connection, token).ConfigureAwait(false);
                    DbTransaction transaction = await BeginAsync(connection, token).ConfigureAwait(false);
                    await using var transactionDisposal = transaction.ConfigureAwait(false);
                    LabelStoreState state = await StateAsync(connection, transaction, token).ConfigureAwait(false);
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
                    StoreLog.Retry(logger, operation, Provider, attempt);
                    await Task.Delay(TimeSpan.FromMilliseconds(25 * attempt), token).ConfigureAwait(false);
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
            StoreLog.Failed(logger, operation, Provider, outcome);
            throw;
        }
        catch (InvalidDataException)
        {
            outcome = "integrity";
            StoreLog.Failed(logger, operation, Provider, outcome);
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
            await using var connectionDisposal = connection.ConfigureAwait(false);
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
            await PrepareAsync(connection, cancellationToken).ConfigureAwait(false);
            await MigrateAsync(connection, cancellationToken).ConfigureAwait(false);
            DbTransaction transaction = await BeginAsync(connection, cancellationToken).ConfigureAwait(false);
            await using var transactionDisposal = transaction.ConfigureAwait(false);
            // The schema seeds the state row, so concurrent binders lock an existing identity.
            LabelStoreState state = await StateAsync(connection, transaction, cancellationToken, allowUnbound: true).ConfigureAwait(false);
            if (state.Issuer.Length == 0)
            {
                await ExecuteAsync(connection, transaction,
                    "UPDATE label_state SET issuer=@issuer, stream_id=@stream WHERE id=1", cancellationToken,
                    ("@issuer", issuer.Value), ("@stream", Guid.NewGuid().ToString("N"))).ConfigureAwait(false);
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
            StoreLog.Failed(logger, "initialize", Provider, "schema");
            throw;
        }
        finally
        {
            metrics.Stop("initialize", outcome, start);
        }
    }

    public Task<LabelStoreState> GetStateAsync(CancellationToken cancellationToken = default) =>
        RunAsync("state", (_, _, state) => Task.FromResult(state), cancellationToken);

    private static void Revision(LabelStoreState state, long? expected)
    {
        if (expected.HasValue && state.Revision != expected)
        {
            throw new InvalidOperationException("The label store revision changed; reload state before deciding again.");
        }
    }

    private static void Limit(int limit)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(limit, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(limit, 1000);
    }

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

    private static void OperationId(string operationId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(operationId);
        if (operationId.Contains('\0', StringComparison.Ordinal))
        {
            throw new ArgumentException("An operation key cannot contain U+0000.", nameof(operationId));
        }

        ArgumentOutOfRangeException.ThrowIfGreaterThan(s_strictUtf8.GetByteCount(operationId), 256);
    }

    private static byte[] OperationHash(string operationId) => SHA256.HashData(Encoding.UTF8.GetBytes(operationId));

    private static async Task<(LabelAppendReceipt Receipt, byte[] Request)?> ReceiptAsync(
        DbConnection connection, DbTransaction transaction, string operationId, CancellationToken token)
    {
        DbCommand command = Command(connection, transaction,
            "SELECT operation_id, first_seq, last_seq, revision, request FROM label_operations WHERE operation_hash=@hash",
            ("@hash", OperationHash(operationId)));
        await using var commandDisposal = command.ConfigureAwait(false);
        DbDataReader reader = await command.ExecuteReaderAsync(token).ConfigureAwait(false);
        await using var readerDisposal = reader.ConfigureAwait(false);
        if (!await reader.ReadAsync(token).ConfigureAwait(false))
        {
            return null;
        }

        if (!string.Equals(reader.GetString(0), operationId, StringComparison.Ordinal))
        {
            throw new InvalidDataException("An operation identity hash collision was detected.");
        }

        return (new LabelAppendReceipt(operationId, reader.GetInt64(1), reader.GetInt64(2), reader.GetInt64(3)), (byte[])reader.GetValue(4));
    }

    public async Task<LabelAppendReceipt> AppendAsync(string operationId, IReadOnlyList<SignedLabelSnapshot> labels,
        long? expectedRevision = null, long? retirementThrough = null, CancellationToken cancellationToken = default)
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
        LabelAppendReceipt receipt = await RunAsync("append", async (connection, transaction, state) =>
        {
            inserted = false;
            var existing = await ReceiptAsync(connection, transaction, operationId, cancellationToken).ConfigureAwait(false);
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
            long now = Integer(await ScalarAsync(connection, transaction, ClockSql, cancellationToken).ConfigureAwait(false));
            for (int i = 0; i < batch.Length; i++)
            {
                Label label = batch[i].Label;
                if (!string.Equals(label.Source.Value, state.Issuer, StringComparison.Ordinal))
                {
                    throw new InvalidDataException("A signed label has the wrong issuer.");
                }

                await ExecuteAsync(connection, transaction,
                    "INSERT INTO label_entries(seq, signed_json, payload, format_version, queued_at) VALUES(@seq,@json,@payload,1,@now)",
                    cancellationToken, ("@seq", checked(state.IssuedThrough + i + 1)), ("@json", batch[i].GetJsonBytes()),
                    ("@payload", batch[i].GetCanonicalUnsignedBytes()), ("@now", now)).ConfigureAwait(false);
            }

            LabelStoreState next = state with
            {
                Revision = checked(state.Revision + 1),
                IssuedThrough = last,
                RetirementThrough = retirementThrough ?? state.RetirementThrough,
                Phase = retirementThrough == state.RetirementUpperBound && retirementThrough.HasValue
                    ? LabelerLifecyclePhase.Distributing : state.Phase
            };
            await SaveAsync(connection, transaction, next, cancellationToken).ConfigureAwait(false);
            LabelAppendReceipt result = new(operationId, batch.Length == 0 ? 0 : checked(state.IssuedThrough + 1),
                batch.Length == 0 ? 0 : last, next.Revision);
            await ExecuteAsync(connection, transaction,
                "INSERT INTO label_operations(operation_hash, operation_id, first_seq, last_seq, revision, request) VALUES(@hash,@id,@first,@last,@revision,@request)",
                cancellationToken, ("@hash", OperationHash(operationId)), ("@id", operationId), ("@first", result.FirstSequence),
                ("@last", result.LastSequence), ("@revision", result.Revision), ("@request", request)).ConfigureAwait(false);
            inserted = true;
            return result;
        }, cancellationToken).ConfigureAwait(false);
        if (inserted)
        {
            metrics.Labels("append", batch.Length, metrics.BytesEnabled ? batch.Sum(label => (long)label.GetJsonBytes().Length) : 0);
        }

        return receipt;
    }

    public Task<LabelAppendReceipt?> GetReceiptAsync(string operationId, CancellationToken cancellationToken = default)
    {
        OperationId(operationId);
        return RunAsync<LabelAppendReceipt?>("receipt", async (connection, transaction, _) =>
            (await ReceiptAsync(connection, transaction, operationId, cancellationToken).ConfigureAwait(false))?.Receipt, cancellationToken);
    }

    public async Task<LabelStoreState> PublishAsync(int limit = 100, CancellationToken cancellationToken = default)
    {
        Limit(limit);
        long count = 0;
        LabelStoreState result = await RunAsync("publish", async (connection, transaction, state) =>
        {
            count = Math.Min(limit, state.IssuedThrough - state.PublishedThrough);
            if (count == 0)
            {
                return state;
            }

            LabelStoreState next = state with
            {
                Revision = checked(state.Revision + 1), PublishedThrough = checked(state.PublishedThrough + count)
            };
            await SaveAsync(connection, transaction, next, cancellationToken).ConfigureAwait(false);
            return next;
        }, cancellationToken).ConfigureAwait(false);
        metrics.Labels("publish", count, 0);
        return result;
    }

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

    private static string Cursor(Guid stream, long tip, long last, string fingerprint) =>
        Convert.ToBase64String(Encoding.UTF8.GetBytes(string.Create(CultureInfo.InvariantCulture, $"1|{stream:N}|{tip}|{last}|{fingerprint}")));

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

    private static void Boundaries(LabelStoreState state, long after, long through)
    {
        if (after < state.ReplayFloor || through < after || through > state.PublishedThrough)
        {
            throw new InvalidDataException("The replay boundaries are pruned, reversed or beyond the published tip.");
        }
    }

    private static async Task<LabelPage> PageAsync(DbConnection connection, DbTransaction transaction, LabelStoreState state,
        long after, long through, int limit, string[] patterns, Did? source, string? fingerprint, CancellationToken token)
    {
        List<StoredLabel> labels = [];
        long scanned = after;
        while (scanned < through && labels.Count < limit)
        {
            int scanLimit = patterns.Length == 0 && source is null ? limit - labels.Count : 1000;
            DbCommand command = Command(connection, transaction,
                "SELECT seq, signed_json, payload, format_version FROM label_entries WHERE seq>@after AND seq<=@through ORDER BY seq LIMIT @limit",
                ("@after", scanned), ("@through", through), ("@limit", scanLimit));
            await using var commandDisposal = command.ConfigureAwait(false);
            DbDataReader reader = await command.ExecuteReaderAsync(token).ConfigureAwait(false);
            await using var readerDisposal = reader.ConfigureAwait(false);
            bool found = false;
            while (labels.Count < limit && await reader.ReadAsync(token).ConfigureAwait(false))
            {
                found = true;
                scanned = reader.GetInt64(0);
                SignedLabelSnapshot signed = Restore(reader);
                Label label = signed.Label;
                bool matches = patterns.Length == 0 || patterns.Any(pattern => pattern.EndsWith('*')
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

        return new LabelPage(state.StreamId, through, labels.AsReadOnly(),
            fingerprint is not null && scanned < through ? Cursor(state.StreamId, through, scanned, fingerprint) : null);
    }

    public async Task<LabelPage> QueryAsync(IReadOnlyList<string> uriPatterns, Did? source = null, string? cursor = null,
        int limit = 100, CancellationToken cancellationToken = default)
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
        LabelPage page = await RunAsync("query", (connection, transaction, state) =>
        {
            (long tip, long last) = cursor is null ? (state.PublishedThrough, state.ReplayFloor) : ReadCursor(cursor, state, fingerprint);
            return PageAsync(connection, transaction, state, last, tip, limit, patterns, source, fingerprint, cancellationToken);
        }, cancellationToken).ConfigureAwait(false);
        metrics.Labels("query", page.Labels.Count, metrics.BytesEnabled ? page.Labels.Sum(label => (long)label.SignedLabel.GetJsonBytes().Length) : 0);
        return page;
    }

    public async Task<LabelPage> ReplayAsync(Guid streamId, long after, long through, int limit = 100,
        CancellationToken cancellationToken = default)
    {
        Limit(limit);
        LabelPage page = await RunAsync("replay", (connection, transaction, state) =>
        {
            if (streamId != state.StreamId)
            {
                throw new InvalidDataException("The replay stream identity differs.");
            }

            Boundaries(state, after, through);
            return PageAsync(connection, transaction, state, after, through, limit, [], null, null, cancellationToken);
        }, cancellationToken).ConfigureAwait(false);
        metrics.Labels("replay", page.Labels.Count, metrics.BytesEnabled ? page.Labels.Sum(label => (long)label.SignedLabel.GetJsonBytes().Length) : 0);
        return page;
    }

    private async Task<LabelStoreState> TransitionAsync(string operation, long expectedRevision,
        Func<LabelStoreState, long, LabelStoreState> transition, CancellationToken token)
    {
        LabelStoreState result = await RunAsync(operation, async (connection, transaction, state) =>
        {
            Revision(state, expectedRevision);
            long now = Integer(await ScalarAsync(connection, transaction, ClockSql, token).ConfigureAwait(false));
            LabelStoreState next = transition(state, now) with { Revision = checked(state.Revision + 1) };
            await SaveAsync(connection, transaction, next, token).ConfigureAwait(false);
            return next;
        }, token).ConfigureAwait(false);
        StoreLog.Transition(logger, result.Phase, result.Revision);
        return result;
    }

    public Task<LabelStoreState> BeginRetirementAsync(long expectedRevision, CancellationToken cancellationToken = default) =>
        TransitionAsync("retirement", expectedRevision, (state, _) =>
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
        }, cancellationToken);

    public Task<LabelStoreState> BeginGracePeriodAsync(long expectedRevision, TimeSpan gracePeriod, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(gracePeriod, TimeSpan.Zero);
        return TransitionAsync("grace", expectedRevision, (state, now) =>
            state.Phase == LabelerLifecyclePhase.Distributing && state.IssuedThrough == state.PublishedThrough
                ? state with
                {
                    Phase = LabelerLifecyclePhase.Grace,
                    // The clock sample is truncated to milliseconds; include that quantization interval.
                    GraceDeadline = DateTimeOffset.FromUnixTimeMilliseconds(checked(now + (long)Math.Ceiling(gracePeriod.TotalMilliseconds) + 1))
                }
                : throw new InvalidOperationException("Retirement enumeration and publication must finish before grace begins."), cancellationToken);
    }

    public Task<LabelStoreState> CompleteRetirementAsync(long expectedRevision, CancellationToken cancellationToken = default) =>
        TransitionAsync("retired", expectedRevision, (state, now) =>
            state.Phase == LabelerLifecyclePhase.Grace && state.GraceDeadline <= DateTimeOffset.FromUnixTimeMilliseconds(now)
                ? state with { Phase = LabelerLifecyclePhase.Retired }
                : throw new InvalidOperationException("The distribution grace deadline has not completed."), cancellationToken);

    public Task<LabelStoreState> PurgeAsync(long expectedRevision, bool confirmed, int limit = 100, CancellationToken cancellationToken = default)
    {
        Limit(limit);
        if (!confirmed)
        {
            throw new ArgumentException("Local purge requires explicit confirmation.", nameof(confirmed));
        }

        return RunAsync("purge", async (connection, transaction, state) =>
        {
            Revision(state, expectedRevision);
            if (state.Phase is not (LabelerLifecyclePhase.Retired or LabelerLifecyclePhase.Purging))
            {
                throw new InvalidOperationException("Only a completely retired labeler can be purged.");
            }

            long nextFloor = state.ReplayFloor + Math.Min(limit, state.IssuedThrough - state.ReplayFloor);
            await ExecuteAsync(connection, transaction, "DELETE FROM label_entries WHERE seq<=@floor",
                cancellationToken, ("@floor", nextFloor)).ConfigureAwait(false);
            // Receipts are bounded separately, including zero-label retirement checkpoint receipts.
            DbCommand keys = Command(connection, transaction,
                "SELECT operation_hash FROM label_operations ORDER BY revision LIMIT @limit", ("@limit", limit));
            await using var keysDisposal = keys.ConfigureAwait(false);
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
                await ExecuteAsync(connection, transaction, "DELETE FROM label_operations WHERE operation_hash=@hash",
                    cancellationToken, ("@hash", hash)).ConfigureAwait(false);
            }

            object? remaining = await ScalarAsync(connection, transaction,
                "SELECT 1 FROM label_operations LIMIT 1", cancellationToken).ConfigureAwait(false);
            LabelStoreState next = state with
            {
                Revision = checked(state.Revision + 1), ReplayFloor = nextFloor,
                Phase = nextFloor == state.IssuedThrough && remaining is null ? LabelerLifecyclePhase.Purged : LabelerLifecyclePhase.Purging
            };
            await SaveAsync(connection, transaction, next, cancellationToken).ConfigureAwait(false);
            return next;
        }, cancellationToken);
    }

    public async Task<LabelStoreSnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default)
    {
        LabelStoreSnapshot snapshot = await RunAsync("snapshot", async (connection, transaction, state) =>
        {
            object? oldest = await ScalarAsync(connection, transaction,
                "SELECT MIN(queued_at) FROM label_entries WHERE seq>@published", cancellationToken,
                ("@published", state.PublishedThrough)).ConfigureAwait(false);
            long now = Integer(await ScalarAsync(connection, transaction, ClockSql, cancellationToken).ConfigureAwait(false));
            return new LabelStoreSnapshot(state, state.IssuedThrough - state.PublishedThrough,
                oldest is null ? TimeSpan.Zero : TimeSpan.FromMilliseconds(Math.Max(0, now - Integer(oldest))));
        }, cancellationToken).ConfigureAwait(false);
        metrics.Snapshot(snapshot);
        return snapshot;
    }
}
