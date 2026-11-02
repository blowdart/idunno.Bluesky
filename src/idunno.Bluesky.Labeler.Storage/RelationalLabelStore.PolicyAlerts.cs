// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Data.Common;
using System.Globalization;
using System.Text.Json;

using idunno.AtProto;
using idunno.Bluesky.Labeler.Administration;
using idunno.Bluesky.Labeler.Moderation;

namespace idunno.Bluesky.Labeler.Storage;

internal abstract partial class RelationalLabelStore
{
    /// <summary>Counts current active administrators independently of registered email addresses.</summary>
    private static int ActiveAdministratorCount(AdministrationState team) =>
        team.Members.Values.Count(member => member is { Active: true, Removed: false, Role: LabelTeamRole.Administrator });

    /// <summary>Persists one alert obligation per continuous verification-failure incident.</summary>
    public Task RecordVerificationFailureAsync(string category, CancellationToken cancellationToken = default)
    {
        LabelInput.ValidateText(category, nameof(category), 128);
        return RunAsync(
            operation: "policy-alert-failure",
            action: async (connection, transaction, state) =>
            {
                AdministrationState team = await ReadAdministrationAsync(connection, transaction, cancellationToken).ConfigureAwait(false);
                if (!team.PolicyVerificationFailureActive)
                {
                    long now = await DatabaseTimeAsync(connection, transaction, cancellationToken).ConfigureAwait(false);
                    team.PolicyVerificationFailureActive = true;
                    team.PendingPolicyAlert ??= new(
                        Id: Guid.NewGuid().ToString("N"),
                        Category: category,
                        OccurredAt: DateTimeOffset.FromUnixTimeMilliseconds(now));
                    await AuditAdministrationAsync(
                        connection: connection,
                        transaction: transaction,
                        administration: team,
                        actor: new Did(state.Issuer),
                        localIdentity: "system:policy-verification",
                        operationId: "policy-verification-failure/" + team.PendingPolicyAlert.Id,
                        action: "policy-verification-failed",
                        reason: "Independent active-policy verification failed.",
                        succeeded: false,
                        token: cancellationToken).ConfigureAwait(false);
                }

                return true;
            },
            token: cancellationToken);
    }

    /// <summary>Closes the current failure incident while retaining any unsent notification.</summary>
    public Task RecordVerificationSuccessAsync(CancellationToken cancellationToken = default) =>
        RunAsync(
            operation: "policy-alert-recovery",
            action: async (connection, transaction, _) =>
            {
                AdministrationState team = await ReadAdministrationAsync(connection, transaction, cancellationToken).ConfigureAwait(false);
                if (team.PolicyVerificationFailureActive)
                {
                    team.PolicyVerificationFailureActive = false;
                    await SaveAdministrationAsync(connection, transaction, team, cancellationToken).ConfigureAwait(false);
                }

                return true;
            },
            token: cancellationToken);

    /// <summary>Claims one pending alert under the database clock and includes only current active administrators.</summary>
    public Task<LabelPolicyAlertDelivery?> ClaimPendingAlertAsync(
        string leaseId,
        TimeSpan leaseDuration,
        CancellationToken cancellationToken = default)
    {
        LabelInput.ValidateText(leaseId, nameof(leaseId), 256);
        ArgumentOutOfRangeException.ThrowIfLessThan(leaseDuration, TimeSpan.FromSeconds(30));
        ArgumentOutOfRangeException.ThrowIfGreaterThan(leaseDuration, TimeSpan.FromMinutes(10));
        return RunAsync<LabelPolicyAlertDelivery?>(
            operation: "policy-alert-claim",
            action: async (connection, transaction, _) =>
            {
                AdministrationState team = await ReadAdministrationAsync(connection, transaction, cancellationToken).ConfigureAwait(false);
                long now = await DatabaseTimeAsync(connection, transaction, cancellationToken).ConfigureAwait(false);
                string[] recipients = team.Members.Values
                    .Where(member => member is { Active: true, Removed: false, Role: LabelTeamRole.Administrator, NotificationEmail: not null })
                    .Select(member => member.NotificationEmail!)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(address => address, StringComparer.OrdinalIgnoreCase)
                    .ToArray();
                if (team.PendingPolicyAlert is { } alert && recipients.Length > 0 &&
                    (alert.LeaseUntil is null || alert.LeaseUntil.Value.ToUnixTimeMilliseconds() <= now))
                {
                    team.PendingPolicyAlert = alert with
                    {
                        LeaseId = leaseId,
                        LeaseUntil = DateTimeOffset.FromUnixTimeMilliseconds(now).Add(leaseDuration)
                    };
                    await SaveAdministrationAsync(connection, transaction, team, cancellationToken).ConfigureAwait(false);
                    return new LabelPolicyAlertDelivery(
                        alertId: alert.Id,
                        leaseId: leaseId,
                        category: alert.Category,
                        occurredAt: alert.OccurredAt,
                        recipients: recipients);
                }

                if (ActiveAdministratorCount(team) <= 1)
                {
                    team.ChangeNotificationThrough = team.Sequence;
                    team.PendingChangeAlert = null;
                    await SaveAdministrationAsync(connection, transaction, team, cancellationToken).ConfigureAwait(false);
                    return null;
                }
                if (recipients.Length == 0)
                {
                    return null;
                }
                if (team.PendingChangeAlert is null)
                {
                    await ReadChangeNotificationAsync(connection, transaction, team, cancellationToken).ConfigureAwait(false);
                }
                if (team.PendingChangeAlert is not { } change ||
                    change.LeaseUntil is { } until && until.ToUnixTimeMilliseconds() > now)
                {
                    await SaveAdministrationAsync(connection, transaction, team, cancellationToken).ConfigureAwait(false);
                    return null;
                }

                team.PendingChangeAlert = change with
                {
                    LeaseId = leaseId,
                    LeaseUntil = DateTimeOffset.FromUnixTimeMilliseconds(now).Add(leaseDuration)
                };
                await SaveAdministrationAsync(connection, transaction, team, cancellationToken).ConfigureAwait(false);
                return new LabelPolicyAlertDelivery(
                    alertId: "change-" + change.Sequence.ToString(CultureInfo.InvariantCulture),
                    leaseId: leaseId,
                    category: "administration-change",
                    occurredAt: change.OccurredAt,
                    recipients: recipients)
                {
                    ChangeDescription = change.Description
                };
            },
            token: cancellationToken);
    }

