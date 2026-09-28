// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using idunno.AtProto.Labels;

namespace idunno.AtProto.Firehose;

/// <summary>
/// Encapsulates a <c>#labels</c> event from <c>com.atproto.label.subscribeLabels</c>.
/// </summary>
/// <remarks>
/// <para>Label signatures are only checked when <see cref="FirehoseOptions.VerifySignatures"/> is set. Without verification
/// a label is only as trustworthy as the labeler it was received from.</para>
/// </remarks>
public sealed record FirehoseLabelsEvent : FirehoseEvent
{
    /// <summary>
    /// Creates a new instance of <see cref="FirehoseLabelsEvent"/>.
    /// </summary>
    /// <param name="sequence">The sequence number of the event.</param>
    /// <param name="labels">The labels in the event.</param>
    internal FirehoseLabelsEvent(long sequence, IReadOnlyList<Label> labels) : base(sequence)
    {
        Labels = labels;
    }

    /// <summary>
    /// Gets the labels in the event.
    /// </summary>
    public IReadOnlyList<Label> Labels { get; }
}
