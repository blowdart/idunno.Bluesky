// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.


namespace idunno.AtProto.Repo
{
    /// <summary>
    /// A base record for AT Proto objects.
    /// </summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Minor Code Smell", "S2094:Classes should not be empty", Justification = "Starts out as an empty record for json serialization & deserialization")]
    public abstract record AtProtoObject
    {
    }
}
