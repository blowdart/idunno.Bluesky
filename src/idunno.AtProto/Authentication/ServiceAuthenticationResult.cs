// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

#pragma warning disable IDE0130 // Authentication features share the protocol's root namespace.
namespace idunno.AtProto;
#pragma warning restore IDE0130

/// <summary>
/// Contains the authenticated identity and replay identifiers of a verified service token.
/// </summary>
public sealed class ServiceAuthenticationResult
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ServiceAuthenticationResult"/> class from verified claims.
    /// </summary>
    internal ServiceAuthenticationResult(
        Did issuer,
        AtProtoServiceReference audience,
        string tokenId,
        DateTimeOffset expiresAt,
        string tokenHash)
    {
        Issuer = issuer;
        Audience = audience;
        TokenId = tokenId;
        ExpiresAt = expiresAt;
        TokenHash = tokenHash;
    }

    /// <summary>
    /// Gets the authenticated issuer DID.
    /// </summary>
    public Did Issuer { get; }

    /// <summary>
    /// Gets the exact destination service authenticated by the token.
    /// </summary>
    public AtProtoServiceReference Audience { get; }

    /// <summary>
    /// Gets the token's <c>jti</c> claim.
    /// </summary>
    public string TokenId { get; }

    /// <summary>
    /// Gets the token's expiry instant, without clock skew.
    /// </summary>
    public DateTimeOffset ExpiresAt { get; }

    /// <summary>
    /// Gets the uppercase hexadecimal SHA-256 hash of the complete compact token.
    /// </summary>
    public string TokenHash { get; }
}
