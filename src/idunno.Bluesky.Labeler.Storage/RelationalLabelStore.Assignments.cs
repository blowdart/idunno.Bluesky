// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Data.Common;
using System.Globalization;

using idunno.Bluesky.Labeler.Administration;
using idunno.Bluesky.Labeler.Moderation;

namespace idunno.Bluesky.Labeler.Storage;

internal abstract partial class RelationalLabelStore
{
    /// <summary>Finds the sole live moderator or administrator without treating viewers or removed members as candidates.</summary>
    private static string? SoleModerator(AdministrationState team)
    {
        string[] candidates = team.Members
            .Where(entry => entry.Value.Active && !entry.Value.Removed && entry.Value.Role >= LabelTeamRole.Moderator)
            .Select(entry => entry.Key).Take(2).ToArray();
        return candidates.Length == 1 ? candidates[0] : null;
    }

    /// <summary>Reconciles existing unresolved assignments under the issuer lock, atomically with team changes or authorized inspection.</summary>
    /// <remarks><para>Reads bounded batches and persists each revision and history entry in the same transaction.
    /// Cancellation or restart rolls back the transaction; the next authorized operation can safely retry it.
    /// Resolved cases and immutable operation receipts are never rewritten.</para></remarks>
    private async Task ReconcileAssignmentsAsync(
        DbConnection connection,
        DbTransaction transaction,
        AdministrationState team,
        LabelAdministrationCaller caller,
        CancellationToken token)
    {
        if (SoleModerator(team) is not string sole)
        {
            return;
        }

        long changed = 0;
        DateTimeOffset timestamp = DateTimeOffset.FromUnixTimeMilliseconds(
            await DatabaseTimeAsync(connection, transaction, token).ConfigureAwait(false));
        while (true)
        {
            List<ModerationCaseData> cases = [];
            DbCommand command = Command(
                connection: connection,
                transaction: transaction,
                sql: "SELECT payload FROM label_cases WHERE status<>@resolved AND " +
                    "(assignee_key IS NULL OR assignee_key<>@assignee) ORDER BY case_id LIMIT 128",
                ("@resolved", (int)LabelCaseStatus.Resolved),
                ("@assignee", s_strictUtf8.GetBytes(sole)));
            await using (command.ConfigureAwait(false))
            {
                DbDataReader reader = await command.ExecuteReaderAsync(token).ConfigureAwait(false);
                await using (reader.ConfigureAwait(false))
                {
                    while (await reader.ReadAsync(token).ConfigureAwait(false))
                    {
                        cases.Add(DecodeCase((byte[])reader.GetValue(0)));
                    }
                }
            }

            if (cases.Count == 0)
            {
                break;
            }

            foreach (ModerationCaseData current in cases)
            {
                ModerationCaseData next = current with
                {
                    Assignee = sole,
                    Revision = checked(current.Revision + 1),
                    UpdatedAt = timestamp
                };
                await SaveCaseAsync(
                    connection: connection,
                    transaction: transaction,
                    next: next,
                    subject: current.ToCase().Subject,
                    insert: false,
                    token: token).ConfigureAwait(false);
                string operation = $"auto-assign/{current.Id.ToString(CultureInfo.InvariantCulture)}/{next.Revision.ToString(CultureInfo.InvariantCulture)}";
                await AppendCaseEventAsync(
                    connection: connection,
                    transaction: transaction,
                    next: next,
                    operationId: operation,
                    action: LabelCaseAction.Assign,
                    actor: caller.Actor,
                    localIdentity: caller.LocalIdentity,
                    reason: "Automatically assigned to the only active moderator or administrator.",
                    intent: Intent(sole),
                    operationNamespace: "automatic-assignment",
                    token: token).ConfigureAwait(false);
                changed++;
            }
        }

        if (changed > 0)
        {
            await AuditAdministrationAsync(
                connection: connection,
                transaction: transaction,
                administration: team,
                actor: caller.Actor,
                localIdentity: caller.LocalIdentity,
                operationId: string.Empty,
                action: "automatic-assignment",
                reason: $"Assigned {changed.ToString(CultureInfo.InvariantCulture)} unresolved cases to the sole active moderator.",
                succeeded: true,
                token: token).ConfigureAwait(false);
        }
    }
}
