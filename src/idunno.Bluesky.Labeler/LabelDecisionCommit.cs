// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using idunno.AtProto;

namespace idunno.Bluesky.Labeler;

/// <summary>Supplies the complete immutable input for an atomic decision transaction.</summary>
/// <remarks>
/// <para>A missing signed snapshot represents an audited no-op; a supplied snapshot is the exact signed label to persist.</para>
/// <para>The resolved target must preserve the request's target kind and record identity. DID-based requested repositories cannot be changed; a handle-based repository may resolve to a DID. Issue commits retain the requested CID, while a negate with no requested CID may carry the copied CID of the stored decision.</para>
/// <para>The provider is responsible for validating issuer binding, idempotency and revision under its mutation lock. Optional data is retained without imposing additional receipt semantics.</para>
/// </remarks>
public sealed class LabelDecisionCommit
{
    /// <summary>Initializes a new instance of the <see cref="LabelDecisionCommit"/> class.</summary>
    /// <param name="request">The original issue or negate request.</param>
    /// <param name="resolvedTarget">The DID-resolved target for this decision.</param>
    /// <param name="expectedRevision">The store revision observed before evaluating the decision.</param>
    /// <param name="signedLabel">The optional exact signed label to append; <see langword="null"/> represents a no-op.</param>
    /// <param name="audit">The audit outcome to commit atomically.</param>
    /// <exception cref="ArgumentNullException">A required reference argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="resolvedTarget"/> is not DID-based, changes the original repository or record identity, or violates the operation's CID rules.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="expectedRevision"/> is negative.</exception>
    public LabelDecisionCommit(
        LabelOperationRequest request,
        LabelTarget resolvedTarget,
        long expectedRevision,
        SignedLabelSnapshot? signedLabel,
        LabelAuditRecord audit)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(resolvedTarget);
        ArgumentOutOfRangeException.ThrowIfNegative(expectedRevision);
        ArgumentNullException.ThrowIfNull(audit);

        ValidateResolvedTarget(request, resolvedTarget);

        Request = request;
        ResolvedTarget = resolvedTarget;
        ExpectedRevision = expectedRevision;
        SignedLabel = signedLabel;
        Audit = audit;
    }

    /// <summary>Ensures resolution changed only a handle to its DID or copied the stored CID for an unpinned withdrawal.</summary>
    /// <remarks><para>A commit must not redirect authorized intent to another repository, record path
    /// or issuance version. The provider separately checks the copied withdrawal CID against history.</para></remarks>
    private static void ValidateResolvedTarget(LabelOperationRequest request, LabelTarget resolvedTarget)
    {
        if (resolvedTarget.Repository is not Did resolvedDid ||
            resolvedTarget.Kind != request.Target.Kind)
        {
            throw new ArgumentException("The resolved target must have the original kind and a DID repository.", nameof(resolvedTarget));
        }

        if (request.Target.Repository is Did requestedDid && !requestedDid.Equals(resolvedDid))
        {
            throw new ArgumentException("A DID-based request cannot be resolved to a different repository.", nameof(resolvedTarget));
        }

        if (request.Target.Kind == LabelTargetKind.Record)
        {
            AtUri requestedUri = request.Target.RecordUri!;
            AtUri resolvedUri = resolvedTarget.RecordUri!;

            if (resolvedUri.Repo is not Did resolvedUriDid ||
                !resolvedDid.Equals(resolvedUriDid) ||
                !string.Equals(requestedUri.Collection?.ToString(), resolvedUri.Collection?.ToString(), StringComparison.Ordinal) ||
                !string.Equals(requestedUri.RecordKey?.ToString(), resolvedUri.RecordKey?.ToString(), StringComparison.Ordinal))
            {
                throw new ArgumentException("The resolved target must preserve the original record identity and use its resolved DID repository.", nameof(resolvedTarget));
            }
        }

        if (request.Kind == LabelOperationKind.Issue && !Equals(request.Target.Cid, resolvedTarget.Cid))
        {
            throw new ArgumentException("An issue commit must preserve the requested CID.", nameof(resolvedTarget));
        }

        if (request.Kind == LabelOperationKind.Negate &&
            request.Target.Cid is not null &&
            !request.Target.Cid.Equals(resolvedTarget.Cid))
        {
            throw new ArgumentException("A negate commit with an explicit CID must preserve that CID.", nameof(resolvedTarget));
        }
    }

    /// <summary>Gets the original issue or negate request.</summary>
    /// <value>The immutable operation request.</value>
    public LabelOperationRequest Request { get; }

    /// <summary>Gets the DID-resolved target for the decision.</summary>
    /// <value>The resolved account or record target.</value>
    public LabelTarget ResolvedTarget { get; }

    /// <summary>Gets the expected store revision.</summary>
    /// <value>A nonnegative optimistic concurrency revision.</value>
    public long ExpectedRevision { get; }

    /// <summary>Gets the optional exact signed label to append.</summary>
    /// <value>The signed label snapshot, or <see langword="null"/> for an audited no-op.</value>
    public SignedLabelSnapshot? SignedLabel { get; }

    /// <summary>Gets the audit outcome to commit.</summary>
    /// <value>The immutable audit record.</value>
    public LabelAuditRecord Audit { get; }
}
