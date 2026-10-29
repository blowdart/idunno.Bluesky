// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Data.Common;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

using idunno.AtProto;
using idunno.Bluesky.Labeler.Administration;
using idunno.Bluesky.Labeler.Moderation;

namespace idunno.Bluesky.Labeler.Storage;

internal abstract partial class RelationalLabelStore
{
    /// <summary>Accepts a protocol report while retaining authenticated submissions that cannot yet be scoped.</summary>
    public Task<LabelServiceResult<LabelReportIntakeReceipt>> AcceptReportAsync(
        ModerationReportAuthenticationResult authentication,
        LabelReportSubmission submission,
        long? expectedPolicyRevision,
        int maximumPendingReports,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(authentication);
        ArgumentNullException.ThrowIfNull(submission);
        ArgumentOutOfRangeException.ThrowIfLessThan(maximumPendingReports, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(maximumPendingReports, 1_000_000);
        if (expectedPolicyRevision is { } revision)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(revision);
        }

        return AdministrationAsync(async (connection, transaction, state) =>
        {
            long now = await DatabaseTimeAsync(connection, transaction, cancellationToken).ConfigureAwait(false);
            LabelServiceResult<LabelReportIntakeReceipt> result;
            if (state.Issuer != authentication.Labeler.Value ||
                authentication.ExpiresAt.AddSeconds(5).ToUnixTimeMilliseconds() <= now)
            {
                result = CaseFailure<LabelReportIntakeReceipt>(
                    LabelServiceErrorCode.Unauthorized,
                    "The reporting token is expired or addressed to another labeler.");
            }
            else
            {
                ModerationPolicyGate? gate = await ReadPolicyGateAsync(connection, transaction, cancellationToken).ConfigureAwait(false);
                bool verified = gate is not null &&
                    gate.Revision == expectedPolicyRevision &&
                    gate.Phase == LabelPolicyPhase.VerifiedActive &&
                    gate.VerifiedUntil is { } verifiedUntil &&
                    verifiedUntil > now &&
                    now >= verifiedUntil - gate.FreshnessMilliseconds &&
                    gate.Policy is not null;
                if (verified)
                {
                    LabelServiceResult<LabelModerationReport> accepted = await CommitReportAsync(
                        connection: connection,
                        transaction: transaction,
                        authentication: authentication,
                        submission: submission,
                        expectedPolicyRevision: expectedPolicyRevision,
                        state: state,
                        now: now,
                        token: cancellationToken).ConfigureAwait(false);
                    result = accepted.Succeeded
                        ? LabelServiceResult<LabelReportIntakeReceipt>.Success(
                            new(accepted.Result.Id, accepted.Result.CreatedAt, pendingVerification: false))
                        : LabelServiceResult<LabelReportIntakeReceipt>.Failure(accepted.Error);
                }
                else
                {
                    result = await QueuePendingReportAsync(
                        connection: connection,
                        transaction: transaction,
                        authentication: authentication,
                        submission: submission,
                        maximumPendingReports: maximumPendingReports,
                        state: state,
                        now: now,
                        token: cancellationToken).ConfigureAwait(false);
                }
            }

            AdministrationState team = await ReadAdministrationAsync(connection, transaction, cancellationToken).ConfigureAwait(false);
            string auditReason;
            if (!result.Succeeded)
            {
                auditReason = result.Error.Message;
            }
            else if (result.Result.PendingVerification)
            {
                auditReason = "Report queued pending policy verification.";
            }
            else
            {
                auditReason = "Report accepted.";
            }

            await AuditAdministrationAsync(
                connection: connection,
                transaction: transaction,
                administration: team,
                actor: authentication.Reporter,
                localIdentity: null,
                operationId: result.Succeeded ? "report/" + result.Result.Id.ToString(CultureInfo.InvariantCulture) : string.Empty,
                action: "report-intake",
                reason: auditReason,
                succeeded: result.Succeeded,
                token: cancellationToken).ConfigureAwait(false);
            return result;
        }, cancellationToken);
    }

