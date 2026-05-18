// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.


namespace idunno.Bluesky;

/// <summary>
/// Bluesky claim types
/// </summary>
public static class ClaimTypes
{
    /// <summary>
    /// The claim name for an account's display name
    /// </summary>
    public const string DisplayName = System.Security.Claims.ClaimTypes.GivenName;

    /// <summary>
    /// The claim name for an account's handle
    /// </summary>
    public const string Handle = "urn:bluesky:handle";
}
