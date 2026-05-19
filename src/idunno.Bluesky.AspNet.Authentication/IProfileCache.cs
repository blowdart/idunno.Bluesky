// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using idunno.AtProto;

namespace idunno.Bluesky.AspNet.Authentication;

/// <summary>
/// Defines the interface for a cache of profile information.
/// This is used to store profile information that is retrieved from the Bluesky API and associated with a user's identity.
/// </summary>
public interface IProfileCache
{
    /// <summary>
    /// Adds a new <paramref name="profile"/> to the cache, or updates an existing cached <paramref name="profile"/>.
    /// </summary>
    /// <param name="did">The DID of the profile to add or update.</param>
    /// <param name="profile">The <see cref="ProfileCacheEntry"/> to add or update in the cache.</param>
    public Task Add(Did did, ProfileCacheEntry profile);

    /// <summary>
    /// Retrieves a cached <see cref="ProfileCacheEntry"/> for the specified <paramref name="did"/>.
    /// </summary>
    /// <param name="did">The <see cref="Did"/> of the profile to retrieve.</param>
    /// <returns>The cached <see cref="ProfileCacheEntry"/>, or <see langword="null"/> if no cached profile exists.</returns>
    public Task<ProfileCacheEntry?> GetCachedValue(Did did);
}
