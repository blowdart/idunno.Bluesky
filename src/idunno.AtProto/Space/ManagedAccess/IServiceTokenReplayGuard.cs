// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;

#pragma warning disable IDE0130 // Namespace does not match folder structure.
namespace idunno.AtProto.Space;
#pragma warning restore IDE0130 // Namespace does not match folder structure.

/// <summary>
/// Reserves verified service tokens so that each token can be accepted only once.
/// </summary>
/// <remarks>
/// <para>Implementations must atomically reserve both the issuer and token identifier pair and the token hash, and retain
/// each reservation until the token's expiry plus clock skew. Applications running more than one instance must use shared
/// storage.</para>
/// </remarks>
[Experimental("BSKYSpaces", UrlFormat = "https://bluesky.idunno.dev/docs/spaces.html")]
public interface IServiceTokenReplayGuard
{
    /// <summary>
    /// Attempts to reserve a verified service token.
    /// </summary>
    /// <param name="token">The verified service token.</param>
    /// <param name="cancellationToken">A cancellation token for the operation.</param>
    /// <returns><see langword="true"/> if the token was reserved; otherwise, <see langword="false"/> when it was already used or cannot be reserved.</returns>
    ValueTask<bool> TryReserve(ServiceAuthenticationResult token, CancellationToken cancellationToken);
}
