// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

namespace idunno.Bluesky.Labeler.Moderation;

/// <summary>Identifies a reasoned case-management operation.</summary>
public enum LabelCaseAction
{
    /// <summary>Opens or links to the existing open case for the exact subject.</summary>
    Open,
    /// <summary>Begins review of an open case.</summary>
    StartReview,
    /// <summary>Assigns or releases an unresolved case.</summary>
    Assign,
    /// <summary>Appends a note without changing workflow status.</summary>
    Note,
    /// <summary>Closes a case without issuing or withdrawing labels.</summary>
    Resolve,
    /// <summary>Reopens a resolved case if its subject has no other open case.</summary>
    Reopen,
    /// <summary>Updates a case through an atomic zero-action or multi-label decision, retaining unresolved appeals.</summary>
    Decide,
    /// <summary>Opens or links an exact-subject case and captures current labels for an internal moderator review.</summary>
    OpenAppeal
}
