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
        /// Creates a new instance of <see cref="OAuthOptions"/>.
        /// </summary>
        public OAuthOptions()
        {
        }

        /// <summary>
        /// Check that the options are valid.
        /// </summary>
        /// <exception cref="ArgumentException">Thrown when <see cref="ClientId"/> is null or white space.</exception>
        /// <exception cref="ArgumentNullException">Thrown when <see cref="Scopes"/> is null.</exception>
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
        public Uri? CallbackUri { get; set; } = default!;

        /// <summary>
        /// Gets or sets the list of permissions to request.
        /// </summary>
        public IEnumerable<string> Scopes
        {
            get
            {
                return _scopes;
            }

            set
            {
                if (value is null)
                {
                    ArgumentNullException.ThrowIfNull(value);
                }
                else
                {
                    ArgumentOutOfRangeException.ThrowIfZero(value.Count());
                    _scopes = value;
                }
            }
        }
    }
}
