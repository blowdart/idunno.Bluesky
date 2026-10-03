// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace idunno.Bluesky.AspNet.Authentication;

internal sealed record OAuthClientMetadata(
    [property: JsonPropertyName("client_id")] string ClientId,
    [property: JsonPropertyName("redirect_uris")] string[] RedirectUris,
    [property: JsonPropertyName("scope")] string Scope,
    [property: JsonPropertyName("client_name")] string? ClientName,
    [property: JsonPropertyName("client_uri")] string? ClientUri,
    [property: JsonPropertyName("logo_uri")] string? LogoUri,
    [property: JsonPropertyName("tos_uri")] string? TosUri,
    [property: JsonPropertyName("policy_uri")] string? PolicyUri,
    [property: JsonPropertyName("token_endpoint_auth_method")] string TokenEndpointAuthMethod,
    [property: JsonPropertyName("token_endpoint_auth_signing_alg")] string? TokenEndpointAuthSigningAlgorithm,
    [property: JsonPropertyName("jwks")] OAuthClientJsonWebKeySet? JsonWebKeySet)
{
    [JsonPropertyName("application_type")]
    public string ApplicationType { get; } = "web";

    [JsonPropertyName("grant_types")]
    public string[] GrantTypes { get; } = ["authorization_code", "refresh_token"];

    [JsonPropertyName("response_types")]
    public string[] ResponseTypes { get; } = ["code"];

    [JsonPropertyName("dpop_bound_access_tokens")]
    public bool DPoPBoundAccessTokens { get; } = true;
}

internal sealed record OAuthClientJsonWebKeySet(
    [property: JsonPropertyName("keys")] OAuthClientJsonWebKey[] Keys);

internal sealed record OAuthClientJsonWebKey(
    [property: JsonPropertyName("kty")] string KeyType,
    [property: JsonPropertyName("crv")] string Curve,
    [property: JsonPropertyName("x")] string X,
    [property: JsonPropertyName("y")] string Y,
    [property: JsonPropertyName("kid")] string KeyId,
    [property: JsonPropertyName("alg")] string Algorithm,
    [property: JsonPropertyName("use")] string Use);