// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

namespace idunno.Bluesky.Labeler;

/// <summary>Records the original durable outcome of an issue or negate operation.</summary>
/// <param name="request">The immutable original request.</param>
/// <param name="resolvedTarget">The DID-resolved target used for the decision.</param>
/// <param name="changed"><see langword="true"/> if a new signed event was appended; otherwise, <see langword="false"/>.</param>
/// <param name="label">The appended or existing decision label, or <see langword="null"/> when no decision exists.</param>
/// <param name="stateAtCommit">The durable store state at the original commit.</param>
/// <param name="audit">The immutable audit outcome.</param>
/// <exception cref="ArgumentNullException">A required reference argument is <see langword="null"/>.</exception>
/// <remarks>
/// <para>A changed operation carries its appended label; an unchanged operation may carry an existing decision or no label. The constructor preserves restored state and does not infer or impose additional event semantics.</para>
/// <para><see cref="StateAtCommit"/> is the state at the original commit, not a live publication or delivery acknowledgement.</para>
/// </remarks>
public sealed class LabelOperationReceipt(
    LabelOperationRequest request,
    LabelTarget resolvedTarget,
    bool changed,
    StoredLabel? label,
    LabelStoreState stateAtCommit,
    LabelAuditRecord audit)
{
    /// <summary>Gets the original operation request.</summary>
    /// <value>The immutable request, including its original caller context.</value>
    /// <exception cref="ArgumentNullException">The request supplied during construction is <see langword="null"/>.</exception>
    public LabelOperationRequest Request { get; } = request ?? throw new ArgumentNullException(nameof(request));

    /// <summary>Gets the resolved target used for the operation.</summary>
    /// <value>The account or record target with a DID repository.</value>
    /// <exception cref="ArgumentNullException">The resolved target supplied during construction is <see langword="null"/>.</exception>
    public LabelTarget ResolvedTarget { get; } = resolvedTarget ?? throw new ArgumentNullException(nameof(resolvedTarget));

    /// <summary>Gets a value that indicates whether a new signed event was appended.</summary>
    /// <value><see langword="true"/> if a new event was appended; otherwise, <see langword="false"/>.</value>
    public bool Changed { get; } = changed;

    /// <summary>Gets the appended or existing decision label.</summary>
    /// <value>The signed label and stream sequence, or <see langword="null"/> when no decision exists.</value>
    public StoredLabel? Label { get; } = label;

    /// <summary>Gets the original durable state at commit time.</summary>
    /// <value>The immutable state captured at the original commit.</value>
    /// <exception cref="ArgumentNullException">The state supplied during construction is <see langword="null"/>.</exception>
    public LabelStoreState StateAtCommit { get; } = stateAtCommit ?? throw new ArgumentNullException(nameof(stateAtCommit));

    /// <summary>Gets the audit outcome attached to the operation.</summary>
    /// <value>The immutable audit record.</value>
    /// <exception cref="ArgumentNullException">The audit supplied during construction is <see langword="null"/>.</exception>
    public LabelAuditRecord Audit { get; } = audit ?? throw new ArgumentNullException(nameof(audit));
}
