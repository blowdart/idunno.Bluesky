// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

namespace idunno.Bluesky.Labeler.Moderation;

/// <summary>Persists deduplicated policy-verification alert state and delivery leases.</summary>
/// <remarks><para>This trusted infrastructure interface is for the host's notification worker, not a
/// public administrative endpoint. Delivery is at-least-once because SMTP cannot participate in the
/// database transaction.</para></remarks>
public interface ILabelPolicyAlertStore
{
    /// <summary>Records the beginning of a verification-failure incident once until verification succeeds.</summary>
    /// <param name="category">A bounded server-selected failure category.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that completes after the incident state is durable.</returns>
    Task RecordVerificationFailureAsync(string category, CancellationToken cancellationToken = default);

    /// <summary>Ends the current failure incident without discarding an undelivered alert.</summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that completes after the recovered state is durable.</returns>
    Task RecordVerificationSuccessAsync(CancellationToken cancellationToken = default);

    /// <summary>Claims one pending alert for a bounded delivery lease.</summary>
    /// <param name="leaseId">The unique identifier for this delivery attempt.</param>
    /// <param name="leaseDuration">The bounded claim lifetime.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The claimed alert, or <see langword="null" /> when none is due or no administrator has registered an address.</returns>
    Task<LabelPolicyAlertDelivery?> ClaimPendingAlertAsync(
        string leaseId,
        TimeSpan leaseDuration,
        CancellationToken cancellationToken = default);

    /// <summary>Completes or releases the matching alert-delivery claim.</summary>
    /// <param name="alertId">The claimed alert identifier.</param>
    /// <param name="leaseId">The exact delivery lease identifier.</param>
    /// <param name="delivered">Whether the SMTP server accepted the message.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that completes after the durable delivery state changes.</returns>
    Task CompleteAlertDeliveryAsync(
        string alertId,
        string leaseId,
        bool delivered,
        CancellationToken cancellationToken = default);
}

/// <summary>Contains one claimed verification alert and its eligible administrator recipients.</summary>
public sealed class LabelPolicyAlertDelivery
{
    /// <summary>Initializes one durable alert delivery claim.</summary>
    /// <param name="alertId">The stable failure-incident identity.</param>
    /// <param name="leaseId">The unique delivery-attempt identity.</param>
    /// <param name="category">The bounded server-selected failure category.</param>
    /// <param name="occurredAt">The first failure time from the database clock.</param>
    /// <param name="recipients">The active administrators' registered addresses.</param>
    internal LabelPolicyAlertDelivery(
        string alertId,
        string leaseId,
        string category,
        DateTimeOffset occurredAt,
        IReadOnlyList<string> recipients)
    {
        AlertId = alertId;
        LeaseId = leaseId;
        Category = category;
        OccurredAt = occurredAt;
        Recipients = recipients;
    }

    /// <summary>Gets the stable failure-incident identity.</summary>
    public string AlertId { get; }

    /// <summary>Gets the unique delivery-attempt identity.</summary>
    public string LeaseId { get; }

    /// <summary>Gets the bounded server-selected failure category.</summary>
    public string Category { get; }

    /// <summary>Gets the original failure time.</summary>
    public DateTimeOffset OccurredAt { get; }

    /// <summary>Gets active administrator addresses registered when the claim was created.</summary>
    public IReadOnlyList<string> Recipients { get; }
}