    /// <summary>Processes a bounded set of queued reports under the exact currently verified policy.</summary>
    public Task<LabelServiceResult<LabelPendingReportBatchResult>> ProcessPendingReportsAsync(
        long expectedPolicyRevision,
        int limit,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(expectedPolicyRevision);
        ArgumentOutOfRangeException.ThrowIfLessThan(limit, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(limit, 100);
        return AdministrationAsync(async (connection, transaction, state) =>
        {
            long now = await DatabaseTimeAsync(connection, transaction, cancellationToken).ConfigureAwait(false);
            ModerationPolicyGate? gate = await ReadPolicyGateAsync(connection, transaction, cancellationToken).ConfigureAwait(false);
            if (gate is null || gate.Revision != expectedPolicyRevision ||
                gate.Phase != LabelPolicyPhase.VerifiedActive ||
                gate.VerifiedUntil is not { } validUntil || validUntil <= now ||
                now < validUntil - gate.FreshnessMilliseconds || gate.Policy is null)
            {
                return CaseFailure<LabelPendingReportBatchResult>(
                    LabelServiceErrorCode.PolicyUnavailable,
                    "The expected policy revision is no longer independently verified.");
            }

            DbCommand command = Command(
                connection: connection,
                transaction: transaction,
                sql: "SELECT report_id,payload FROM label_pending_reports WHERE pending=TRUE ORDER BY report_id LIMIT @limit",
                ("@limit", limit));
            List<(long Id, PendingModerationReportData Data)> page = [];
            await using (command.ConfigureAwait(false))
            {
                DbDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                await using (reader.ConfigureAwait(false))
                {
                    while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                    {
                        long id = Convert.ToInt64(reader.GetValue(0), CultureInfo.InvariantCulture);
                        PendingModerationReportData data = DecodePendingReport((byte[])reader.GetValue(1));
                        if (id != data.Id)
                        {
                            throw new InvalidDataException("The pending report identifier differs from its payload.");
                        }
                        page.Add((id, data));
                    }
                }
            }

            AdministrationState team = await ReadAdministrationAsync(connection, transaction, cancellationToken).ConfigureAwait(false);
            LabelReportScope scope = gate.Policy.ToPolicy().ReportScope;
            int processed = 0;
            foreach ((long id, PendingModerationReportData pending) in page)
            {
                LabelReportSubmission submission = pending.ToPendingReport().Submission;
                bool subjectAllowed = scope.SubjectTypes.Contains(submission.Subject.SubjectType, StringComparer.Ordinal) &&
                    (submission.Subject.RecordUri is not { } recordUri ||
                        scope.SubjectCollections is not { } collections ||
                        collections.Contains(recordUri.Collection!));
                if (!subjectAllowed)
                {
                    PendingModerationReportData closed = pending with
                    {
                        PendingVerification = false,
                        PolicyVersion = gate.Policy.Version,
                        ProcessedAt = DateTimeOffset.FromUnixTimeMilliseconds(now),
                        ResolutionReason = "Not Applicable"
                    };
                    await ExecuteAsync(
                        connection: connection,
                        transaction: transaction,
                        sql: "UPDATE label_pending_reports SET pending=FALSE,payload=@payload WHERE report_id=@id AND pending=TRUE",
                        token: cancellationToken,
                        ("@payload", JsonSerializer.SerializeToUtf8Bytes(closed, ModerationJsonContext.Default.PendingModerationReportData)),
                        ("@id", id)).ConfigureAwait(false);
                    await AuditAdministrationAsync(
                        connection: connection,
                        transaction: transaction,
                        administration: team,
                        actor: state.Issuer,
                        localIdentity: "system:policy-scope",
                        operationId: "report-scope/" + id.ToString(CultureInfo.InvariantCulture),
                        action: "report-policy-not-applicable",
                        reason: "The active policy does not allow this report subject.",
                        succeeded: true,
                        token: cancellationToken).ConfigureAwait(false);
                    processed++;
                    continue;
                }

                LabelServiceResult<LabelModerationReport> promoted = await CommitReportAsync(
                    connection: connection,
                    transaction: transaction,
                    authentication: null,
                    submission: submission,
                    expectedPolicyRevision: expectedPolicyRevision,
                    state: state,
                    now: now,
                    token: cancellationToken,
                    pending: pending,
                    assignedReportId: id).ConfigureAwait(false);
                if (!promoted.Succeeded)
                {
                    return LabelServiceResult<LabelPendingReportBatchResult>.Failure(promoted.Error);
                }

                await ExecuteAsync(
                    connection: connection,
                    transaction: transaction,
                    sql: "DELETE FROM label_pending_reports WHERE report_id=@id AND pending=TRUE",
                    token: cancellationToken,
                    ("@id", id)).ConfigureAwait(false);
                await AuditAdministrationAsync(
                    connection: connection,
                    transaction: transaction,
                    administration: team,
                    actor: state.Issuer,
                    localIdentity: "system:policy-verification",
                    operationId: "report-promotion/" + id.ToString(CultureInfo.InvariantCulture),
                    action: "report-policy-verified",
                    reason: "Report passed the active policy subject-scope check and entered ordinary case review.",
                    succeeded: true,
                    token: cancellationToken).ConfigureAwait(false);
                processed++;
            }

            bool hasMore = page.Count == limit &&
                Integer(await ScalarAsync(
                    connection: connection,
                    transaction: transaction,
                    sql: "SELECT COUNT(*) FROM label_pending_reports WHERE pending=TRUE",
                    token: cancellationToken).ConfigureAwait(false)) > 0;
            return LabelServiceResult<LabelPendingReportBatchResult>.Success(new(processed, hasMore));
        }, cancellationToken);
    }

    public Task<LabelServiceResult<LabelModerationReport>> SubmitReportAsync(
        ModerationReportAuthenticationResult authentication,
        LabelReportSubmission submission,
        long? expectedPolicyRevision,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(authentication);
        ArgumentNullException.ThrowIfNull(submission);
        if (expectedPolicyRevision is { } revision)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(revision);
        }
        return AdministrationAsync(async (connection, transaction, state) =>
        {
            long now = await DatabaseTimeAsync(connection, transaction, cancellationToken).ConfigureAwait(false);
            LabelServiceResult<LabelModerationReport> result;
            if (state.Issuer != authentication.Labeler.Value || authentication.ExpiresAt.AddSeconds(5).ToUnixTimeMilliseconds() <= now)
            {
                result = CaseFailure<LabelModerationReport>(LabelServiceErrorCode.Unauthorized, "The reporting token is expired or addressed to another labeler.");
            }
            else
            {
                result = await CommitReportAsync(
                    connection: connection, transaction: transaction, authentication: authentication,
                    submission: submission, expectedPolicyRevision: expectedPolicyRevision, state: state, now: now, token: cancellationToken).ConfigureAwait(false);
            }

            AdministrationState team = await ReadAdministrationAsync(connection, transaction, cancellationToken).ConfigureAwait(false);
            await AuditAdministrationAsync(
                connection: connection, transaction: transaction, administration: team, actor: authentication.Reporter,
                localIdentity: null, operationId: result.Succeeded ? "report/" + result.Result.Id.ToString(CultureInfo.InvariantCulture) : string.Empty,
                action: "report-intake", reason: result.Succeeded ? "Report accepted." : "Report intake rejected.",
                succeeded: result.Succeeded, token: cancellationToken).ConfigureAwait(false);
            return result;
        }, cancellationToken);
    }

