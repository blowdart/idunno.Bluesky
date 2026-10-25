// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

namespace idunno.Bluesky.Labeler.Signing;

/// <summary>Reports a fixed sequence boundary for a resumable label-signature rotation.</summary>
public sealed class LabelSigningKeyRotationProgress
{
    internal LabelSigningKeyRotationProgress(Guid streamId, long through, long lastSequence)
    {
        StreamId = streamId;
        Through = through;
        LastSequence = lastSequence;
    }

    /// <summary>Gets the durable label stream identity.</summary>
    public Guid StreamId { get; }

    /// <summary>Gets the inclusive published boundary captured for this rotation.</summary>
    public long Through { get; }

    /// <summary>Gets the last sequence whose signature was replaced.</summary>
    public long LastSequence { get; }

    /// <summary>Gets a value indicating whether the fixed rotation boundary has been reached.</summary>
    public bool IsComplete => LastSequence >= Through;
}
