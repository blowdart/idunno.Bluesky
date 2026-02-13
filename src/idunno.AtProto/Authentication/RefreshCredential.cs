// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Net.Http.Headers;

namespace idunno.AtProto.Authentication
{
    /// <summary>
    /// Encapsulates a refresh token.
    /// </summary>
    public class RefreshCredential : AtProtoCredential, IRefreshCredential
    {
        /// <summary>
        /// The refresh token value.
        /// </summary>
        private string _refreshToken;

        /// <summary>
        /// Creates a new instance of <see cref="RefreshCredential"/>.
        /// </summary>
        /// <param name="service">The service the credentials were issued from.</param>
        /// <param name="authenticationType">The type of authentication used to acquire the credentials.</param>
        /// <param name="value">The value for the refresh token.</param>
        /// <exception cref="ArgumentException">Thrown when <paramref name="value"/> is <see langword="null"/> or whitespace.</exception>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="service"/> is <see langword="null"/>.</exception>
        public RefreshCredential(Uri service, AuthenticationType authenticationType, string value) : base(service, authenticationType)
        {
            ArgumentNullException.ThrowIfNull(service);

            ArgumentException.ThrowIfNullOrWhiteSpace(value);

            _refreshToken = value;
        }

        /// <summary>
        /// Creates a new instance of <see cref="RefreshCredential"/> from the specified <paramref name="accessCredentials"/>.
        /// </summary>
        /// <param name="accessCredentials">The <see cref="AccessCredentials"/> to create the refresh credential from.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="accessCredentials"/> is <see langword="null"/>.</exception>
        public RefreshCredential(AccessCredentials accessCredentials) : this(
            accessCredentials != null ? accessCredentials.Service : throw new ArgumentNullException(nameof(accessCredentials)),
            accessCredentials.AuthenticationType,
            accessCredentials.RefreshToken)
        {
        }

        /// <summary>
        /// Gets or sets a string representation of the token to use when a new access token is required.
        /// </summary>
        /// <exception cref="ArgumentException">Thrown when setting the value and the value is <see langword="null"/> or whitespace.</exception>
        public string RefreshToken
        {
            get
            {
                ReaderWriterLockSlim.EnterReadLock();
                try
                {
                    return _refreshToken;
                }
                finally
                {
                    ReaderWriterLockSlim.ExitReadLock();
                }
            }

            set
            {
                ArgumentException.ThrowIfNullOrWhiteSpace(value);

                ReaderWriterLockSlim.EnterWriteLock();
                try
                {
                    _refreshToken = value;
                }
                finally
                {
                    ReaderWriterLockSlim.ExitWriteLock();
                }
            }
        }

        /// <summary>
        /// Add authentication headers to the specified <paramref name="httpRequestMessage"/>.
        /// </summary>
        /// <param name="httpRequestMessage">The <see cref="HttpRequestMessage"/> to add authentication headers to.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="httpRequestMessage"/> is <see langword="null"/>.</exception>
        public override void SetAuthenticationHeaders(HttpRequestMessage httpRequestMessage)
        {
            ArgumentNullException.ThrowIfNull(httpRequestMessage);

            httpRequestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", RefreshToken);
        }
    }
}
