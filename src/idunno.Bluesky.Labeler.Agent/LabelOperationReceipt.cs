// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

namespace idunno.Bluesky.Labeler.Agent;

/// <summary>Contains the durable outcome of a remote issue or negate operation.</summary>
public sealed class LabelOperationReceipt
{
    /// <summary>Initializes a new instance of the <see cref="LabelOperationReceipt"/> class.</summary>
    /// <param name="operationId">The identifier used to recover this outcome.</param>
    /// <param name="sequence">The published stream sequence, or <see langword="null"/> when no new label event was appended.</param>
    /// <param name="changed"><see langword="true"/> if the operation appended a new label event; otherwise, <see langword="false"/>.</param>
    /// <exception cref="ArgumentNullException"><paramref name="operationId"/> is <see langword="null"/>.</exception>
    internal LabelOperationReceipt(string operationId, long? sequence, bool changed)
    {
        OperationId = operationId ?? throw new ArgumentNullException(nameof(operationId));
        Sequence = sequence;
        Changed = changed;
    }

    /// <summary>Gets the identifier used to recover this outcome.</summary>
    /// <value>The durable operation identifier.</value>
    public string OperationId { get; }

    /// <summary>Gets the published stream sequence.</summary>
    /// <value>The sequence, or <see langword="null"/> when the operation appended no event.</value>
    public long? Sequence { get; }

    /// <summary>Gets a value that indicates whether the operation appended a new label event.</summary>
    /// <value><see langword="true"/> if an event was appended; otherwise, <see langword="false"/>.</value>
    public bool Changed { get; }
}
