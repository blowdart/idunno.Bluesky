// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;

#pragma warning disable IDE0130 // Namespace does not match folder structure.
namespace idunno.AtProto.Space;
#pragma warning restore IDE0130 // Namespace does not match folder structure.

/// <summary>Represents a verified, single-use user delegation to a space authority.</summary>
/// <remarks>
/// <para>Authorization consumes the token even if the request fails, because a transport failure cannot establish
/// whether the authority received it. Obtain another token before retrying. This token contains no membership grant.</para>
/// </remarks>
[Experimental("BSKYSpaces", UrlFormat = "https://bluesky.idunno.dev/docs/spaces.html")]
public sealed class SpaceDelegationToken
{
    private string? _token;
    private readonly TimeProvider _clock;

    /// <summary>Initializes a new instance of the <see cref="SpaceDelegationToken"/> class with a verified token.</summary>
    internal SpaceDelegationToken(
        string token,
        SpaceAtUri space,
        Did issuer,
        DateTimeOffset expiresAt,
        TimeProvider clock)
    {
        _token = token;
        Space = space;
        Issuer = issuer;
        ExpiresAt = expiresAt;
        _clock = clock;
    }

    /// <summary>Gets the space for which delegation was granted.</summary>
    /// <value>The verified target space reference.</value>
    public SpaceAtUri Space { get; }

    /// <summary>Gets the DID of the user on whose behalf the application acts.</summary>
    /// <value>The verified delegation issuer.</value>
    public Did Issuer { get; }

    /// <summary>Gets the time at which the token becomes unusable.</summary>
    /// <value>The issuer's signed expiry time.</value>
    public DateTimeOffset ExpiresAt { get; }

    /// <summary>Gets a value that indicates whether the token has expired.</summary>
    /// <value><see langword="true"/> if the expiry time has been reached; otherwise, <see langword="false"/>.</value>
    public bool IsExpired => _clock.GetUtcNow() >= ExpiresAt;

    /// <summary>Gets a value that indicates whether an authorization attempt has consumed the token.</summary>
    /// <value><see langword="true"/> if the secret has been consumed or released after expiry; otherwise, <see langword="false"/>.</value>
    public bool IsUsed => Volatile.Read(ref _token) is null;

    /// <summary>Atomically consumes a live token after validating its exact target space.</summary>
    internal string Consume(SpaceAtUri space)
    {
        if (space != Space)
        {
            throw new ArgumentException("The delegation token belongs to another space.", nameof(space));
        }

        if (IsExpired)
        {
            Interlocked.Exchange(ref _token, null);
            throw new InvalidOperationException("The delegation token has expired. Request another token.");
        }

        return Interlocked.Exchange(ref _token, null) ??
            throw new InvalidOperationException("The delegation token has already been used. Request another token.");
    }
}
