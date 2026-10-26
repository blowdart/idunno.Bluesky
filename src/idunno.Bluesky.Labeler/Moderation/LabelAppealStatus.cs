// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

namespace idunno.Bluesky.Labeler.Moderation;

/// <summary>Identifies the explicit review outcome of a retained label challenge.</summary>
public enum LabelAppealStatus
{
    /// <summary>The captured label outcomes await explicit moderator review.</summary>
    Pending,
    /// <summary>A moderator upheld the unchanged challenged outcomes without issuing labels.</summary>
    Upheld,
    /// <summary>A moderator explicitly withdrew selected challenged labels.</summary>
    Withdrawn,
    /// <summary>A moderator explicitly issued replacement outcomes, optionally withdrawing challenged labels.</summary>
    Replaced,
    /// <summary>A moderator acknowledged that newer outcomes superseded the captured challenge without changing labels.</summary>
    Superseded
}
