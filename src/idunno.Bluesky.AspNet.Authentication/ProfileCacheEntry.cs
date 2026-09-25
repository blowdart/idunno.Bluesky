// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using idunno.AtProto;
using idunno.Bluesky.Actor;

namespace idunno.Bluesky.AspNet.Authentication;

/// <summary>
/// Defines an entry in the display name cache.
/// </summary>
/// <param name="Handle">The handle of the profile.</param>
/// <param name="DisplayName">The display name of the profile.</param>
/// <param name="Description">The description of the profile.</param>
/// <param name="Pronouns">The pronouns of the profile.</param>
/// <param name="Website">The website of the profile.</param>
/// <param name="Avatar">The avatar of the profile.</param>
/// <param name="Banner">The banner of the profile.</param>
/// <param name="Issuer">The issuer of the profile.</param>
public sealed record ProfileCacheEntry(
    Handle? Handle,
    string? DisplayName,
    string? Description,
    string? Pronouns,
    Uri? Website,
    Uri? Avatar,
    Uri? Banner,
    string Issuer)
{
    internal ProfileCacheEntry(ProfileViewDetailed profileViewDetailed, string issuer) : this(
        Handle: profileViewDetailed.Handle,
        DisplayName: profileViewDetailed.DisplayName,
        Description: profileViewDetailed.Description,
        Pronouns: profileViewDetailed.Pronouns,
        Website: profileViewDetailed.Website,
        Avatar: profileViewDetailed.Avatar,
        Banner: profileViewDetailed.Banner,
        Issuer: issuer)
    {
    }

    /// <summary>
    /// Creates a new instance of the <see cref="ProfileCacheEntry"/> class from the specified <paramref name="profileView"/> and <paramref name="issuer"/>.
    /// </summary>
    /// <param name="profileView">The profile view to build the cache entry from.</param>
    /// <param name="issuer">The issuer of the profile.</param>
    internal ProfileCacheEntry(ProfileView profileView, string issuer) : this(
        Handle: profileView.Handle,
        DisplayName: profileView.DisplayName,
        Description: profileView.Description,
        Pronouns: profileView.Pronouns,
        Website: profileView.Website,
        Avatar: profileView.Avatar,
        Banner: null,
        Issuer: issuer)
    {
    }
}
