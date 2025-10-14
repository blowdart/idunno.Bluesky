// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json;
using Duende.IdentityModel.OidcClient;

namespace idunno.AtProto.Authentication
{
    /// <summary>
    /// Encapsulates state that is generated when a start URI is created and must be present for the response to be parsed.
    /// </summary>
    public sealed class OAuthLoginState : AuthorizeState
    {
        /// <summary>
        /// Creates a new instance of <see cref="OAuthLoginState"/>.
        /// </summary>
        /// <param name="state">The state the needs to be hold between starting the authorize request and the response.</param>
        /// <param name="expectedAuthority">The expected authority that the access token should be issued for.</param>
        /// <param name="expectedService">The expected service that the access token should be issued for.</param>
        /// <param name="proofKey">The DPoP proof key which was used to sign the token request.</param>
        /// <param name="correlationId">The correlation identifier used in logging to tie requests and responses together.</param>
        public OAuthLoginState(AuthorizeState state, string expectedAuthority, string expectedService, string proofKey, Guid correlationId)
        {
            ArgumentNullException.ThrowIfNull(state);

            ExpectedAuthority = expectedAuthority;
            ExpectedService = expectedService;
            ProofKey = proofKey;
            CorrelationId = correlationId;

            StartUrl = state.StartUrl;
            State = state.State;
            CodeVerifier = state.CodeVerifier;
            RedirectUri = state.RedirectUri;

            Error = state.Error;
            ErrorDescription = state.ErrorDescription;
        }

        /// <summary>
        /// Gets or sets the authority that the issued token should be issued from.
        /// </summary>
        public string ExpectedAuthority { get; set; }

        /// <summary>
        /// Gets or sets the service that the issued token should be issued for.
        /// </summary>
        public string ExpectedService { get; set; }

        /// <summary>
        /// Gets or sets the DPoP proof key which was used to sign the token request.
        /// </summary>
        public string ProofKey { get; set; }

        /// <summary>
        /// Gets or sets the logging correlation ID.
        /// </summary>
        public Guid CorrelationId { get; set; }

        /// <summary>
        /// Converts the <paramref name="state"/> to a JSON string.
        /// </summary>
        /// <param name="state">The <see cref="OAuthLoginState"/> to convert to json.</param>
        /// <returns>A string containing the <paramref name="state"/> as json.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="state"/> is null.</exception>
        public static string ToJson(OAuthLoginState state)
        {
            ArgumentNullException.ThrowIfNull(state);
            return JsonSerializer.Serialize(state, typeof(OAuthLoginState), SourceGenerationContext.Default);
        }

        /// <summary>
        /// Converts the <paramref name="json"/> to an instance of <see cref="OAuthLoginState"/>.
        /// </summary>
        /// <param name="json">The json to convert.</param>
        /// <returns>An instance of <see cref="OAuthLoginState"/> deserialized from the supplied <paramref name="json"/> string.</returns>
        /// <exception cref="ArgumentException">Thrown when <paramref name="json"/> is null or white space.</exception>
        public static OAuthLoginState? FromJson(string json)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(json);
            return JsonSerializer.Deserialize(json, typeof(OAuthLoginState), SourceGenerationContext.Default) as OAuthLoginState;
        }

        /// <summary>
        /// Converts this instance of <see cref="OAuthLoginState"/> to a JSON string.
        /// </summary>
        /// <returns>A string containing this instance of <see cref="OAuthLoginState"/> as json.</returns>
        public string ToJson()
        {
            return ToJson(this);
        }
    }
}
