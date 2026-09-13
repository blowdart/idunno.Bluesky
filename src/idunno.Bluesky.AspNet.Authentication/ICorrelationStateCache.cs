// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using idunno.AtProto.Authentication;
using idunno.Bluesky.AspNet.Authentication.Events;

namespace idunno.Bluesky.AspNet.Authentication;

/// <summary>
/// Defines the operations necessary for an authentication state cache.
/// </summary>
public interface ICorrelationStateCache
{
    /// <summary>
    /// Gets or sets the <see cref="CorrelationStateCacheEvents"/> the cache raises as state is stored and retrieved.
    /// </summary>
    /// <remarks>
    /// <para>
    ///   This is set from <see cref="BlueskyAuthenticationOptions.CorrelationStateCacheEvents"/> when the options for an
    ///   authentication scheme are built. It cannot be read from the options by a cache's constructor, because the options
    ///   are configured named by scheme and a constructor only has access to the unnamed instance.
    /// </para>
    /// <para>
    ///   A cache instance should not be shared between authentication schemes which configure different events, as the
    ///   scheme whose options are built last would win.
    /// </para>
    /// </remarks>
    CorrelationStateCacheEvents Events { get; set; }

    /// <summary>
    /// Adds the specified <paramref name="state"/> to the authentication state cache 
    /// using the <paramref name="correlationId"/> as a key.
    /// </summary>
    /// <param name="correlationId">The key to use</param>
    /// <param name="state">The state to store.</param>
    /// <returns>A task that represents the asynchronous addoperation.</returns>
    Task AddOAuthLoginState(Guid correlationId, OAuthLoginState state);

    /// <summary>
    /// Retrieves the <see cref="OAuthLoginState"/> from the authentication state cache
    /// for the <paramref name="correlationId"/>.
    /// </summary>
    /// <param name="correlationId">The key to retrieve the <see cref="OAuthLoginState"/> for.</param>
    /// <returns>The <see cref="OAuthLoginState"/> for the key if it was in the cache, otherwise <see langword="null"/>.</returns>
    Task<OAuthLoginState?> GetOAuthLoginState(Guid correlationId);

    /// <summary>
    /// Retrieves the <see cref="OAuthLoginState"/> for the <paramref name="correlationId"/> from the authentication
    /// state cache and removes it, so it can only be consumed once.
    /// </summary>
    /// <param name="correlationId">The key to take the <see cref="OAuthLoginState"/> for.</param>
    /// <returns>The <see cref="OAuthLoginState"/> for the key if it was in the cache, otherwise <see langword="null"/>.</returns>
    /// <remarks>
    /// <para>
    ///   Login state is single use. Calling <see cref="GetOAuthLoginState(Guid)"/> and then
    ///   <see cref="RemoveCorrelationState(Guid)"/> leaves a window in which two callbacks carrying the same correlation
    ///   identifier can both be given the state, so callers consuming a callback should use this instead.
    /// </para>
    /// <para>
    ///   The entry is removed whether or not the state it held could be read, so an entry which cannot be deserialized or
    ///   unprotected does not survive to be presented again.
    /// </para>
    /// </remarks>
    Task<OAuthLoginState?> TakeOAuthLoginState(Guid correlationId);

    /// <summary>
    /// Removes the correlation state for the specified <paramref name="correlationId"/> from the cache.
    /// </summary>
    /// <param name="correlationId">The key to remove the <see cref="OAuthLoginState"/> for.</param>
    /// <returns>A task that represents the asynchronous remove operation.</returns>
    Task RemoveCorrelationState(Guid correlationId);
}
