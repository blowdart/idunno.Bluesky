// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

namespace idunno.Bluesky.Labeler.Moderation;

/// <summary>Identifies a case's review lifecycle independently of its assignment.</summary>
public enum LabelCaseStatus
{
    /// <summary>The subject awaits review.</summary>
    Open,
    /// <summary>A moderator is reviewing the subject.</summary>
    InReview,
    /// <summary>A reasoned outcome closed the review.</summary>
    Resolved
}
