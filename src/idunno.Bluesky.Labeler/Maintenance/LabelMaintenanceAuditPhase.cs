// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

namespace idunno.Bluesky.Labeler.Maintenance;

/// <summary>Distinguishes a maintenance coordinator's authorization intent from its known execution outcome.</summary>
public enum LabelMaintenanceAuditPhase
{
    /// <summary>The authorized coordinator intends to perform maintenance.</summary>
    Intent,
    /// <summary>The coordinator established successful completion.</summary>
    Completed,
    /// <summary>The coordinator established failure.</summary>
    Failed
}
