// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using idunno.AtProto.Authentication;

namespace idunno.Bluesky.AspNet.Authentication;

/// <summary>
/// Defines the operations necessary for an authentication state cache.
/// </summary>
public interface IBlueskyAuthenticationCorrelationCache
{
    /// <summary>
    /// Adds the specified <paramref name="state"/> to the authentication state cache 
    /// using the <paramref name="correlationId"/> as a key.
    /// </summary>
    /// <param name="correlationId">The key to use</param>
    /// <param name="state">The state to store.</param>
    Task AddOAuthLoginState(Guid correlationId, OAuthLoginState state);

    /// <summary>
    /// Retrieves the <see cref="OAuthLoginState"/> from the authentication state cache
    /// for the <paramref name="correlationId"/>.
    /// </summary>
    /// <param name="correlationId">The key to retrieve the <see cref="OAuthLoginState"/> for.</param>
    /// <returns>The <see cref="OAuthLoginState"/> for the key if it was in the cache, otherwise <see langword="null"/>.</returns>
    Task<OAuthLoginState?> GetOAuthLoginState(Guid correlationId);
}
