// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace idunno.AtProto.Authentication
{
    /// <summary>
    /// A class for exposing metadata about an authorization server retrieved from an OpenID Connect discovery point.
    /// </summary>
    /// <remarks>
    /// <para><see href="https://openid.net/specs/openid-connect-discovery-1_0.html">OpenID Connect Discovery 1.0</see> specification.</para>
    /// </remarks>
    [SuppressMessage("Performance", "CA1812", Justification = "Used in OIDC discovery.")]
    internal sealed record AuthorizationMetadata
    {
        /// <summary>
        /// URI using the HTTPS scheme with no query or fragment components that the provider asserts as its Issuer Identifier.
        ///This also MUST be identical to the iss Claim value in ID Tokens issued from this Issuer.
        /// </summary>
        public Uri? Issuer { get; init; }

        /// <summary>
        /// A list of the Subject Identifier types that the provider supports. Valid types include pairwise and public.
        /// </summary>
        [JsonPropertyName("subject_types_supported")]
        public IList<string> SupportedSubjectTypes { get; init; } = new List<string>();

        /// <summary>
        /// A list of the OAuth 2.0 scope values that the provider supports.
        /// </summary>
        /// <remarks><para><see href="https://datatracker.ietf.org/doc/html/rfc6749">RFC6749</see> describes the format of scope values.</para></remarks>
        [JsonPropertyName("scopes_supported")]
        public IList<string> SupportedScopes { get; init; } = new List<string>();

        /// <summary>
        /// A list of the OAuth 2.0 response_type values that the provider supports.
        /// </summary>
        [JsonPropertyName("response_types_supported")]
        public IList<string> SupportedResponseTypes { get; init; } = new List<string>();

        /// <summary>
        /// A list of the OAuth 2.0 response_mode values that the provider supports.
        /// </summary>
        /// <remarks><para>The <see href="https://openid.net/specs/oauth-v2-multiple-response-types-1_0.html">OAuth 2.0 Multiple Response Type Encoding Practices</see> defines response types.</para></remarks>
        [JsonPropertyName("response_modes_supported")]
        public IList<string> SupportedResponseModes { get; init; } = new List<string>();

        /// <summary>
        /// A list of the OAuth 2.0 Grant Type values that this OP supports.
        /// Dynamic OpenID Providers MUST support the authorization_code and implicit Grant Type values and MAY support other Grant Types.
        /// </summary>
        /// <remarks><para>If omitted, the default list has two entries, "authorization_code" and "implicit".</para></remarks>
        [JsonPropertyName("grant_types_supported")]
        public IList<string> SupportedGrantTypes { get; init; } = new List<string>();

        /// <summary>
        /// A list of Proof Key for <see href="https://datatracker.ietf.org/doc/html/rfc7636">Code Exchange(PKCE)</see>
        /// code challenge methods supported by the provider.
        /// </summary>
        /// <remarks>
        /// <para>Code challenge method values are used in the "code_challenge_method" parameter defined
        /// </para>
        /// </remarks>
        [JsonPropertyName("code_challenge_methods_supported")]
        public IList<string> SupportedCodeChallengeMethods { get; init; } = new List<string>();

        /// <summary>
        /// A list of languages and scripts supported for the user interface of the provider.
        /// </summary>
        [JsonPropertyName("ui_locales_supported")]
        public IList<string> SupportedUILocales { get; init; } = new List<string>();

        /// <summary>
        /// A list of the display parameter values that the provider supports.
        /// </summary>
        [JsonPropertyName("display_values_supported")]
        public IList<string> DisplayValuesSupported { get; init; } = new List<string>();

        /// <summary>
        /// A flag indicating whether the provider supports the
        /// <see href="https://www.ietf.org/archive/id/draft-meyerzuselhausen-oauth-iss-auth-resp-02.html">Authorization Server Issued Identifier In Authorization response</see> standard.
        /// </summary>
        [JsonPropertyName("authorization_response_iss_parameter_supported")]
        public bool IssuerParameterInResponseSupported { get; init; } = false;

        /// <summary>
        /// A list of the JWS signing algorithms (alg values) supported by the prover for Request Objects
        /// </summary>
        [JsonPropertyName("request_object_signing_alg_values_supported")]
        public IList<string> RequestObjectSigningSupportedAlgorithms { get; init; } = new List<string>();

        /// <summary>
        /// A list of keys and element data that do not map to any strongly typed properties.
        /// </summary>
        [JsonExtensionData]
        public Dictionary<string, JsonElement>? ExtensionData { get; set; }
    }
}
