// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Data.Common;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;

using idunno.Bluesky.Labeler.Administration;
using idunno.Bluesky.Labeler.Moderation;

namespace idunno.Bluesky.Labeler.Storage;

internal abstract partial class RelationalLabelStore
{
    public Task<LabelServiceResult<LabelCasePage>> GetCaseQueueAsync(
        LabelAdministrationCaller caller,
        LabelCaseQuery query,
        string? cursor = null,
        int limit = 100,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        ValidateCasePage(0, limit);
        return ModerationAsync(
            caller: caller,
            permission: LabelAdministrationPermission.Inspect,
            operationId: string.Empty,
            reason: "Inspect a filtered moderation workflow.",
            action: async (connection, transaction, _) =>
            {
                LabelStoreState state = await StateAsync(connection, transaction, cancellationToken).ConfigureAwait(false);
                string binding = CaseQueueBinding(state.Issuer, caller.Actor.Value, query);
                long after = 0;
                if (cursor is not null)
                {
                    string[] parts = cursor.Length <= 100 ? cursor.Split('.') : [];
                    if (parts.Length != 3 || parts[0] != "xenon1" || parts[1] != binding ||
                        !long.TryParse(parts[2], NumberStyles.None, CultureInfo.InvariantCulture, out after) ||
                        after <= 0 || parts[2] != after.ToString(CultureInfo.InvariantCulture))
                    {
                        return CaseFailure<LabelCasePage>(LabelServiceErrorCode.InvalidCursor, "The continuation does not match this workflow request.");
                    }
                }

                string status = query.Queue switch
                {
                    LabelCaseWorkflow.All => string.Empty,
                    LabelCaseWorkflow.Open => " AND c.status=0",
                    LabelCaseWorkflow.InReview => " AND c.status=1",
                    LabelCaseWorkflow.Resolved => " AND c.status=2",
                    _ => " AND c.status<>2"
                };
                byte[]? subject = query.Subject is null ? null : CaseSubjectKey(query.Subject);
                byte[]? assignee = query.Assignee is null ? null : s_strictUtf8.GetBytes(query.Assignee.Value);
                byte[] actor = s_strictUtf8.GetBytes(caller.Actor.Value);
                DbCommand command = Command(
                    connection: connection,
                    transaction: transaction,
                    sql: "SELECT c.payload FROM label_cases c WHERE c.case_id>@after" + status +
                        (subject is null ? string.Empty : " AND c.subject_hash=@subjectHash AND c.subject_key=@subject") +
                        (assignee is null ? string.Empty : " AND c.assignee_hash=@assigneeHash AND c.assignee_key=@assignee") +
                        (query.Queue == LabelCaseWorkflow.Unassigned ? " AND c.assignee_hash IS NULL" : string.Empty) +
                        (query.Queue == LabelCaseWorkflow.AssignedToMe ? " AND c.assignee_hash=@actorHash AND c.assignee_key=@actor" : string.Empty) +
                        (query.ReasonType is null ? string.Empty :
                            " AND EXISTS(SELECT 1 FROM label_reports r WHERE r.case_id=c.case_id AND r.reason_hash=@reasonHash AND r.reason_type=@reason)") +
                        (query.Queue == LabelCaseWorkflow.PendingAppeals ?
                            " AND EXISTS(SELECT 1 FROM label_appeals a WHERE a.case_id=c.case_id AND a.status=0)" : string.Empty) +
                        " ORDER BY c.case_id LIMIT @limit",
                    ("@after", after),
                    ("@subjectHash", subject is null ? null : SHA256.HashData(subject)),
                    ("@subject", subject),
                    ("@assigneeHash", assignee is null ? null : SHA256.HashData(assignee)),
                    ("@assignee", assignee),
                    ("@actorHash", SHA256.HashData(actor)),
                    ("@actor", actor),
                    ("@reasonHash", query.ReasonType is null ? null : OperationHash(query.ReasonType)),
                    ("@reason", query.ReasonType),
                    ("@limit", limit + 1));
                await using ConfiguredAsyncDisposable commandDisposal = command.ConfigureAwait(false);
                DbDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                await using ConfiguredAsyncDisposable readerDisposal = reader.ConfigureAwait(false);
                List<LabelModerationCase> cases = [];
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    cases.Add(DecodeCase((byte[])reader.GetValue(0)).ToCase());
                }

                bool more = cases.Count > limit;
                if (more)
                {
                    cases.RemoveAt(cases.Count - 1);
                }

                return LabelServiceResult<LabelCasePage>.Success(new(
                    cases,
                    more ? "xenon1." + binding + "." + cases[^1].Id.ToString(CultureInfo.InvariantCulture) : null));
            },
            token: cancellationToken,
            auditAction: "case-queue");
    }

    /// <summary>Binds continuations to exact filters without embedding confidential search data in the cursor.</summary>
    private static string CaseQueueBinding(string issuer, string actor, LabelCaseQuery query)
    {
        using MemoryStream buffer = new();
        using (BinaryWriter writer = new(buffer, s_strictUtf8, leaveOpen: true))
        {
            writer.Write(issuer);
            writer.Write(actor);
            writer.Write((int)query.Queue);
            writer.Write(query.Subject is not null);
            if (query.Subject is { } subject)
            {
                writer.Write(subject.Subject);
                writer.Write(subject.Cid?.ToString() ?? string.Empty);
            }

            writer.Write(query.ReasonType is not null);
            writer.Write(query.ReasonType ?? string.Empty);
            writer.Write(query.Assignee is not null);
            writer.Write(query.Assignee?.Value ?? string.Empty);
        }

        return Convert.ToHexString(SHA256.HashData(buffer.ToArray()));
    }
}