    /// <summary>Persists report content, replay protection and bounded appeal evidence while verification is unavailable.</summary>
    private static async Task<LabelServiceResult<LabelReportIntakeReceipt>> QueuePendingReportAsync(
        DbConnection connection,
        DbTransaction transaction,
        ModerationReportAuthenticationResult authentication,
        LabelReportSubmission submission,
        int maximumPendingReports,
        LabelStoreState state,
        long now,
        CancellationToken token)
    {
        bool appeal = submission.ReasonType is "com.atproto.moderation.defs#reasonAppeal" or "tools.ozone.report.defs#reasonAppeal";
        if (appeal && authentication.Reporter != submission.Subject.Repository)
        {
            return CaseFailure<LabelReportIntakeReceipt>(
                LabelServiceErrorCode.Unauthorized,
                "Only the affected account may submit an external appeal.");
        }

        byte[] replayKey = Encoding.UTF8.GetBytes(authentication.Reporter.Value + "\n" + authentication.TokenId);
        byte[] replayHash = SHA256.HashData(replayKey);
        byte[] tokenHash = Convert.FromHexString(authentication.TokenHash);
        await ExecuteAsync(
            connection: connection,
            transaction: transaction,
            sql: "DELETE FROM label_report_tokens WHERE expires_ms<=@now",
            token: token,
            ("@now", now)).ConfigureAwait(false);
        long used = Integer(await ScalarAsync(
            connection: connection,
            transaction: transaction,
            sql: "SELECT COUNT(*) FROM label_report_tokens WHERE replay_hash=@replay OR token_hash=@token",
            token: token,
            ("@replay", replayHash),
            ("@token", tokenHash)).ConfigureAwait(false));
        if (used != 0)
        {
            return CaseFailure<LabelReportIntakeReceipt>(
                LabelServiceErrorCode.Unauthorized,
                "The reporting token has already been used.");
        }

        long count = Integer(await ScalarAsync(
            connection: connection,
            transaction: transaction,
            sql: "SELECT COUNT(*) FROM label_pending_reports WHERE pending=TRUE",
            token: token).ConfigureAwait(false));
        if (count >= maximumPendingReports)
        {
            return CaseFailure<LabelReportIntakeReceipt>(
                LabelServiceErrorCode.PendingReportQueueFull,
                "The pending report queue is full. Retry after policy verification resumes.");
        }

        StoredLabel[]? challenges = null;
        if (appeal)
        {
            LabelServiceResult<StoredLabel[]> capture = await ReadAppealChallengesAsync(
                connection: connection,
                transaction: transaction,
                state: state,
                subject: submission.Subject,
                now: now,
                token: token).ConfigureAwait(false);
            if (!capture.Succeeded)
            {
                return LabelServiceResult<LabelReportIntakeReceipt>.Failure(capture.Error);
            }

            challenges = capture.Result;
        }

        long id = checked(Integer(await ScalarAsync(
            connection: connection,
            transaction: transaction,
            sql: "SELECT COALESCE(MAX(report_id),0) FROM (SELECT report_id FROM label_reports UNION ALL SELECT report_id FROM label_pending_reports) report_ids",
            token: token).ConfigureAwait(false)) + 1);
        DateTimeOffset createdAt = DateTimeOffset.FromUnixTimeMilliseconds(now);
        PendingModerationReportData pending = new(
            Id: id,
            Subject: submission.Subject.Subject,
            Cid: submission.Subject.Cid?.ToString(),
            ReasonType: submission.ReasonType,
            Reason: submission.Reason,
            ToolName: submission.ModToolName,
            ToolMetadata: submission.ModToolMetadata?.GetRawText(),
            Reporter: authentication.Reporter.Value,
            CreatedAt: createdAt,
            Audience: authentication.Audience,
            TokenHash: authentication.TokenHash,
            ExpiresAt: authentication.ExpiresAt,
            AppealChallenges: challenges?.Select(challenge => new ModerationAppealChallenge(
                challenge.Sequence, challenge.SignedLabel.GetJsonBytes())).ToArray());
        await ExecuteAsync(
            connection: connection,
            transaction: transaction,
            sql: "INSERT INTO label_pending_reports(report_id,pending,payload) VALUES(@id,TRUE,@payload)",
            token: token,
            ("@id", id),
            ("@payload", JsonSerializer.SerializeToUtf8Bytes(pending, ModerationJsonContext.Default.PendingModerationReportData))).ConfigureAwait(false);
        await ExecuteAsync(
            connection: connection,
            transaction: transaction,
            sql: "INSERT INTO label_report_tokens(replay_hash,replay_key,token_hash,expires_ms) VALUES(@hash,@key,@token,@expires)",
            token: token,
            ("@hash", replayHash),
            ("@key", replayKey),
            ("@token", tokenHash),
            ("@expires", authentication.ExpiresAt.AddSeconds(5).ToUnixTimeMilliseconds())).ConfigureAwait(false);
        return LabelServiceResult<LabelReportIntakeReceipt>.Success(new(id, createdAt, pendingVerification: true));
    }

