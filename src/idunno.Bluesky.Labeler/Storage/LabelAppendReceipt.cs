// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

namespace idunno.Bluesky.Labeler.Storage;

/// <summary>
/// Represents the durable acknowledgement returned by <see cref="ILabelStore.AppendAsync"/> to identify a committed
/// signed-label batch or retirement checkpoint and recover its original result after a retry or lost acknowledgement.
/// </summary>
/// <param name="OperationId">The durable idempotency key.</param>
/// <param name="FirstSequence">The first sequence, or zero for an empty checkpoint batch.</param>
/// <param name="LastSequence">The last sequence, or zero for an empty checkpoint batch.</param>
/// <param name="Revision">The revision at original commit.</param>
/// <remarks>
/// <para>
/// An identical retry using the same operation key and signed input returns the original receipt.
/// Use <see cref="ILabelStore.GetReceiptAsync"/> to look up an uncertain outcome without generating new signatures.
/// Sequence boundaries are inclusive; both are zero for a retirement checkpoint that appends no labels.
/// </para>
/// </remarks>
public sealed record LabelAppendReceipt(string OperationId, long FirstSequence, long LastSequence, long Revision);
