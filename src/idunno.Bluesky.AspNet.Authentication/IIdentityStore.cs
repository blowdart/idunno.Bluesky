// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Security.Claims;

using idunno.AtProto;
using idunno.AtProto.Authentication;
using idunno.AtProto.Events;
using idunno.Bluesky.AspNet.Authentication.Events;

namespace idunno.Bluesky.AspNet.Authentication;

/// <summary>
/// Defines the methods needed to store an identity and its claims server side.
/// </summary>
public interface IIdentityStore
{
    /// <summary>
    /// Gets or sets the <see cref="IdentityStoreEvents"/> the store raises as identities are stored and retrieved.
    /// </summary>
    /// <remarks>
    /// <para>
    ///   This is set from <see cref="BlueskyAuthenticationOptions.IdentityStoreEvents"/> when the options for an
    ///   authentication scheme are built. It cannot be read from the options by a store's constructor, because the options
    ///   are configured named by scheme and a constructor only has access to the unnamed instance.
    /// </para>
    /// <para>
    ///   A store instance should not be shared between authentication schemes which configure different events, as the
    ///   scheme whose options are built last would win.
    /// </para>
    /// <para>
    ///   A store which does not serialize the identities it holds, such as <see cref="EphemeralIdentityStore"/>, has nothing
    ///   to hand to the events and may ignore this property.
    /// </para>
    /// </remarks>
    IdentityStoreEvents Events { get; set; }

    /// <summary>
    /// Adds the specified <see cref="ClaimsIdentity"/> to the identity store.
    /// </summary>
    /// <param name="claimsIdentity">The <see cref="ClaimsIdentity"/> to add to the store.</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    Task Add(ClaimsIdentity claimsIdentity, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves the <see cref="ClaimsIdentity"/> for the specified <see cref="Did"/> from the store. If the <see cref="Did"/> is not found, returns <see langword="null"/>.
    /// </summary>
    /// <param name="did">The <see cref="Did"/> whose <see cref="ClaimsIdentity"/> should be retrieved from the store.</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    Task<ClaimsIdentity?> GetIdentity(Did did, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes the cached identity from the identity store for the specified <see cref="Did"/>.
    /// </summary>
    /// <param name="did">The <see cref="Did"/> whose <see cref="ClaimsIdentity"/> should be removed from the store</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    Task Remove(Did did, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates the specified <paramref name="identity"/> in the identity store.
    /// </summary>
    /// <param name="identity">The <see cref="ClaimsIdentity"/> to update</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    Task Update(ClaimsIdentity identity) => Update(identity, default);

    /// <summary>
    /// Updates the specified <paramref name="identity"/> in the identity store.
    /// </summary>
    /// <param name="identity">The <see cref="ClaimsIdentity"/> to update</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    Task Update(ClaimsIdentity identity, CancellationToken cancellationToken);

    /// <summary>
    /// Updates the specified <paramref name="credentials"/> in the identity store.
    /// </summary>
    /// <param name="credentials">The <see cref="AccessCredentials"/> to update</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    Task Update(AccessCredentials credentials) => Update(credentials, default);

    /// <summary>
    /// Updates the specified <paramref name="credentials"/> in the identity store.
    /// </summary>
    /// <param name="credentials">The <see cref="AccessCredentials"/> to update</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    /// <exception cref="System.ArgumentNullException">Thrown when <paramref name="credentials"/> is <see langword="null" />.</exception>
    Task Update(AccessCredentials credentials, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(credentials);
        return Update(BuildClaimsIdentity(credentials), cancellationToken);
    }

