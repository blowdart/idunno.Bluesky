// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

namespace idunno.Bluesky.Labeler.Administration;

/// <summary>Identifies an administrative permission independently of its transport.</summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Naming", "CA1711:Identifiers should not have incorrect suffix",
    Justification = "These are application permissions, not legacy code-access-security permission objects.")]
public enum LabelAdministrationPermission
{
    /// <summary>Permits administrative inspection.</summary>
    Inspect,
    /// <summary>Permits issuing and withdrawing labels.</summary>
    Moderate,
    /// <summary>Permits team and credential management.</summary>
    ManageTeam,
    /// <summary>Permits protected maintenance, but does not execute maintenance.</summary>
    Maintain,
    /// <summary>Permits versioned policy management and explicit declaration publication.</summary>
    ManagePolicy
}
