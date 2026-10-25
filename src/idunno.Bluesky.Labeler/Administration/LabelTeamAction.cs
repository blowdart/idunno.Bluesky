// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

namespace idunno.Bluesky.Labeler.Administration;

/// <summary>Identifies a durable team or credential change.</summary>
public enum LabelTeamAction
{
    /// <summary>Enrolls a verified account.</summary>
    Enroll,
    /// <summary>Changes an active or suspended member's role.</summary>
    SetRole,
    /// <summary>Suspends a member.</summary>
    Suspend,
    /// <summary>Reactivates a suspended member without restoring old bindings.</summary>
    Reinstate,
    /// <summary>Removes a member while retaining a generation tombstone.</summary>
    Remove,
    /// <summary>Binds an OS identity to an active member.</summary>
    BindLocal,
    /// <summary>Revokes an OS identity binding.</summary>
    RevokeLocal
}
