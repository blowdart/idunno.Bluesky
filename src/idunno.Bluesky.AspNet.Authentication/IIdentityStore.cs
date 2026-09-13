// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Security.Claims;

using idunno.AtProto;
using idunno.AtProto.Authentication;
using idunno.AtProto.Events;

namespace idunno.Bluesky.AspNet.Authentication;

/// <summary>
/// Defines the methods needed to store an identity and its claims server side.
/// </summary>
public interface IIdentityStore
{
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
    /// <returns>The task object representing the asynchronous operation.</returns>
    /// <remarks>
    /// <para>
    ///   Implementations must only release the lock if <paramref name="refreshLockToken"/> matches the token currently held against <paramref name="did"/>,
    ///   otherwise a caller whose lock expired mid refresh would release a lock another caller has since acquired.
    /// </para>
    /// </remarks>
    Task EndRefresh(Did did, string? refreshLockToken, CancellationToken cancellationToken = default);

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
    /// </remarks>
    public virtual Task OnCredentialsUpdated(CredentialsUpdatedEventArgs e, CancellationToken cancellationToken = default)
    {
        if (e is not null && e.AccessCredentials is not null)
        {
            return Update(e.AccessCredentials, cancellationToken);
        }

        return Task.CompletedTask;
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
