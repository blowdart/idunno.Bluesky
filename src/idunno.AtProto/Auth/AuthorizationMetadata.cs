// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace idunno.AtProto.Auth
{
    /// <summary>
    /// A class for exposing metadata about an authorization server retrieved from an OpenID Connect discovery point..
    /// </summary>
    /// <remarks>
    /// <para>See https://openid.net/specs/openid-connect-discovery-1_0.html for the specification.</para>
    /// </remarks>
    internal record AuthorizationMetadata
    {
        public Uri? Issuer { get; init; }

        [JsonPropertyName("subject_types_supported")]
        public IList<string> SupportedScopes { get; init; } = new List<string>();

        [JsonPropertyName("response_types_supported")]
        public IList<string> SupportedResponseTypes { get; init; } = new List<string>();

        [JsonPropertyName("response_modes_supported")]
        public IList<string> SupportedResponseModes { get; init; } = new List<string>();

        [JsonPropertyName("grant_types_supported")]
        public IList<string> SupportedGrantTypes { get; init; } = new List<string>();

        [JsonPropertyName("code_challenge_methods_supported")]
        public IList<string> SupportedCodeChallengeMethods { get; init; } = new List<string>();

        [JsonPropertyName("ui_locales_supported")]
        public IList<string> SupportedUILocales { get; init; } = new List<string>();

        [JsonPropertyName("display_values_supported")]
        public IList<string> DisplayValuesSupported { get; init; } = new List<string>();

        [JsonPropertyName("authorization_response_iss_parameter_supported")]
        public bool IssuerParameterInResponseSupported { get; init; } = false;

        [JsonPropertyName("request_object_signing_alg_values_supported")]
        public IList<string> RequestObjectSigningSupportedAlgorithms { get; init; } = new List<string>();

    }
}
