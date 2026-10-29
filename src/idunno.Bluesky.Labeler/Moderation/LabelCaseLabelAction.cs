// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using idunno.Bluesky.Labeler.Issuance;

namespace idunno.Bluesky.Labeler.Moderation;

/// <summary>Describes one label-slot action within an atomic exact-subject case decision.</summary>
public sealed class LabelCaseLabelAction
{
    /// <summary>Initializes a new instance of the <see cref="LabelCaseLabelAction"/> class.</summary>
    /// <param name="kind">The issue or negate action.</param>
    /// <param name="value">The nonblank label value.</param>
    /// <param name="expiresAt">The optional absolute issuance expiry.</param>
    /// <param name="expectedLabelSequence">The optional exact current label to fence, including for appeals; zero requires absent history.</param>
    /// <exception cref="ArgumentException">A value is invalid, or a withdrawal specifies an expiry.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The action, value length or expected sequence is invalid.</exception>
    public LabelCaseLabelAction(
        LabelOperationKind kind,
        string value,
        DateTimeOffset? expiresAt = null,
        long? expectedLabelSequence = null)
    {
        if (!Enum.IsDefined(kind))
        {
            throw new ArgumentOutOfRangeException(nameof(kind));
        }

        if (kind == LabelOperationKind.Negate && expiresAt is not null)
        {
            throw new ArgumentException("A withdrawal cannot specify an expiry.", nameof(expiresAt));
        }

        if (expectedLabelSequence is < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(expectedLabelSequence));
        }

        Kind = kind;
        Value = LabelInput.ValidateText(value, nameof(value), 128);
        ExpiresAt = expiresAt?.ToUniversalTime();
        ExpectedLabelSequence = expectedLabelSequence;
    }

    /// <summary>Gets the issue or withdrawal action.</summary>
    /// <value>The immutable operation kind.</value>
    public LabelOperationKind Kind { get; }
    /// <summary>Gets the label value identifying the subject's label slot.</summary>
    /// <value>The nonblank value.</value>
    public string Value { get; }
    /// <summary>Gets the optional absolute issuance expiry.</summary>
    /// <value>The UTC expiry, or <see langword="null"/> for no expiry.</value>
    public DateTimeOffset? ExpiresAt { get; }
    /// <summary>Gets the exact current label required by this action.</summary>
    /// <value>The retained sequence, zero for absent history, or <see langword="null"/> for ordinary current-state reconciliation.</value>
    public long? ExpectedLabelSequence { get; }
}
