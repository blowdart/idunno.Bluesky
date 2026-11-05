// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Data.Common;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text.Json;

using idunno.Bluesky.Labeler.Administration;
using idunno.Bluesky.Labeler.Moderation;

namespace idunno.Bluesky.Labeler.Storage;

internal abstract partial class RelationalLabelStore
{
    /// <inheritdoc/>
    public Task<LabelServiceResult<LabelMuteState>> ReadMuteAsync(
        LabelAdministrationCaller caller,
        string subject,
        CancellationToken cancellationToken)
    {
        _ = ModerationSubjectEncoding.Decode(subject, null);
        return ModerationAsync(
            caller: caller,
            permission: LabelAdministrationPermission.Inspect,
            operationId: string.Empty,
            reason: "Inspect temporary moderation mutes.",
            action: async (connection, transaction, _) =>
            {
                long now = await DatabaseTimeAsync(connection, transaction, cancellationToken).ConfigureAwait(false);
                long? subjectUntil = await ReadMuteDeadlineAsync(
                    connection: connection, transaction: transaction, subject: subject,
                    kind: LabelMuteKind.Subject, now: now, token: cancellationToken).ConfigureAwait(false);
                long? reporterUntil = await ReadMuteDeadlineAsync(
                    connection: connection, transaction: transaction, subject: subject,
                    kind: LabelMuteKind.Reporter, now: now, token: cancellationToken).ConfigureAwait(false);
                return LabelServiceResult<LabelMuteState>.Success(new(
                    subjectUntil is { } muted ? DateTimeOffset.FromUnixTimeMilliseconds(muted) : null,
                    reporterUntil is { } reporting ? DateTimeOffset.FromUnixTimeMilliseconds(reporting) : null,
                    DateTimeOffset.FromUnixTimeMilliseconds(now)));
            },
            token: cancellationToken,
            auditAction: "inspect-mutes");
    }

