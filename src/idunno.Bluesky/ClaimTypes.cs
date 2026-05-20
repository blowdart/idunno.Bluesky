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

    /// <summary>
    /// The claim name for an account's description
    /// </summary>
    public const string Description = "urn:bluesky:description";

    /// <summary>
    /// The claim name for an account's pronouns
    /// </summary>
    public const string Pronouns = "urn:bluesky:pronouns";

    /// <summary>
    /// The claim name for an accounts website url
    /// </summary>
    public const string Website = "urn:bluesky:website";

    /// <summary>
    /// The claim name for an account's avatar URL
    /// </summary>
    public const string Avatar = "urn:bluesky:avatar";

    /// <summary>
    /// The claim name for an account's banner URL
    /// </summary>
    public const string Banner = "urn:bluesky:banner";
}