    /// <summary>Deletes a delivered obligation or makes a failed SMTP attempt immediately retryable.</summary>
    public Task CompleteAlertDeliveryAsync(
        string alertId,
        string leaseId,
        bool delivered,
        CancellationToken cancellationToken = default)
    {
        LabelInput.ValidateText(alertId, nameof(alertId), 256);
        LabelInput.ValidateText(leaseId, nameof(leaseId), 256);
        return RunAsync(
            operation: "policy-alert-delivery",
            action: async (connection, transaction, _) =>
            {
                AdministrationState team = await ReadAdministrationAsync(connection, transaction, cancellationToken).ConfigureAwait(false);
                if (team.PendingPolicyAlert is { } alert && alert.Id == alertId && alert.LeaseId == leaseId)
                {
                    team.PendingPolicyAlert = delivered
                        ? null
                        : alert with { LeaseId = null, LeaseUntil = null };
                    await SaveAdministrationAsync(connection, transaction, team, cancellationToken).ConfigureAwait(false);
                }
                else if (team.PendingChangeAlert is { } change && change.LeaseId == leaseId &&
                    alertId == "change-" + change.Sequence.ToString(CultureInfo.InvariantCulture))
                {
                    if (delivered)
                    {
                        team.ChangeNotificationThrough = change.Sequence;
                        team.PendingChangeAlert = null;
                    }
                    else
                    {
                        team.PendingChangeAlert = change with { LeaseId = null, LeaseUntil = null };
                    }
                    await SaveAdministrationAsync(connection, transaction, team, cancellationToken).ConfigureAwait(false);
                }

                return true;
            },
            token: cancellationToken);
    }

    /// <summary>Scans a bounded audit page, retaining delivery progress without copying an unbounded outbox into the team aggregate.</summary>
    private static async Task ReadChangeNotificationAsync(
        DbConnection connection,
        DbTransaction transaction,
        AdministrationState team,
        CancellationToken token)
    {
        DbCommand command = Command(
            connection: connection,
            transaction: transaction,
            sql: "SELECT payload FROM label_admin_audit WHERE seq>@after ORDER BY seq LIMIT 100",
            ("@after", team.ChangeNotificationThrough));
        await using System.Runtime.CompilerServices.ConfiguredAsyncDisposable commandDisposal = command.ConfigureAwait(false);
        DbDataReader reader = await command.ExecuteReaderAsync(token).ConfigureAwait(false);
        await using System.Runtime.CompilerServices.ConfiguredAsyncDisposable readerDisposal = reader.ConfigureAwait(false);
        while (await reader.ReadAsync(token).ConfigureAwait(false))
        {
            AdministrationAudit audit = JsonSerializer.Deserialize(
                (byte[])reader.GetValue(0),
                AdministrationJsonContext.Default.AdministrationAudit)
                ?? throw new InvalidDataException("The administrative audit is null.");
            if (audit is { Succeeded: true, ChangeDescription: not null, CreatedAt: not null })
            {
                team.PendingChangeAlert = new(audit.Sequence, audit.ChangeDescription, audit.CreatedAt.Value);
                break;
            }
            team.ChangeNotificationThrough = audit.Sequence;
        }
    }
}
