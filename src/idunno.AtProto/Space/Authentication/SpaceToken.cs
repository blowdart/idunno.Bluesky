// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

using idunno.AtProto.Repo;

#pragma warning disable IDE0130 // Namespace does not match folder structure.
namespace idunno.AtProto.Space;
#pragma warning restore IDE0130 // Namespace does not match folder structure.

/// <summary>Validates space tokens independently of ordinary service authentication tokens.</summary>
[Experimental("BSKYSpaces", UrlFormat = "https://bluesky.idunno.dev/docs/spaces.html")]
internal static class SpaceToken
{
    /// <summary>Verifies a delegation or credential token against the expected issuer and space.</summary>
    internal static async Task<(DateTimeOffset IssuedAt, DateTimeOffset ExpiresAt, string TokenId)> Verify(
        string token,
        SpaceAtUri space,
        Did issuer,
        Did? credentialKey,
        HttpClient httpClient,
        TimeProvider clock,
        Uri? plcDirectory,
        CancellationToken cancellationToken)
    {
        try
        {
            if (token.Length is 0 or > 8192)
            {
                throw new InvalidDataException("The space token is empty or exceeds the supported size.");
            }

            string[] parts = token.Split('.');
            if (parts.Length != 3)
            {
                throw new InvalidDataException("The space token must be a compact signed JWT.");
            }

            using JsonDocument headerDocument = JsonDocument.Parse(ServiceAuthentication.DecodeSegment(parts[0]), new JsonDocumentOptions { MaxDepth = 16 });
            using JsonDocument payloadDocument = JsonDocument.Parse(ServiceAuthentication.DecodeSegment(parts[1]), new JsonDocumentOptions { MaxDepth = 16 });
            JsonElement header = headerDocument.RootElement;
            JsonElement payload = payloadDocument.RootElement;
            ServiceAuthentication.ValidateObject(header);
            ServiceAuthentication.ValidateObject(payload);
            string type = credentialKey is null ? "atproto-space-delegation+jwt" : "atproto-space-credential+jwt";
            string algorithm = ServiceAuthentication.ReadString(header, "alg");
            if (ServiceAuthentication.ReadString(header, "typ") != type ||
                ServiceAuthentication.ReadString(header, "kid") != "#atproto" ||
                algorithm is not ("ES256" or "ES256K") ||
                header.TryGetProperty("jku", out _) || header.TryGetProperty("x5u", out _) ||
                header.TryGetProperty("jwk", out _) || header.TryGetProperty("crit", out _) || header.TryGetProperty("b64", out _) ||
                ServiceAuthentication.ReadString(payload, "iss") != issuer.Value ||
                ServiceAuthentication.ReadString(payload, "sub") != space.ToString() ||
                payload.TryGetProperty("lxm", out _))
            {
                throw new InvalidDataException("The space token's header, issuer or subject does not match the request.");
            }

            if (credentialKey is null)
            {
                if (ServiceAuthentication.ReadString(payload, "aud") != $"{space.SpaceDid}#atproto_pds" ||
                    payload.TryGetProperty("cnf", out _))
                {
                    throw new InvalidDataException("The delegation token has an invalid audience or confirmation claim.");
                }
            }
            else if (payload.TryGetProperty("aud", out _) ||
                !payload.TryGetProperty("cnf", out JsonElement confirmation) ||
                confirmation.ValueKind != JsonValueKind.Object ||
                ServiceAuthentication.ReadString(confirmation, "kid") != credentialKey.Value)
            {
                throw new InvalidDataException("The credential is not bound to the requested key or contains an audience.");
            }

            DateTimeOffset issued = ServiceAuthentication.ReadTimestamp(payload, "iat");
            DateTimeOffset expires = ServiceAuthentication.ReadTimestamp(payload, "exp");
            string tokenId = ServiceAuthentication.ReadString(payload, "jti");
            if (string.IsNullOrWhiteSpace(tokenId) || tokenId.Length > 256)
            {
                throw new InvalidDataException("The space token identifier is missing or too long.");
            }

            DateTimeOffset? notBefore = payload.TryGetProperty("nbf", out _) ? ServiceAuthentication.ReadTimestamp(payload, "nbf") : null;
            ValidateLifetime(issued: issued, expires: expires, notBefore: notBefore, delegation: credentialKey is null, clock: clock);
            DidDocument? document = await IdentityResolution.ResolveDidDocumentAsync(
                did: issuer,
                plcDirectory: plcDirectory,
                httpClient: httpClient,
                cancellationToken: cancellationToken).ConfigureAwait(false);
            SigningKey key = SigningKeyVerifier.GetKey(document, issuer, SigningKeyVerifier.RepositorySigningKeyFragment);
            if (key.JwtAlgorithm != algorithm)
            {
                throw new InvalidDataException("The space token algorithm does not match the issuer's signing key.");
            }

            key.Verify(Encoding.ASCII.GetBytes($"{parts[0]}.{parts[1]}"), ServiceAuthentication.DecodeSegment(parts[2]), "space token");
            ValidateLifetime(issued: issued, expires: expires, notBefore: notBefore, delegation: credentialKey is null, clock: clock);
            return (issued, expires, tokenId);
        }
        catch (Exception exception) when (exception is JsonException or ArgumentException or FormatException or CryptographicException)
        {
            throw new InvalidDataException("The space token could not be verified.", exception);
        }
    }

    /// <summary>Rejects expired credentials, premature use and protocol-exceeding lifetimes.</summary>
    private static void ValidateLifetime(
        DateTimeOffset issued,
        DateTimeOffset expires,
        DateTimeOffset? notBefore,
        bool delegation,
        TimeProvider clock)
    {
        DateTimeOffset now = clock.GetUtcNow();
        if (expires <= issued || expires - issued > TimeSpan.FromMinutes(delegation ? 1 : 60) ||
            issued - now > TimeSpan.FromSeconds(5) || now >= expires ||
            notBefore is { } earliest && (earliest >= expires || earliest - now > TimeSpan.FromSeconds(5)))
        {
            throw new InvalidDataException("The space token lifetime is invalid or has expired.");
        }
    }
}
