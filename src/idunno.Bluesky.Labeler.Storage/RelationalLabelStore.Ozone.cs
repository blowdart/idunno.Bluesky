// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Data.Common;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

using idunno.Bluesky.Labeler.Administration;
using idunno.Bluesky.Labeler.Issuance;
using idunno.Bluesky.Labeler.Moderation;

namespace idunno.Bluesky.Labeler.Storage;

internal abstract partial class RelationalLabelStore
{
    /// <inheritdoc/>
    public Task<LabelServiceResult<LabelOzoneEvent>> CommitOzoneEventAsync(
        LabelAdministrationCaller caller,
        LabelOzoneEventRequest request,
        long policyRevision,
        IReadOnlyList<LabelDecisionCommit> labels,
        CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfGreaterThan(request.Actions.Length, 16);
        request.Mute?.Validate();
        if (request.EscalateSubject &&
            (request.Mute is not null || labels.Count != 0 || request.ResolveSubject ||
                request.AllReports || request.ReportIds.Length != 0 || request.ReportTypes.Length != 0))
        {
            throw new ArgumentException("Escalation cannot change labels, report handling or mutes.", nameof(request));
        }
        if (request.Mute is not null &&
            (labels.Count != 0 || request.ResolveSubject || request.AllReports ||
                request.ReportIds.Length != 0 || request.ReportTypes.Length != 0))
        {
            throw new ArgumentException("A mute cannot change labels, report handling or review state.", nameof(request));
        }
        if (request.Actions.Length != labels.Count ||
            request.Actions.Select(action => action.Value).Distinct(StringComparer.Ordinal).Count() != labels.Count)
        {
            throw new ArgumentException("Every distinct action requires exactly one preparation.", nameof(labels));
        }
        return ModerationAsync(
            caller: caller,
            permission: LabelAdministrationPermission.Moderate,
            operationId: request.OperationId,
            reason: request.Reason,
            action: async (connection, transaction, _) =>
            {
                object? previous = await ScalarAsync(
                    connection: connection,
                    transaction: transaction,
                    sql: "SELECT payload FROM label_ozone_events WHERE operation_hash=@hash",
                    token: cancellationToken,
                    ("@hash", OperationHash(request.OperationId))).ConfigureAwait(false);
                if (previous is not null)
                {
                    return CaseFailure<LabelOzoneEvent>(
                        LabelServiceErrorCode.IdempotencyConflict,
                        request.ExternalId is null ? "This Ozone operation has already committed." : "Duplicate external event identifier.");
                }
                ModerationPolicyGate? gate = await ReadPolicyGateAsync(connection, transaction, cancellationToken).ConfigureAwait(false);
                if (gate is null || gate.Revision != policyRevision)
                {
                    return CaseFailure<LabelOzoneEvent>(LabelServiceErrorCode.PolicyUnavailable, "The policy changed during event preparation.");
                }
                LabelStoreState state = await StateAsync(connection, transaction, cancellationToken).ConfigureAwait(false);
                StoredLabel?[] observations = new StoredLabel?[labels.Count];
                for (int index = 0; index < labels.Count; index++)
                {
                    LabelDecisionCommit prepared = labels[index];
                    LabelCaseLabelAction action = request.Actions[index];
                    if (prepared.Request.Kind != action.Kind || prepared.Request.Value != action.Value ||
                        prepared.Request.ExpiresAt != action.ExpiresAt ||
                        prepared.ResolvedTarget.Subject != request.Subject.Subject)
                    {
                        throw new InvalidOperationException("Prepared labels must preserve the submitted event subject and actions.");
                    }
                    LabelServiceResult<LabelDecisionSnapshot> validated = await ValidatePreparedDecisionAsync(
                        connection: connection,
                        transaction: transaction,
                        state: state,
                        decision: prepared,
                        token: cancellationToken).ConfigureAwait(false);
                    if (!validated.Succeeded)
                    {
                        return LabelServiceResult<LabelOzoneEvent>.Failure(validated.Error);
                    }
                    observations[index] = validated.Result.CurrentLabel;
                    if (prepared.ResolvedTarget.Cid != request.Subject.Cid &&
                        (action.Kind != LabelOperationKind.Negate || prepared.ResolvedTarget.Cid is not null ||
                            observations[index]?.SignedLabel.Label.Cid is not null))
                    {
                        return CaseFailure<LabelOzoneEvent>(LabelServiceErrorCode.StaleDecision, "The label scope differs from the event subject.");
                    }
                }

                List<ModerationReportData> selected = [];
                List<ModerationCaseData> subjectCases = [];
                DbCommand casesCommand = Command(
                    connection: connection,
                    transaction: transaction,
                    sql: "SELECT payload FROM label_cases WHERE subject_key>=@start AND subject_key<@end ORDER BY case_id",
                    ("@start", Encoding.UTF8.GetBytes(request.Subject.Subject + "\n")),
                    ("@end", Encoding.UTF8.GetBytes(request.Subject.Subject + "\v")));
                await using (casesCommand.ConfigureAwait(false))
                {
                    DbDataReader reader = await casesCommand.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                    await using (reader.ConfigureAwait(false))
                    {
                        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                        {
                            subjectCases.Add(DecodeCase((byte[])reader.GetValue(0)));
                            if (subjectCases.Count > 1000)
                            {
                                return CaseFailure<LabelOzoneEvent>(LabelServiceErrorCode.StorageUnavailable, "The subject exceeds the bounded Ozone case transaction size.");
                            }
                        }
                    }
                }
                if (request.EscalateSubject && !subjectCases.Any(current =>
                    current.Status != LabelCaseStatus.Resolved &&
                    (current.Cid ?? current.RecordResolution?.Cid) == request.Subject.Cid?.ToString()))
                {
                    return CaseFailure<LabelOzoneEvent>(LabelServiceErrorCode.CaseConflict, "Escalation requires an unresolved case on the exact subject.");
                }
                foreach (ModerationCaseData subjectCase in subjectCases)
                {
                    long after = 0;
                    while (true)
                    {
                        List<ModerationReportData> page = [];
                        DbCommand reportsCommand = Command(
                            connection: connection,
                            transaction: transaction,
                            sql: "SELECT payload FROM label_reports WHERE case_id=@case AND report_id>@after ORDER BY report_id LIMIT 100",
                            ("@case", subjectCase.Id),
                            ("@after", after));
                        await using (reportsCommand.ConfigureAwait(false))
                        {
                            DbDataReader reader = await reportsCommand.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                            await using (reader.ConfigureAwait(false))
                            {
                                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                                {
                                    page.Add(DecodeReport((byte[])reader.GetValue(0)));
                                }
                            }
                        }
                        selected.AddRange(page.Where(report => report.DecisionOperationId is null &&
                            !(request.PreserveEscalatedReview && subjectCase.Status == LabelCaseStatus.Escalated) &&
                            (request.ReportIds.Contains(report.Id) ||
                                (request.Subject.Cid is null ||
                                    (report.Cid ?? report.RecordResolution?.Cid ?? subjectCase.RecordResolution?.Cid) == request.Subject.Cid.ToString()) &&
                                (request.AllReports || request.ReportTypes.Contains(report.ReasonType, StringComparer.Ordinal)) &&
                                (subjectCase.Assignee is null || request.IncludeAssignedReports))));
                        if (selected.Count > 1000)
                        {
                            return CaseFailure<LabelOzoneEvent>(LabelServiceErrorCode.StorageUnavailable, "An Ozone transaction may close at most 1000 reports.");
                        }
                        if (page.Count < 100)
                        {
                            break;
                        }
                        after = page[^1].Id;
                    }
                }
                if (request.ReportIds.Any(id => !selected.Any(report => report.Id == id)))
                {
                    return CaseFailure<LabelOzoneEvent>(LabelServiceErrorCode.CaseConflict, "Selected reports must be open on the submitted subject.");
                }

                long id = checked(Integer(await ScalarAsync(
                    connection: connection,
                    transaction: transaction,
                    sql: "SELECT COALESCE(MAX(event_id),0) FROM label_ozone_events",
                    token: cancellationToken).ConfigureAwait(false)) + 1);
                if (id > MaximumSequence / 1024)
                {
                    return CaseFailure<LabelOzoneEvent>(LabelServiceErrorCode.StorageUnavailable, "Ozone event identifiers are exhausted.");
                }
                for (int index = 0; index < labels.Count; index++)
                {
                    LabelOperationReceipt receipt = await ApplyPreparedDecisionAsync(
                        connection: connection,
                        transaction: transaction,
                        state: state,
                        decision: labels[index],
                        current: observations[index],
                        administration: null,
                        token: cancellationToken).ConfigureAwait(false);
                    state = receipt.StateAtCommit;
                }
                DateTimeOffset now = DateTimeOffset.FromUnixTimeMilliseconds(
                    await DatabaseTimeAsync(connection, transaction, cancellationToken).ConfigureAwait(false));
                if (request.Mute is { } mute)
                {
                    LabelServiceResult<LabelMuteChange> changed = await ApplyMuteAsync(
                        connection: connection,
                        transaction: transaction,
                        subject: request.Subject,
                        change: mute,
                        eventId: id,
                        now: now.ToUnixTimeMilliseconds(),
                        token: cancellationToken).ConfigureAwait(false);
                    if (!changed.Succeeded)
                    {
                        return LabelServiceResult<LabelOzoneEvent>.Failure(changed.Error);
                    }
                }
                foreach (ModerationReportData report in selected)
                {
                    ModerationReportData handled = report with
                    {
                        DecisionOperationId = request.OperationId,
                        HandledAt = now,
                        ResolutionReason = request.ReportNote ?? request.Reason
                    };
                    await SaveReportPayloadAsync(
                        connection: connection,
                        transaction: transaction,
                        report: handled,
                        token: cancellationToken).ConfigureAwait(false);
                    await UpdateReportSearchTextAsync(
                        connection,
                        transaction,
                        handled,
                        cancellationToken).ConfigureAwait(false);
                }
                LabelOzoneEvent result = new(
                    Id: id,
                    OperationId: request.OperationId,
                    Subject: request.Subject.Subject,
                    Cid: request.Subject.Cid?.ToString(),
                    Actor: caller.Actor.Value,
                    CreatedAt: now,
                    EventJson: request.EventJson,
                    ModToolJson: request.ModToolJson,
                    ReportIds: [.. selected.Select(report => report.Id)],
                    Automated: request.Automated,
                    ReportNote: request.ReportNote);
                byte[] payload = JsonSerializer.SerializeToUtf8Bytes(result, OzoneStoreJsonContext.Default.LabelOzoneEvent);
                if (request.Mute is not null)
                {
                    foreach (ModerationCaseData current in subjectCases)
                    {
                        await AppendCaseEventAsync(
                            connection: connection,
                            transaction: transaction,
                            next: current,
                            operationId: request.OperationId,
                            action: LabelCaseAction.Note,
                            actor: caller.Actor,
                            localIdentity: caller.LocalIdentity,
                            reason: request.Reason,
                            intent: payload,
                            operationNamespace: "ozone/" + current.Id.ToString(System.Globalization.CultureInfo.InvariantCulture),
                            token: cancellationToken,
                            createdAt: now).ConfigureAwait(false);
                    }
                }
                foreach (ModerationCaseData current in subjectCases.Where(current =>
                    selected.Any(report => report.CaseId == current.Id) ||
                    request.ResolveSubject && (request.Subject.Cid is null ||
                        (current.Cid ?? current.RecordResolution?.Cid) == request.Subject.Cid.ToString()) ||
                    labels.Count > 0 && (current.Cid ?? current.RecordResolution?.Cid) == request.Subject.Cid?.ToString() ||
                    request.EscalateSubject && current.Status != LabelCaseStatus.Resolved &&
                        (current.Cid ?? current.RecordResolution?.Cid) == request.Subject.Cid?.ToString()))
                {
                    bool pending = Integer(await ScalarAsync(
                        connection: connection,
                        transaction: transaction,
                        sql: "SELECT COUNT(*) FROM label_appeals WHERE case_id=@case AND status=@pending",
                        token: cancellationToken,
                        ("@case", current.Id),
                        ("@pending", (int)LabelAppealStatus.Pending)).ConfigureAwait(false)) > 0;
                    bool remaining = await HasOpenOzoneReportsAsync(
                        connection: connection,
                        transaction: transaction,
                        caseId: current.Id,
                        token: cancellationToken).ConfigureAwait(false);
                    LabelCaseStatus status = current.Status;
                    if (request.EscalateSubject)
                    {
                        status = LabelCaseStatus.Escalated;
                    }
                    else if (request.ResolveSubject && !pending && !remaining &&
                        !(request.PreserveEscalatedReview && current.Status == LabelCaseStatus.Escalated))
                    {
                        status = LabelCaseStatus.Resolved;
                    }
                    ModerationCaseData next = current with
                    {
                        Assignee = request.EscalateSubject ? current.Assignee : current.Assignee ?? caller.Actor.Value,
                        Revision = checked(current.Revision + 1),
                        UpdatedAt = now,
                        Status = status
                    };
                    await SaveCaseAsync(
                        connection: connection,
                        transaction: transaction,
                        next: next,
                        subject: current.ToCase().Subject,
                        insert: false,
                        token: cancellationToken).ConfigureAwait(false);
                    await AppendCaseEventAsync(
                        connection: connection,
                        transaction: transaction,
                        next: next,
                        operationId: request.OperationId,
                        action: request.EscalateSubject ? LabelCaseAction.Escalate : LabelCaseAction.Note,
                        actor: caller.Actor,
                        localIdentity: caller.LocalIdentity,
                        reason: request.Reason,
                        intent: payload,
                        operationNamespace: "ozone/" + current.Id.ToString(System.Globalization.CultureInfo.InvariantCulture),
                        token: cancellationToken).ConfigureAwait(false);
                }
                await ExecuteAsync(
                    connection: connection,
                    transaction: transaction,
                    sql: "INSERT INTO label_ozone_events(event_id,operation_hash,payload) VALUES(@id,@hash,@payload)",
                    token: cancellationToken,
                    ("@id", id),
                    ("@hash", OperationHash(request.OperationId)),
                    ("@payload", payload)).ConfigureAwait(false);
                return LabelServiceResult<LabelOzoneEvent>.Success(result);
            },
            token: cancellationToken,
            auditAction: "ozone-event");
    }

