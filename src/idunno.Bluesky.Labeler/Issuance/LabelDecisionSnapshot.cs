// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using idunno.Bluesky.Labeler.Storage;
namespace idunno.Bluesky.Labeler.Issuance;

/// <summary>Captures a decision observation and the store revision used for optimistic concurrency.</summary>
/// <param name="state">The observed durable store state.</param>
/// <param name="currentLabel">The current decision label, or <see langword="null"/> when none exists.</param>
/// <exception cref="ArgumentNullException"><paramref name="state"/> is <see langword="null"/>.</exception>
/// <remarks>
/// <para>The optional current label may be absent when no decision exists. The state is preserved as supplied, including its lifecycle and publication boundaries.</para>
/// <para>A snapshot does not reserve or lock the decision; a later commit must validate its revision.</para>
/// </remarks>
public sealed class LabelDecisionSnapshot(LabelStoreState state, StoredLabel? currentLabel)
{
    /// <summary>Gets the observed durable store state.</summary>
    /// <value>The state whose revision can be used for an optimistic commit.</value>
    /// <exception cref="ArgumentNullException">The state supplied during construction is <see langword="null"/>.</exception>
    public LabelStoreState State { get; } = state ?? throw new ArgumentNullException(nameof(state));

    /// <summary>Gets the current stored decision label.</summary>
    /// <value>The latest decision label, or <see langword="null"/> when no decision exists.</value>
    public StoredLabel? CurrentLabel { get; } = currentLabel;
}
