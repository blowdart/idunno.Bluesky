// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

namespace idunno.Bluesky.Labeler.Administration;

/// <summary>Identifies a labeler team member's authority.</summary>
public enum LabelTeamRole
{
    /// <summary>Permits administrative inspection.</summary>
    Viewer,
    /// <summary>Also permits issuing and withdrawing labels.</summary>
    Moderator,
    /// <summary>Also permits team, credential and maintenance administration.</summary>
    Administrator
}
