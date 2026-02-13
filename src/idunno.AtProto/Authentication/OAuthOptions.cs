// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

namespace idunno.AtProto.Authentication
{
    /// <summary>
    /// Configuration options for OAuth authentication.
    /// </summary>
    public sealed class OAuthOptions
    {
        private IEnumerable<string> _scopes = ["atproto"];

        /// <summary>
        /// Configuration Provider Key
        /// </summary>
        public const string AtProto = "AtProto";

        /// <summary>
        /// Configuration Provider Key
        /// </summary>
        public const string AtProtoOAuth = "OAuth";

        /// <summary>
        /// Creates a new instance of <see cref="OAuthOptions"/>.
        /// </summary>
        public OAuthOptions()
        {
        }

        /// <summary>
        /// Creates a new instance of <see cref="OAuthOptions"/>.
        /// </summary>
        /// <param name="clientId">The OAuth client id.</param>
        /// <param name="returnUri">The return <see cref="Uri"/> an oauth authentication flow should return to.</param>
        /// <param name="scopes">The list of permissions to request.</param>
        public OAuthOptions(string clientId, Uri? returnUri = null, IEnumerable<string>? scopes = null)
        {
            ClientId = clientId;
        
            ReturnUri = returnUri;

            if (scopes is not null)
            {
                Scopes = scopes;
            }
        }

        /// <summary>
        /// Check that the options are valid.
        /// </summary>
        /// <exception cref="ArgumentException">Thrown when <see cref="ClientId"/> is white space.</exception>
        /// <exception cref="ArgumentNullException">Thrown when <see cref="ClientId"/> or <see cref="Scopes"/> is <see langword="null"/>.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <see cref="Scopes"/> is empty.</exception>
        public void Validate()
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(ClientId);
            ArgumentNullException.ThrowIfNull(Scopes);
            ArgumentOutOfRangeException.ThrowIfZero(Scopes.Count());
        }

        /// <summary>
        /// Gets or sets the OAuth client id.
        /// </summary>
        public string ClientId { get; set; } = default!;

        /// <summary>
        /// Gets or sets the <see cref="Uri"/> the OAuth server should call back to when it has authenticated the user.
        /// </summary>
        public Uri? ReturnUri { get; set; } = default!;

        /// <summary>
        /// Gets or sets the list of permissions to request.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when setting to <see langword="null"/>.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when setting to an empty collection.</exception>
        public IEnumerable<string> Scopes
        {
            get
            {
                return _scopes;
            }

            set
            {
                ArgumentNullException.ThrowIfNull(value);
                ArgumentOutOfRangeException.ThrowIfZero(value.Count());

                _scopes = value.Distinct();
            }
        }
    }
}