    /// <summary>Commits a scoped, single-use reporter submission without team authority or subject retrieval.</summary>
    private async Task<LabelServiceResult<LabelModerationReport>> CommitReportAsync(
        DbConnection connection,
        DbTransaction transaction,
        ModerationReportAuthenticationResult? authentication,
        LabelReportSubmission submission,
        long? expectedPolicyRevision,
        LabelStoreState state,
        long now,
        CancellationToken token,
        PendingModerationReportData? pending = null,
        long? assignedReportId = null)
    {
        if ((authentication is null) == (pending is null))
        {
            throw new InvalidOperationException("Report promotion requires exactly one authentication source.");
        }

        ModerationPolicyGate? gate = await ReadPolicyGateAsync(connection, transaction, token).ConfigureAwait(false);
        if (gate is null || gate.Revision != expectedPolicyRevision || gate.Phase != LabelPolicyPhase.VerifiedActive ||
            gate.VerifiedUntil is null || gate.VerifiedUntil <= now ||
            now < gate.VerifiedUntil - gate.FreshnessMilliseconds || gate.Policy is null)
        {
            return CaseFailure<LabelModerationReport>(LabelServiceErrorCode.PolicyUnavailable, "The verified report policy changed or expired.");
        }

        LabelReportScope scope = gate.Policy.ToPolicy().ReportScope;
        if (!scope.SubjectTypes.Contains(submission.Subject.SubjectType, StringComparer.Ordinal) ||
            submission.Subject.RecordUri is { } recordUri && scope.SubjectCollections is { } collections &&
            !collections.Contains(recordUri.Collection!))
        {
            return CaseFailure<LabelModerationReport>(LabelServiceErrorCode.TargetNotAllowed, "The declaration does not accept this report scope.");
        }

        bool notApplicable = scope.ReasonTypes is { } reasons && !reasons.Contains(submission.ReasonType, StringComparer.Ordinal);
        bool appeal = submission.ReasonType is "com.atproto.moderation.defs#reasonAppeal" or "tools.ozone.report.defs#reasonAppeal";
        Did reporter = authentication?.Reporter ?? new(pending!.Reporter);
        string audience = authentication?.Audience ?? pending!.Audience;
        string tokenHashValue = authentication?.TokenHash ?? pending!.TokenHash;
        if (appeal && reporter != submission.Subject.Repository)
        {
            return CaseFailure<LabelModerationReport>(LabelServiceErrorCode.Unauthorized, "Only the affected account may submit an external appeal.");
        }

        byte[] tokenHash = Convert.FromHexString(tokenHashValue);
        byte[]? replayKey = null;
        byte[]? replayHash = null;
        if (pending is null)
        {
            replayKey = Encoding.UTF8.GetBytes(authentication!.Reporter.Value + "\n" + authentication.TokenId);
            replayHash = SHA256.HashData(replayKey);
            await ExecuteAsync(
                connection: connection, transaction: transaction,
                sql: "DELETE FROM label_report_tokens WHERE expires_ms<=@now", token: token, ("@now", now)).ConfigureAwait(false);
            long used = Integer(await ScalarAsync(
                connection: connection, transaction: transaction,
                sql: "SELECT COUNT(*) FROM label_report_tokens WHERE replay_hash=@replay OR token_hash=@token",
                token: token, ("@replay", replayHash), ("@token", tokenHash)).ConfigureAwait(false));
            if (used != 0)
            {
                return CaseFailure<LabelModerationReport>(LabelServiceErrorCode.Unauthorized, "The reporting token has already been used.");
            }
        }

        StoredLabel[]? challenges = null;
        if (appeal && !notApplicable)
        {
            if (pending?.AppealChallenges is { } savedChallenges)
            {
                challenges = [.. savedChallenges.Select(challenge => new StoredLabel(challenge.Sequence, new(challenge.SignedJson)))];
            }
            else
            {
                LabelServiceResult<StoredLabel[]> capture = await ReadAppealChallengesAsync(
                    connection: connection,
                    transaction: transaction,
                    state: state,
                    subject: submission.Subject,
                    now: now,
                    token: token).ConfigureAwait(false);
                if (!capture.Succeeded)
                {
                    return LabelServiceResult<LabelModerationReport>.Failure(capture.Error);
                }

                challenges = capture.Result;
            }
        }

        long id = assignedReportId ?? checked(Integer(await ScalarAsync(
            connection: connection,
            transaction: transaction,
            sql: "SELECT COALESCE(MAX(report_id),0) FROM (SELECT report_id FROM label_reports UNION ALL SELECT report_id FROM label_pending_reports) report_ids",
            token: token).ConfigureAwait(false)) + 1);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(id);
        DateTimeOffset timestamp = pending?.CreatedAt ?? DateTimeOffset.FromUnixTimeMilliseconds(now);
        ModerationCaseData? current = await OpenCaseAsync(connection, transaction, submission.Subject, token).ConfigureAwait(false);
        AdministrationState team = await ReadAdministrationAsync(connection, transaction, token).ConfigureAwait(false);
        long caseId = current?.Id ?? checked(Integer(await ScalarAsync(
            connection: connection, transaction: transaction, sql: "SELECT COALESCE(MAX(case_id),0) FROM label_cases",
            token: token).ConfigureAwait(false)) + 1);
        LabelCaseStatus status = notApplicable ? LabelCaseStatus.Resolved : current?.Status ?? LabelCaseStatus.Open;
        ModerationCaseData next = notApplicable && current is not null ? current : new(
            Id: caseId,
            Revision: checked((current?.Revision ?? 0) + 1),
            Subject: submission.Subject.Subject,
            Cid: submission.Subject.Cid?.ToString(),
            Status: status,
            Assignee: SoleModerator(team) ?? current?.Assignee,
            CreatedAt: current?.CreatedAt ?? timestamp,
            UpdatedAt: timestamp);
        if (!notApplicable || current is null)
        {
            await SaveCaseAsync(
                connection: connection, transaction: transaction, next: next, subject: submission.Subject,
                insert: current is null, token: token).ConfigureAwait(false);
        }
        string operation = "report/" + id.ToString(CultureInfo.InvariantCulture);
        ModerationReportData report = new(
            Id: id,
            CaseId: caseId,
            Subject: submission.Subject.Subject,
            Cid: submission.Subject.Cid?.ToString(),
            ReasonType: submission.ReasonType,
            Reason: submission.Reason,
            ToolName: submission.ModToolName,
            ToolMetadata: submission.ModToolMetadata?.GetRawText(),
            Reporter: reporter.Value,
            CreatedAt: timestamp,
            PolicyVersion: gate.Policy.Version,
            Audience: audience,
            TokenHash: tokenHashValue,
            DecisionOperationId: notApplicable ? operation : null,
            HandledAt: notApplicable ? timestamp : null,
            ResolutionReason: notApplicable ? "Not Applicable" : null);
        byte[] payload = JsonSerializer.SerializeToUtf8Bytes(report, ModerationJsonContext.Default.ModerationReportData);
        LabelCaseAction reportAction = (notApplicable, current) switch
        {
            (true, null) => LabelCaseAction.Resolve,
            (true, _) => LabelCaseAction.Note,
            _ => LabelCaseAction.Open
        };
        await AppendCaseEventAsync(
            connection: connection, transaction: transaction, next: next, operationId: operation,
            action: reportAction,
            actor: notApplicable ? state.Issuer : reporter,
            localIdentity: notApplicable ? "system:report-reason" : null,
            reason: notApplicable ? "Not Applicable" : "Report accepted.",
            intent: payload, operationNamespace: "report", token: token, createdAt: timestamp).ConfigureAwait(false);
        await ExecuteAsync(
            connection: connection, transaction: transaction,
            sql: "INSERT INTO label_reports(report_id,case_id,reason_hash,reason_type,payload) VALUES(@id,@case,@hash,@reason,@payload)",
            token: token,
            ("@id", id), ("@case", caseId), ("@hash", OperationHash(submission.ReasonType)),
            ("@reason", submission.ReasonType), ("@payload", payload)).ConfigureAwait(false);
        await IndexSearchTextAsync(
            connection: connection, transaction: transaction, kind: 0, id: id,
            content: report.Subject + "\n" + report.Reason, token: token).ConfigureAwait(false);
        if (notApplicable)
        {
            await AuditAdministrationAsync(
                connection: connection,
                transaction: transaction,
                administration: team,
                actor: state.Issuer,
                localIdentity: "system:report-reason",
                operationId: operation,
                action: "report-not-applicable",
                reason: "Not Applicable",
                succeeded: true,
                token: token).ConfigureAwait(false);
        }
        if (challenges is not null)
        {
            await CaptureReportAppealAsync(
                connection: connection,
                transaction: transaction,
                report: report,
                challenges: challenges,
                token: token).ConfigureAwait(false);
        }

        if (pending is null)
        {
            await ExecuteAsync(
                connection: connection, transaction: transaction,
                sql: "INSERT INTO label_report_tokens(replay_hash,replay_key,token_hash,expires_ms) VALUES(@hash,@key,@token,@expires)",
                token: token, ("@hash", replayHash!), ("@key", replayKey!), ("@token", tokenHash),
                ("@expires", authentication!.ExpiresAt.AddSeconds(5).ToUnixTimeMilliseconds())).ConfigureAwait(false);
        }
        return LabelServiceResult<LabelModerationReport>.Success(report.ToReport());
    }

