// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Security.Claims;

using idunno.AtProto;
using idunno.AtProto.Authentication;

using Duende.IdentityModel.OidcClient;

using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace idunno.Bluesky.AspNet.Authentication.Test;

/// <summary>
/// Builders for the values the identity store and correlation cache tests operate on.
/// </summary>
/// <remarks>
/// The ephemeral store and cache are backed by process wide static caches, so every test works against a freshly
/// generated key. That keeps tests from seeing each other's entries without needing a reset hook on the production types.
/// </remarks>
internal static class TestData
{
    internal static Did NewDid() => new($"did:plc:{Guid.NewGuid():N}");

    internal static ClaimsIdentity ClaimsIdentity(Did did, string? accessToken = null, string authenticationType = "Bluesky")
    {
        List<Claim> claims =
        [
            new Claim(AtProtoClaims.Did, did, ClaimValueTypes.String, "https://bsky.social"),
            new Claim(AtProtoClaims.AccessToken, accessToken ?? "access-token", ClaimValueTypes.String, "https://bsky.social"),
            new Claim(AtProtoClaims.RefreshToken, "refresh-token", ClaimValueTypes.String, "https://bsky.social"),
        ];

        return new ClaimsIdentity(claims, authenticationType);
    }

    internal static OAuthLoginState LoginState(Guid correlationId, string codeVerifier = "code-verifier") =>
        new(
            state: new AuthorizeState
            {
                StartUrl = "https://bsky.social/oauth/authorize?client_id=test",
                State = "oauth-state",
                CodeVerifier = codeVerifier,
                RedirectUri = "https://localhost/signin-bluesky",
                Error = string.Empty,
                ErrorDescription = string.Empty,
            },
            expectedAuthority: "https://bsky.social",
            expectedService: "https://bsky.social",
            proofKey: "proof-key",
            correlationId: correlationId);

    internal static IDistributedCache DistributedCache() =>
        new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));
}