    /// <summary>
    /// Marks the specified <paramref name="did"/> as starting the refresh token process. This is used to prevent multiple refreshes from occurring at the same time.
    /// </summary>
    /// <param name="did">The <see cref="Did"/> to mark as being refreshed.</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    /// <returns>
    ///   The task object representing the asynchronous operation. The task result contains a token identifying the caller's ownership of the refresh lock
    ///   if the caller can start a refresh flow; otherwise <see langword="null"/>.
    /// </returns>
    /// <remarks>
    /// <para>
    ///   Implementations must acquire the lock atomically. AT Proto refresh tokens are single use, so if two callers are allowed to refresh the same
    ///   <see cref="Did"/> concurrently the loser's refresh will fail against a token the winner has already consumed.
    /// </para>
    /// <para>
    ///   The returned token must be passed to <see cref="EndRefresh(Did, string?, CancellationToken)"/> so that a caller can only release a lock it still
    ///   owns. Without it a caller whose lock had already expired and been re-acquired by someone else would release the new owner's lock.
    /// </para>
    /// </remarks>
    Task<string?> StartRefresh(Did did, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes the specified <paramref name="did"/> from the list of DIDs being refreshed. This is used to prevent multiple refreshes from occurring at the same time.
    /// </summary>
    /// <param name="did">The <see cref="Did"/> to remove from the list of DIDs being refreshed.</param>
    /// <param name="refreshLockToken">The token returned by <see cref="StartRefresh(Did, CancellationToken)"/> when the lock was acquired.</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    /// <returns>
    ///   The task object representing the asynchronous operation. The task result contains <see langword="true"/> if the caller still owned the lock and it
    ///   was released; otherwise <see langword="false"/>.
    /// </returns>
    /// <remarks>
    /// <para>
    ///   Implementations must only release the lock if <paramref name="refreshLockToken"/> matches the token currently held against <paramref name="did"/>,
    ///   otherwise a caller whose lock expired mid refresh would release a lock another caller has since acquired.
    /// </para>
    /// <para>
    ///   A <see langword="false"/> result means the caller's lock expired while it was refreshing and, if the refresh itself succeeded, that another caller
    ///   may have been refreshing the same <see cref="Did"/> at the same time. Implementations should perform the comparison and the release as a single
    ///   atomic operation, so that the result reflects what actually happened rather than what was the case when the token was last read.
    /// </para>
    /// </remarks>
    Task<bool> EndRefresh(Did did, string? refreshLockToken, CancellationToken cancellationToken = default);

    /// <summary>
    /// Determines if the specified <paramref name="did"/> is currently being refreshed. This is used to prevent multiple refreshes from occurring at the same time.
    /// </summary>
    /// <param name="did">The <see cref="Did"/> to check.</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains <see langword="true"/> if the specified <paramref name="did"/> is currently being refreshed; otherwise, <see langword="false"/>.</returns>
    Task<bool> IsRefreshing(Did did, CancellationToken cancellationToken = default);

    /// <summary>
    /// Called by an agent when its credentials are updated.
    /// A store should use this to update the cached identity for the specified <see cref="Did"/> with the new credentials.
    /// </summary>
    /// <param name="e">The <see cref="CredentialsUpdatedEventArgs"/> instance containing the event data.</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    /// <remarks>
    /// <para>
    /// This is awaited by the agent, so a failure to persist the updated credentials propagates to the caller rather than
    /// being silently lost. AT Proto refresh tokens are single use, so a swallowed failure here would leave the store
    /// holding a refresh token the service has already invalidated.
    /// </para>
    /// <para>
    /// An agent raises this for a rotated DPoP nonce as well as for a token refresh, and it writes back the whole credential,
    /// not just the part which changed. A request which picks up a nonce is not holding the refresh lock, so the stored
    /// identity is read back first and left alone when it carries a later expiry than the credentials being written. Without
    /// that check a long running request could restore the tokens it started with over a refresh another request had already
    /// stored, putting back a refresh token the service had spent and signing the user out at the next refresh. The nonce is
    /// dropped in that case rather than merged, which costs at most one <c>use_dpop_nonce</c> challenge on a later request.
    /// </para>
    /// </remarks>
    public virtual async Task OnCredentialsUpdated(CredentialsUpdatedEventArgs e, CancellationToken cancellationToken = default)
    {
        if (e is null || e.AccessCredentials is null)
        {
            return;
        }

        await UpdateIfNewer(e.AccessCredentials, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Updates the specified <paramref name="credentials"/> in the identity store, unless the store already holds credentials which expire later.
    /// </summary>
    /// <param name="credentials">The <see cref="AccessCredentials"/> to update.</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    /// <returns>
    ///   The task object representing the asynchronous operation. The task result contains <see langword="true"/> if the credentials were written;
    ///   otherwise <see langword="false"/>, meaning the store already held a later set of credentials which were left in place.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="credentials"/> is <see langword="null" />.</exception>
    /// <remarks>
    /// <para>
    ///   The refresh lock is advisory. It stops two callers starting a refresh at the same time, but nothing revalidates it at the point the resulting
    ///   credentials are written, so a caller whose lock expired mid refresh can still reach the write. This resolves that race on expiry rather than on
    ///   lock ownership, so the newest credentials win regardless of which caller produced them.
    /// </para>
    /// <para>
    ///   Refusing the write instead would be worse than allowing it. AT Proto refresh tokens are single use, so once a refresh has succeeded the tokens it
    ///   returned are the only usable ones and the tokens they replaced are already spent. Discarding them because a lock had expired would throw away the
    ///   only credentials which still work and sign the user out.
    /// </para>
    /// </remarks>
    async Task<bool> UpdateIfNewer(AccessCredentials credentials, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(credentials);

        ClaimsIdentity? storedIdentity = await GetIdentity(credentials.Did, cancellationToken).ConfigureAwait(false);

        if (storedIdentity is not null &&
            AtProtoCredential.TryCreate(storedIdentity, out DPoPAccessCredentials? storedCredentials) &&
            storedCredentials is not null &&
            storedCredentials.ExpiresOn > credentials.ExpiresOn)
        {
            return false;
        }

        await Update(credentials, cancellationToken).ConfigureAwait(false);

        return true;
    }

    /// <summary>
    /// Builds a <see cref="ClaimsIdentity"/> from the specified <see cref="AccessCredentials"/>.
    /// </summary>
    /// <param name="credentials">The <see cref="AccessCredentials"/> to build an identity from.</param>
    /// <param name="scheme">The authentication scheme.</param>
    /// <returns>A <see cref="ClaimsIdentity"/> from the specified <see cref="AccessCredentials"/></returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="credentials"/> is <see langword="null" />.</exception>
    static ClaimsIdentity BuildClaimsIdentity(AccessCredentials credentials, string? scheme = null)
    {
        ArgumentNullException.ThrowIfNull(credentials);

        scheme ??= BlueskyAuthenticationDefaults.AuthenticationScheme;

        List<Claim> claims =
        [
            // A stable, unique identifier for the authenticated user. ASP.NET Core antiforgery uses the
            // NameIdentifier claim to fingerprint the user in its tokens. Without it, antiforgery falls back to
            // hashing every claim, including the rotating access/refresh tokens and DPoP nonce, which makes a
            // token issued while signed in fail validation once those values change (an HTTP 400 on POST).
            new Claim(
                System.Security.Claims.ClaimTypes.NameIdentifier,
                credentials.Did,
                ClaimValueTypes.String,
                credentials.Service.ToString()),
            new Claim(
                AtProtoClaims.Did,
                credentials.Did,
                ClaimValueTypes.String,
                credentials.Service.ToString()),
            new Claim(
                AtProtoClaims.AccessToken,
                credentials.AccessJwt,
                ClaimValueTypes.String,
                credentials.Service.ToString()),
            new Claim(
                AtProtoClaims.RefreshToken,
                credentials.RefreshToken,
                ClaimValueTypes.String,
                credentials.Service.ToString()),
        ];

        if (credentials is DPoPAccessCredentials dPoPAccessCredentials)
        {
            claims.Add(
                new Claim(
                    AtProtoClaims.DPoPProof,
                    dPoPAccessCredentials.DPoPProofKey,
                    ClaimValueTypes.String,
                    credentials.Service.ToString()));
            claims.Add(
                new Claim(
                    AtProtoClaims.DPoPNonce,
                    dPoPAccessCredentials.DPoPNonce,
                    ClaimValueTypes.String,
                    credentials.Service.ToString()));
        }

        ClaimsIdentity identity = new(
            claims: claims,
            authenticationType: scheme);

        return identity;
    }
}