    public Task<LabelServiceResult<LabelModerationReport>> GetReportAsync(
        LabelAdministrationCaller caller,
        long reportId,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(reportId);
        return ModerationAsync(
            caller: caller,
            permission: LabelAdministrationPermission.Inspect,
            operationId: string.Empty,
            reason: "Inspect a confidential report.",
            action: async (connection, transaction, _) =>
            {
                object? payload = await ScalarAsync(
                    connection: connection, transaction: transaction,
                    sql: "SELECT payload FROM label_reports WHERE report_id=@id",
                    token: cancellationToken, ("@id", reportId)).ConfigureAwait(false);
                return payload is byte[] bytes
                    ? LabelServiceResult<LabelModerationReport>.Success(DecodeReport(bytes).ToReport())
                    : CaseFailure<LabelModerationReport>(LabelServiceErrorCode.OperationNotFound, "The report was not found.");
            },
            token: cancellationToken,
            auditAction: "report-inspection");
    }

    public Task<LabelServiceResult<IReadOnlyList<LabelModerationReport>>> GetReportsAsync(
        LabelAdministrationCaller caller,
        long? caseId = null,
        long afterId = 0,
        int limit = 100,
        CancellationToken cancellationToken = default)
    {
        ValidateCasePage(afterId, limit);
        if (caseId is { } id)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(id);
        }

