// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

namespace idunno.Bluesky.Labeler.Moderation;

/// <summary>Identifies a fixed workflow view rather than configurable routing or a named work queue.</summary>
public enum LabelCaseWorkflow
{
    /// <summary>Includes open and in-review cases.</summary>
    Unresolved,
    /// <summary>Includes cases awaiting initial review.</summary>
    Open,
    /// <summary>Includes cases whose review has started.</summary>
    InReview,
    /// <summary>Includes unresolved cases without a manually assigned moderator.</summary>
    Unassigned,
    /// <summary>Includes unresolved cases assigned to the authenticated caller.</summary>
    AssignedToMe,
    /// <summary>Includes unresolved cases with at least one pending appeal.</summary>
    PendingAppeals,
    /// <summary>Includes resolved cases.</summary>
    Resolved,
    /// <summary>Includes cases in every workflow state.</summary>
    All
}
