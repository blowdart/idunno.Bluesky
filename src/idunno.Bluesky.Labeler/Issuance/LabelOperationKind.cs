// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

namespace idunno.Bluesky.Labeler.Issuance;

/// <summary>Identifies the decision requested by a label operation.</summary>
public enum LabelOperationKind
{
    /// <summary>Adds or refreshes a positive label decision.</summary>
    Issue,

    /// <summary>Withdraws a prior label decision by issuing a negation.</summary>
    Negate
}
