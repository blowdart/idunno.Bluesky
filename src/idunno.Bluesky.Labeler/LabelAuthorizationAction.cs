// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

namespace idunno.Bluesky.Labeler;

/// <summary>Identifies the service operation for which authorization is requested.</summary>
public enum LabelAuthorizationAction
{
    /// <summary>Authorizes a label issuance request.</summary>
    Issue,

    /// <summary>Authorizes a label negation request.</summary>
    Negate,

    /// <summary>Authorizes inspection of an operation receipt.</summary>
    InspectOperation,

    /// <summary>Authorizes inspection of publication state.</summary>
    InspectPublication
}
