// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

namespace idunno.Bluesky.Labeler.Moderation;

/// <summary>Identifies a retained policy administration or independent declaration observation.</summary>
public enum LabelPolicyHistoryAction
{
    /// <summary>Retains an immutable policy version without changing the active gate.</summary>
    Draft,
    /// <summary>Begins an explicit conditional publication or reconciliation.</summary>
    Publication,
    /// <summary>Abandons publication without assuming that a remote write was cancelled.</summary>
    Abandonment,
    /// <summary>Records independent verification, conflict or a mismatched pending publication.</summary>
    Observation
}