        return ModerationAsync<IReadOnlyList<LabelModerationReport>>(
            caller: caller,
            permission: LabelAdministrationPermission.Inspect,
            operationId: string.Empty,
            reason: "Inspect confidential report pages.",
            action: async (connection, transaction, _) =>
            {
                DbCommand command = Command(
                    connection: connection, transaction: transaction,
                    sql: "SELECT payload FROM label_reports WHERE report_id>@after" +
                        (caseId is null ? string.Empty : " AND case_id=@case") + " ORDER BY report_id LIMIT @limit",
                    ("@after", afterId), ("@case", caseId), ("@limit", limit));
                await using ConfiguredAsyncDisposable disposal = command.ConfigureAwait(false);
                DbDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                await using ConfiguredAsyncDisposable readerDisposal = reader.ConfigureAwait(false);
                List<LabelModerationReport> reports = [];
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    reports.Add(DecodeReport((byte[])reader.GetValue(0)).ToReport());
                }

                return LabelServiceResult<IReadOnlyList<LabelModerationReport>>.Success(reports.AsReadOnly());
            },
            token: cancellationToken,
            auditAction: "report-inspection");
    }

    /// <summary>Decodes required persisted report data without reflection or silent null recovery.</summary>
    private static ModerationReportData DecodeReport(byte[] bytes) =>
        JsonSerializer.Deserialize(bytes, ModerationJsonContext.Default.ModerationReportData)
            ?? throw new InvalidDataException("The report payload is null.");

    /// <summary>Decodes required pending report data without reflection or silent null recovery.</summary>
    private static PendingModerationReportData DecodePendingReport(byte[] bytes) =>
        JsonSerializer.Deserialize(bytes, ModerationJsonContext.Default.PendingModerationReportData)
            ?? throw new InvalidDataException("The pending report payload is null.");
}