    /// <inheritdoc/>
    public Task<LabelServiceResult<IReadOnlyList<LabelOzoneEvent>>> ReadOzoneEventsAsync(
        LabelAdministrationCaller caller,
        long afterId,
        int limit,
        CancellationToken cancellationToken)
    {
        ValidateCasePage(afterId, limit);
        return ModerationAsync<IReadOnlyList<LabelOzoneEvent>>(
            caller: caller,
            permission: LabelAdministrationPermission.Inspect,
            operationId: string.Empty,
            reason: "Inspect Ozone moderation events.",
            action: async (connection, transaction, _) =>
            {
                DbCommand command = Command(
                    connection: connection,
                    transaction: transaction,
                    sql: "SELECT payload FROM label_ozone_events WHERE event_id>@after ORDER BY event_id LIMIT @limit",
                    ("@after", afterId),
                    ("@limit", limit));
                await using ConfiguredAsyncDisposable commandDisposal = command.ConfigureAwait(false);
                DbDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                await using ConfiguredAsyncDisposable readerDisposal = reader.ConfigureAwait(false);
                List<LabelOzoneEvent> events = [];
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    events.Add(JsonSerializer.Deserialize((byte[])reader.GetValue(0), OzoneStoreJsonContext.Default.LabelOzoneEvent)
                        ?? throw new InvalidDataException("The stored Ozone event is null."));
                }

                return LabelServiceResult<IReadOnlyList<LabelOzoneEvent>>.Success(events);
            },
            token: cancellationToken);
    }

    /// <inheritdoc/>
    public Task<LabelServiceResult<IReadOnlyList<LabelOzoneEvent>>> ReadOzoneOperationAsync(
        LabelAdministrationCaller caller,
        string operationId,
        CancellationToken cancellationToken) =>
        ModerationAsync<IReadOnlyList<LabelOzoneEvent>>(
            caller: caller,
            permission: LabelAdministrationPermission.Inspect,
            operationId: string.Empty,
            reason: "Inspect an Ozone operation.",
            action: async (connection, transaction, _) =>
            {
                object? payload = await ScalarAsync(
                    connection: connection,
                    transaction: transaction,
                    sql: "SELECT payload FROM label_ozone_events WHERE operation_hash=@hash",
                    token: cancellationToken,
                    ("@hash", OperationHash(operationId))).ConfigureAwait(false);
                LabelOzoneEvent[] found = payload is null ? [] :
                    [JsonSerializer.Deserialize((byte[])payload, OzoneStoreJsonContext.Default.LabelOzoneEvent)
                        ?? throw new InvalidDataException("The stored Ozone event is null.")];
                return LabelServiceResult<IReadOnlyList<LabelOzoneEvent>>.Success(found);
            },
            token: cancellationToken);

    /// <summary>Checks outstanding reports after selected closures without changing their original subjects.</summary>
    private static async Task<bool> HasOpenOzoneReportsAsync(
        DbConnection connection,
        DbTransaction transaction,
        long caseId,
        CancellationToken token)
    {
        DbCommand command = Command(
            connection: connection,
            transaction: transaction,
            sql: "SELECT payload FROM label_reports WHERE case_id=@case",
            ("@case", caseId));
        await using ConfiguredAsyncDisposable commandDisposal = command.ConfigureAwait(false);
        DbDataReader reader = await command.ExecuteReaderAsync(token).ConfigureAwait(false);
        await using ConfiguredAsyncDisposable readerDisposal = reader.ConfigureAwait(false);
        while (await reader.ReadAsync(token).ConfigureAwait(false))
        {
            if (DecodeReport((byte[])reader.GetValue(0)).DecisionOperationId is null)
            {
                return true;
            }
        }
        return false;
    }
}

/// <summary>Provides reflection-free persistence for immutable Ozone events.</summary>
[JsonSerializable(typeof(LabelOzoneEvent))]
internal sealed partial class OzoneStoreJsonContext : JsonSerializerContext;
