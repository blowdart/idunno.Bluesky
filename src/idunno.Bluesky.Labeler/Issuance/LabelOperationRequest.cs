// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

namespace idunno.Bluesky.Labeler.Issuance;

/// <summary>Describes an immutable issue or negate intent with a durable operation identifier.</summary>
/// <remarks>
/// <para>The factories validate and retain caller intent without resolving identifiers or applying policy. Expiry is normalized to an absolute UTC instant.</para>
/// <para>Semantic retry identity excludes the diagnostic correlation identifier; the original request remains available on a recovered receipt.</para>
/// </remarks>
public sealed class LabelOperationRequest
{
    /// <summary>Initializes a new instance of the <see cref="LabelOperationRequest"/> class with validated text and UTC expiry.</summary>
    /// <remarks><para>The public factories supply the operation kind and non-null references; this shared
    /// constructor keeps input limits and expiry normalization identical for both request paths.</para></remarks>
    private LabelOperationRequest(
        string operationId,
        LabelOperationKind kind,
        LabelTarget target,
        string value,
        LabelOperationContext context,
        DateTimeOffset? expiresAt)
    {
        OperationId = LabelInput.ValidateText(operationId, nameof(operationId), 256);
        Kind = kind;
        Target = target;
        Value = LabelInput.ValidateText(value, nameof(value), 128);
        Context = context;
        ExpiresAt = expiresAt?.ToUniversalTime();
    }

    /// <summary>Creates an issue request.</summary>
    /// <param name="operationId">The nonblank durable operation identifier.</param>
    /// <param name="target">The account or record to label.</param>
    /// <param name="value">The nonblank label value.</param>
    /// <param name="context">The trusted caller context and reason.</param>
    /// <param name="expiresAt">An optional absolute expiry instant.</param>
    /// <returns>An immutable issue request.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="target"/> or <paramref name="context"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">A text argument is blank, contains U+0000 or is not valid UTF-8.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The operation identifier exceeds 256 UTF-8 bytes or the label value exceeds 128 UTF-8 bytes.</exception>
    public static LabelOperationRequest Issue(
        string operationId,
        LabelTarget target,
        string value,
        LabelOperationContext context,
        DateTimeOffset? expiresAt = null)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(context);
        return new LabelOperationRequest(
            operationId: operationId,
            kind: LabelOperationKind.Issue,
            target: target,
            value: value,
            context: context,
            expiresAt: expiresAt);
    }

    /// <summary>Creates a negate request.</summary>
    /// <param name="operationId">The nonblank durable operation identifier.</param>
    /// <param name="target">The account or record to withdraw a label from.</param>
    /// <param name="value">The nonblank label value to withdraw.</param>
    /// <param name="context">The trusted caller context and reason.</param>
    /// <returns>An immutable negate request.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="target"/> or <paramref name="context"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">A text argument is blank, contains U+0000 or is not valid UTF-8.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The operation identifier exceeds 256 UTF-8 bytes or the label value exceeds 128 UTF-8 bytes.</exception>
    public static LabelOperationRequest Negate(
        string operationId,
        LabelTarget target,
        string value,
        LabelOperationContext context)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(context);
        return new LabelOperationRequest(
            operationId: operationId,
            kind: LabelOperationKind.Negate,
            target: target,
            value: value,
            context: context,
            expiresAt: null);
    }

    /// <summary>Gets the durable identifier used to recover this operation.</summary>
    /// <value>The nonblank operation identifier.</value>
    public string OperationId { get; }

    /// <summary>Gets the requested issue or negate action.</summary>
    /// <value>The operation kind.</value>
    public LabelOperationKind Kind { get; }

    /// <summary>Gets the caller context attached to the request.</summary>
    /// <value>The immutable operation context.</value>
    public LabelOperationContext Context { get; }

    /// <summary>Gets the original target identity.</summary>
    /// <value>The requested target.</value>
    public LabelTarget Target { get; }

    /// <summary>Gets the requested label value.</summary>
    /// <value>The nonblank label value.</value>
    public string Value { get; }

    /// <summary>Gets the optional expiry normalized to UTC.</summary>
    /// <value>The absolute UTC expiry, or <see langword="null"/> when the label does not expire.</value>
    public DateTimeOffset? ExpiresAt { get; }

    /// <summary>Compares this request's canonical business intent with another request.</summary>
    /// <param name="other">The request to compare.</param>
    /// <returns><see langword="true"/> if the semantic inputs match; otherwise, <see langword="false"/>.</returns>
    /// <remarks>
    /// <para>The comparison includes action, original target identity and CID, value, normalized expiry, actor and reason. It excludes the operation ID and correlation ID.</para>
    /// <para>CID comparison uses the parsed CID's structural identity (version, codec and hash), rather than an input encoding's original text. The CID type canonicalizes its string representation when formatted.</para>
    /// <para>This internal comparison is the canonical semantic check used alongside the separately keyed operation identifier.</para>
    /// </remarks>
    internal bool SemanticallyEquals(LabelOperationRequest other)
    {
        ArgumentNullException.ThrowIfNull(other);

        return Kind == other.Kind
            && Target.Kind == other.Target.Kind
            && string.Equals(Target.Subject, other.Target.Subject, StringComparison.Ordinal)
            && Equals(Target.Cid, other.Target.Cid)
            && string.Equals(Value, other.Value, StringComparison.Ordinal)
            && ExpiresAt == other.ExpiresAt
            && Equals(Context.Actor, other.Context.Actor)
            && string.Equals(Context.Reason, other.Context.Reason, StringComparison.Ordinal);
    }
}
