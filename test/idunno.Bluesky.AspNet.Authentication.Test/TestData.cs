// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Security.Claims;
using System.Text;

using Duende.IdentityModel.OidcClient;
using Duende.IdentityModel.OidcClient.DPoP;

using idunno.AtProto;
using idunno.AtProto.Authentication;

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

    /// <summary>
    /// Builds a <see cref="System.Security.Claims.ClaimsIdentity"/> carrying unexpired DPoP credentials, of the shape
    /// a <see cref="idunno.Bluesky.BlueskyAgent"/> will consider authenticated.
    /// </summary>
    /// <param name="did">The <see cref="Did"/> the identity is for.</param>
    /// <param name="authenticationType">The authentication type, which is the name of the scheme which issued the identity.</param>
    /// <param name="signableProofKey">
    ///   Whether the DPoP proof key should be a real JSON web key. A request which is actually sent has a DPoP proof
    ///   signed with this key, so anything reaching the network needs one, but generating a key is expensive enough to
    ///   be worth skipping for the tests which do not.
    /// </param>
    internal static ClaimsIdentity AuthenticatedClaimsIdentity(
        Did did,
        string authenticationType = "Bluesky",
        bool signableProofKey = false)
    {
        // The service comes from the issuer of the DID claim, and the expiry from the access token, so both have to
        // be set for AtProtoCredential.TryCreate to produce credentials which have not expired.
        List<Claim> claims =
        [
            new Claim(AtProtoClaims.Did, did, ClaimValueTypes.String, "https://bsky.social"),
            new Claim(AtProtoClaims.AccessToken, Jwt(did), ClaimValueTypes.String, "https://bsky.social"),
            new Claim(AtProtoClaims.RefreshToken, Jwt(did), ClaimValueTypes.String, "https://bsky.social"),
            new Claim(
                AtProtoClaims.DPoPProof,
                signableProofKey ? JsonWebKeys.CreateRsaJson() : "proof-key",
                ClaimValueTypes.String,
                "https://bsky.social"),
            new Claim(AtProtoClaims.DPoPNonce, "nonce", ClaimValueTypes.String, "https://bsky.social"),
        ];

        return new ClaimsIdentity(claims, authenticationType);
    }

    /// <summary>
    /// The OAuth state parameter the login state returned by <see cref="LoginState"/> carries. A callback has to carry
    /// the same value for the correlation cookie belonging to that login to be found.
    /// </summary>
    internal const string OAuthState = "oauth-state";

    internal static OAuthLoginState LoginState(
        Guid correlationId,
        string codeVerifier = "code-verifier",
        string oauthState = OAuthState) =>        new(
            state: new AuthorizeState
            {
                StartUrl = "https://bsky.social/oauth/authorize?client_id=test",
                State = oauthState,
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

    /// <summary>
    /// Builds an unsigned token in JWS compact serialization with the supplied lifetime.
    /// </summary>
    /// <remarks>
    /// The SDK reads the expiry out of an access token rather than validating it, so a token only has to be well
    /// formed and carry an <c>exp</c> claim for code which depends on credentials not having expired to run.
    /// </remarks>
    internal static string Jwt(Did did, TimeSpan? lifetime = null)
    {
        DateTimeOffset issuedAt = DateTimeOffset.UtcNow;
        DateTimeOffset expiresAt = issuedAt.Add(lifetime ?? TimeSpan.FromHours(1));

        string header = Base64UrlEncode("""{"alg":"none","typ":"JWT"}""");
        string payload = Base64UrlEncode(
            $$"""{"iss":"https://bsky.social","sub":"{{did}}","iat":{{issuedAt.ToUnixTimeSeconds()}},"exp":{{expiresAt.ToUnixTimeSeconds()}}}""");

        return $"{header}.{payload}.{Base64UrlEncode("not-a-real-signature")}";

        static string Base64UrlEncode(string value) =>
            Convert.ToBase64String(Encoding.UTF8.GetBytes(value)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }
}
