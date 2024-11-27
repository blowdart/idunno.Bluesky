// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

namespace idunno.AtProto
{
    /// <summary>
    /// Used for API calls which do not return a response.
    /// </summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Minor Code Smell", "S2094:Classes should not be empty", Justification = "Deliberately empty because an API that doesn't have response still needs to return an object.")]
    public sealed record EmptyResponse
    {
    }
}
