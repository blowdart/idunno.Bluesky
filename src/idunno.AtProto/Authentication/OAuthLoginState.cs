// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

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
    }
}
