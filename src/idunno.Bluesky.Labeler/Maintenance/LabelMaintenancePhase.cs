// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

namespace idunno.Bluesky.Labeler.Maintenance;

/// <summary>Identifies the durable phase of a coordinated signing-key rotation.</summary>
public enum LabelMaintenancePhase
{
    /// <summary>No rotation is active.</summary>
    None,
    /// <summary>Issuance and delivery are draining and participating hosts must acknowledge quiescence.</summary>
    Draining,
    /// <summary>The drained stream boundary is fixed and the replacement key must be published and verified.</summary>
    AwaitingKeyPublication,
    /// <summary>Retained published labels are being re-signed in bounded batches.</summary>
    Resigning,
    /// <summary>All retained labels are re-signed and the replacement key can be activated.</summary>
    ReadyToReopen,
    /// <summary>The rotation completed and maintenance was safely released.</summary>
    Completed
}