    /// <inheritdoc/>
    public Task<LabelServiceResult<IReadOnlyList<LabelPendingModerationReport>>> ReadMutedReportsAsync(
        LabelAdministrationCaller caller,
        long afterId,
        int limit,
        long? reportId,
        CancellationToken cancellationToken)
    {
        ValidateCasePage(afterId, limit);
        if (reportId is <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(reportId));
        }
        return ModerationAsync<IReadOnlyList<LabelPendingModerationReport>>(
            caller: caller,
            permission: LabelAdministrationPermission.Inspect,
            operationId: string.Empty,
            reason: "Inspect retained submissions from muted reporters.",
            action: async (connection, transaction, _) =>
            {
                string muted = TableJsonField("ReporterMuted") + (Provider == "sqlite" ? "=1" : "='true'");
                DbCommand command = Command(
                    connection: connection,
                    transaction: transaction,
                    sql: $"SELECT t.payload FROM label_pending_reports t WHERE t.report_id>@after AND {muted}" +
                        (reportId is null ? string.Empty : " AND t.report_id=@id") +
                        " ORDER BY t.report_id LIMIT @limit",
                    ("@after", afterId), ("@id", reportId), ("@limit", limit));
                await using ConfiguredAsyncDisposable commandDisposal = command.ConfigureAwait(false);
                DbDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                await using ConfiguredAsyncDisposable readerDisposal = reader.ConfigureAwait(false);
                List<LabelPendingModerationReport> entries = [];
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    PendingModerationReportData retained = JsonSerializer.Deserialize(
                        (byte[])reader.GetValue(0), ModerationJsonContext.Default.PendingModerationReportData)
                        ?? throw new InvalidDataException("A retained muted submission is missing.");
                    entries.Add(retained.ToPendingReport());
                }
                return LabelServiceResult<IReadOnlyList<LabelPendingModerationReport>>.Success(entries);
            },
            token: cancellationToken,
            auditAction: "inspect-muted-reports");
    }

    /// <inheritdoc/>
    public Task<LabelServiceResult<IReadOnlyList<LabelMuteEntry>>> ReadMutesAsync(
        LabelAdministrationCaller caller,
        long afterEventId,
        int limit,
        CancellationToken cancellationToken)
    {
        ValidateCasePage(afterEventId, limit);
        return ModerationAsync<IReadOnlyList<LabelMuteEntry>>(
            caller: caller,
            permission: LabelAdministrationPermission.Inspect,
            operationId: string.Empty,
            reason: "Inspect active moderation mutes and their authors.",
            action: async (connection, transaction, _) =>
            {
                long now = await DatabaseTimeAsync(connection, transaction, cancellationToken).ConfigureAwait(false);
                DbCommand command = Command(
                    connection: connection,
                    transaction: transaction,
                    sql: "SELECT m.kind,m.until_ms,e.payload FROM label_mutes m JOIN label_ozone_events e ON e.event_id=m.event_id " +
                        "WHERE m.event_id>@after AND m.until_ms>@now ORDER BY m.event_id LIMIT @limit",
                    ("@after", afterEventId), ("@now", now), ("@limit", limit));
                await using ConfiguredAsyncDisposable commandDisposal = command.ConfigureAwait(false);
                DbDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                await using ConfiguredAsyncDisposable readerDisposal = reader.ConfigureAwait(false);
                List<LabelMuteEntry> entries = [];
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    LabelOzoneEvent provenance = JsonSerializer.Deserialize(
                        (byte[])reader.GetValue(2), OzoneStoreJsonContext.Default.LabelOzoneEvent)
                        ?? throw new InvalidDataException("A mute event is missing.");
                    entries.Add(new(
                        EventId: provenance.Id,
                        Subject: provenance.Subject,
                        Kind: (LabelMuteKind)Integer(reader.GetValue(0)),
                        Until: DateTimeOffset.FromUnixTimeMilliseconds(Integer(reader.GetValue(1))),
                        Actor: provenance.Actor,
                        CreatedAt: provenance.CreatedAt));
                }
                return LabelServiceResult<IReadOnlyList<LabelMuteEntry>>.Success(entries);
            },
            token: cancellationToken,
            auditAction: "inspect-mutes");
    }

    /// <summary>Reads a collision-checked active deadline under the issuer serialization lock.</summary>
    private static async Task<long?> ReadMuteDeadlineAsync(
        DbConnection connection,
        DbTransaction transaction,
        string subject,
        LabelMuteKind kind,
        long now,
        CancellationToken token)
    {
        byte[] key = s_strictUtf8.GetBytes(subject);
        object? deadline = await ScalarAsync(
            connection: connection,
            transaction: transaction,
            sql: "SELECT until_ms FROM label_mutes WHERE kind=@kind AND subject_hash=@hash AND subject_key=@key AND until_ms>@now",
            token: token,
            ("@kind", (int)kind), ("@hash", SHA256.HashData(key)), ("@key", key), ("@now", now)).ConfigureAwait(false);
        return deadline is null ? null : Integer(deadline);
    }

    /// <summary>Commits queue or reporter suppression in the same transaction as its immutable event and audit.</summary>
    private static async Task<LabelServiceResult<LabelMuteChange>> ApplyMuteAsync(
        DbConnection connection,
        DbTransaction transaction,
        LabelReportSubject subject,
        LabelMuteChange change,
        long eventId,
        long now,
        CancellationToken token)
    {
        change.Validate();
        if (change.Kind == LabelMuteKind.Reporter && subject.RecordUri is not null)
        {
            throw new ArgumentException("Only accounts can be muted from reporting.", nameof(subject));
        }
        byte[] key = s_strictUtf8.GetBytes(subject.Subject);
        byte[] hash = SHA256.HashData(key);
        object? currentEvent = await ScalarAsync(
            connection: connection, transaction: transaction,
            sql: "SELECT event_id FROM label_mutes WHERE kind=@kind AND subject_hash=@hash",
            token: token, ("@kind", (int)change.Kind), ("@hash", hash)).ConfigureAwait(false);
        if (change.ExpectedEventId is { } expected && expected != (currentEvent is null ? 0 : Integer(currentEvent)))
        {
            return CaseFailure<LabelMuteChange>(LabelServiceErrorCode.CaseConflict, "The mute changed. Reload the mute list before changing it.");
        }
        object? existing = await ScalarAsync(
            connection: connection, transaction: transaction,
            sql: "SELECT subject_key FROM label_mutes WHERE kind=@kind AND subject_hash=@hash",
            token: token, ("@kind", (int)change.Kind), ("@hash", hash)).ConfigureAwait(false);
        if (existing is byte[] stored && !stored.AsSpan().SequenceEqual(key))
        {
            throw new InvalidDataException("A moderation mute subject hash collision was detected.");
        }
        await ExecuteAsync(
            connection: connection, transaction: transaction,
            sql: "DELETE FROM label_mutes WHERE kind=@kind AND subject_hash=@hash",
            token: token, ("@kind", (int)change.Kind), ("@hash", hash)).ConfigureAwait(false);
        if (change.DurationInHours is { } hours)
        {
            await ExecuteAsync(
                connection: connection, transaction: transaction,
                sql: "INSERT INTO label_mutes(kind,event_id,subject_hash,subject_key,until_ms) VALUES(@kind,@event,@hash,@key,@until)",
                token: token,
                ("@kind", (int)change.Kind), ("@event", eventId), ("@hash", hash), ("@key", key),
                ("@until", checked(now + hours * 3_600_000L))).ConfigureAwait(false);
        }
        return LabelServiceResult<LabelMuteChange>.Success(change);
    }
}
