// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

namespace idunno.Bluesky.Labeler;

/// <summary>Identifies the kind of subject targeted by a label operation.</summary>
public enum LabelTargetKind
{
    /// <summary>Targets an account identity.</summary>
    Account,

    /// <summary>Targets one record in an AT Protocol repository.</summary>
    Record
}
