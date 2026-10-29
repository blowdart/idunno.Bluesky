// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using idunno.AtProto;

namespace idunno.Bluesky.Labeler.Moderation;

/// <summary>Identifies the durable declaration publication gate.</summary>
public enum LabelPolicyPhase
{
    /// <summary>No declaration has been independently verified.</summary>
    Unverified,
    /// <summary>An explicit publication intent is gated pending independent verification.</summary>
    Publishing,
    /// <summary>The selected declaration was independently verified; freshness still applies.</summary>
    VerifiedActive,
    /// <summary>Observed declaration drift requires explicit administrator reconciliation.</summary>
    Conflict,
    /// <summary>Removed label values are being negated in durable batches before declaration publication is permitted.</summary>
    Withdrawing
}

/// <summary>Describes a durable policy selection and its latest verification observation.</summary>
/// <remarks><para>A verified phase alone is insufficient for issuance: providers check the database-clock
/// freshness deadline and exact policy version at commit. Failed refreshes do not extend that deadline.</para></remarks>
public sealed class LabelPolicyState
{
    /// <summary>Initializes a retained provider snapshot.</summary>
    internal LabelPolicyState(
        Did issuer,
        long revision,
        LabelPolicyPhase phase,
        LabelPolicyDefinition? policy,
        Cid? previousCid,
        Cid? verifiedCid,
        DateTimeOffset? verifiedUntil,
        TimeSpan verificationFreshness,
        DateTimeOffset databaseTime,
        string? publicationOperationId,
        LabelPolicyWithdrawalProgress? withdrawal = null)
    {
        Issuer = issuer;
        Revision = revision;
        Phase = phase;
        Policy = policy;
        PreviousCid = previousCid;
        VerifiedCid = verifiedCid;
        VerifiedUntil = verifiedUntil;
        VerificationFreshness = verificationFreshness;
        DatabaseTime = databaseTime;
        PublicationOperationId = publicationOperationId;
        Withdrawal = withdrawal;
    }

    /// <summary>Gets the issuer bound to this policy store.</summary>
    /// <value>The labeler DID whose declaration must be independently verified.</value>
    public Did Issuer { get; }

    /// <summary>Gets the optimistic revision for publication and verification.</summary>
    /// <value>The durable selection revision.</value>
    public long Revision { get; }
    /// <summary>Gets the stable identity of the publication intent retained by this gate.</summary>
    /// <value>The publication operation identifier, or <see langword="null"/> before publication.</value>
    /// <remarks><para>Recovery must compare this identifier with current state rather than trusting
    /// a historical operation receipt or matching version alone. Abandonment retains the identifier
    /// but closes permission; a subsequent publication replaces it.</para></remarks>
    public string? PublicationOperationId { get; }
    /// <summary>Gets the publication gate phase.</summary>
    /// <value>The durable phase, independent of freshness expiry.</value>
    public LabelPolicyPhase Phase { get; }
    /// <summary>Gets the selected intended declaration and permissions.</summary>
    /// <value>The selected policy, or <see langword="null"/> before publication intent.</value>
    public LabelPolicyDefinition? Policy { get; }
    /// <summary>Gets the expected pre-publication record CID.</summary>
    /// <value>The conditional-update CID, or <see langword="null"/> for conditional creation.</value>
    public Cid? PreviousCid { get; }
    /// <summary>Gets the independently observed matching declaration CID.</summary>
    /// <value>The verified CID, or <see langword="null"/> before successful verification.</value>
    public Cid? VerifiedCid { get; }
    /// <summary>Gets the database-clock freshness deadline.</summary>
    /// <value>The exclusive deadline, or <see langword="null"/> while unverified.</value>
    public DateTimeOffset? VerifiedUntil { get; }
    /// <summary>Gets the configured maximum observation freshness.</summary>
    /// <value>The bounded freshness interval, initially 30 minutes.</value>
    public TimeSpan VerificationFreshness { get; }
    /// <summary>Gets the database clock sampled while reading this snapshot.</summary>
    /// <value>The verification-start instant to retain across an independent network lookup.</value>
    public DateTimeOffset DatabaseTime { get; }

    /// <summary>Gets bounded withdrawal progress independently of retained authorization evidence.</summary>
    internal LabelPolicyWithdrawalProgress? Withdrawal { get; }
}
