// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Security.Claims;

using idunno.AtProto;
using idunno.AtProto.Authentication;

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
    /// Renews the specified <paramref name="identity"/> in the identity store.
    /// </summary>
    /// <param name="identity">The <see cref="ClaimsIdentity"/> to renew</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    Task Renew(ClaimsIdentity identity) => Renew(identity, default);

   /// <summary>
    /// Renews the specified <paramref name="identity"/> in the identity store.
    /// </summary>
    /// <param name="identity">The <see cref="ClaimsIdentity"/> to renew</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    Task Renew(ClaimsIdentity identity, CancellationToken cancellationToken);

    /// <summary>
    /// Renews the specified <paramref name="credentials"/> in the identity store.
    /// </summary>
    /// <param name="credentials">The <see cref="DPoPAccessCredentials"/> to renew</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    Task Renew(DPoPAccessCredentials credentials) => Renew(credentials, default);

    /// <summary>
    /// Renews the specified <paramref name="credentials"/> in the identity store.
    /// </summary>
    /// <param name="credentials">The <see cref="DPoPAccessCredentials"/> to renew</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    /// <exception cref="System.ArgumentNullException">Thrown when <paramref name="credentials"/> is <see langword="null" />.</exception>
    Task Renew(DPoPAccessCredentials credentials, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(credentials);
        return Renew(BuildClaimsIdentity(credentials), cancellationToken);
    }

    /// <summary>
    /// Builds a <see cref="ClaimsIdentity"/> from the specified <see cref="DPoPAccessCredentials"/>.
    /// </summary>
    /// <param name="credentials">The <see cref="DPoPAccessCredentials"/> to build an identity from.</param>
    /// <param name="scheme">The authentication scheme.</param>
    /// <returns>A <see cref="ClaimsIdentity"/> from the specified <see cref="DPoPAccessCredentials"/></returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="credentials"/> is <see langword="null" />.</exception>
    static ClaimsIdentity BuildClaimsIdentity(DPoPAccessCredentials credentials, string? scheme = null)
    {
        ArgumentNullException.ThrowIfNull(credentials);

        scheme ??= BlueskyAuthenticationDefaults.AuthenticationScheme;

        ClaimsIdentity identity = new(
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
            new Claim(
                AtProtoClaims.DPoPProof,
                credentials.DPoPProofKey,
                ClaimValueTypes.String,
                credentials.Service.ToString()),
            new Claim(
                AtProtoClaims.DPoPNonce,
                credentials.DPoPNonce,
                ClaimValueTypes.String,
                credentials.Service.ToString()),
        ],
        authenticationType: scheme);

        return identity;
    }
}
