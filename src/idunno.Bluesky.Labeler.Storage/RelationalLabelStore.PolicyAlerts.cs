// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using idunno.AtProto;
using idunno.Bluesky.Labeler.Administration;
using idunno.Bluesky.Labeler.Moderation;

namespace idunno.Bluesky.Labeler.Storage;

internal abstract partial class RelationalLabelStore
{
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
                if (team.PendingPolicyAlert is not { } alert)
                {
                    return null;
                }

                long now = await DatabaseTimeAsync(connection, transaction, cancellationToken).ConfigureAwait(false);
                if (alert.LeaseUntil is { } leaseUntil && leaseUntil.ToUnixTimeMilliseconds() > now)
                {
                    return null;
                }

                string[] recipients = team.Members.Values
                    .Where(member => member is { Active: true, Removed: false, Role: LabelTeamRole.Administrator, NotificationEmail: not null })
                    .Select(member => member.NotificationEmail!)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(address => address, StringComparer.OrdinalIgnoreCase)
                    .ToArray();
                if (recipients.Length == 0)
                {
                    return null;
                }

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

                return true;
            },
            token: cancellationToken);
    }
}
