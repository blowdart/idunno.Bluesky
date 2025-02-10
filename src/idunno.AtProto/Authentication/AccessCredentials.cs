// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;
using System.Net.Http.Headers;

using Microsoft.IdentityModel.JsonWebTokens;

namespace idunno.AtProto.Authentication
{
    /// <summary>
    /// Encapsulates the credentials and supporting values necessary to call an authenticated API.
    /// </summary>
    public class AccessCredentials : RefreshCredential, IAccessCredential
    {
        private string _accessToken;

        /// <summary>
        /// Creates a new instance of <see cref="AccessCredentials"/> with the specified <paramref name="accessJwt"/> and <paramref name="refreshToken"/>.
        /// </summary>
        /// <param name="service">The <see cref="Uri"/> of the service the credentials were issued from.</param>
        /// <param name="authenticationType">The type of authentication used to acquire the credentials.</param>
        /// <param name="accessJwt">A string representation of the JWT to use when making authenticated access requests.</param>
        /// <param name="refreshToken">A string representation of the token to use when a new access token is required.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="service"/> is null.</exception>
        /// <exception cref="ArgumentException">Thrown when <paramref name="accessJwt"/> or <paramref name="refreshToken"/> is null or whitespace.</exception>
        public AccessCredentials(Uri service, AuthenticationType authenticationType, string accessJwt, string refreshToken) : base(service, authenticationType, refreshToken)
        {
            ArgumentNullException.ThrowIfNull(service);
            ArgumentException.ThrowIfNullOrWhiteSpace(accessJwt);
            ArgumentException.ThrowIfNullOrWhiteSpace(refreshToken);

            _accessToken = accessJwt;
            ExtractJwtProperties(accessJwt);
        }

        /// <summary>
        /// Gets a string representation of the JWT to use when making authenticated access requests.
        /// </summary>
        /// <exception cref="ArgumentException">Thrown when setting the value and the value is null or whitespace.</exception>
        public string AccessJwt
        {
            get
            {
                ReaderWriterLockSlim.EnterReadLock();
                try
                {
                    return _accessToken;
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
                    _accessToken = value;
                    ExtractJwtProperties(value);
                }
                finally
                {
                    ReaderWriterLockSlim.ExitWriteLock();
                }
            }
        }

        /// <summary>
        /// Gets the <see cref="DateTimeOffset"/> the <see cref="AccessJwt"/> expires on.
        /// </summary>
        /// <returns>The value of the 'exp' claim converted to a <see cref="DateTimeOffset"/> from the jwt.</returns>
        /// <remarks>
        /// <para>Identifies the expiration time on or after which the JWT MUST NOT be accepted for processing. See: https://datatracker.ietf.org/doc/html/rfc7519#section-4.1.4.</para>
        /// <para>If the 'exp' claim is not found, then <see cref="DateTimeOffset.MinValue">MinValue</see> is returned.</para>
        /// </remarks>
        public DateTimeOffset ExpiresOn { get; private set; }

        /// <summary>
        /// Gets the <see cref="AtProto.Did"/> the access token was issued for.
        /// </summary>
        public Did Did { get; private set; }

        /// <summary>
        /// Add authentication headers to the specified <paramref name="httpRequestMessage"/>.
        /// </summary>
        /// <param name="httpRequestMessage">The <see cref="HttpRequestMessage"/> to add authentication headers to.</param>
        public override void SetAuthenticationHeaders(HttpRequestMessage httpRequestMessage)
        {
            ArgumentNullException.ThrowIfNull(httpRequestMessage);

            httpRequestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", AccessJwt);
        }

        internal void Update(AccessCredentials accessCredentials)
        {
            ArgumentNullException.ThrowIfNull(accessCredentials);
            ArgumentException.ThrowIfNullOrWhiteSpace(accessCredentials.AccessJwt);
            ArgumentException.ThrowIfNullOrWhiteSpace(accessCredentials.RefreshToken);

            ReaderWriterLockSlim.EnterWriteLock();

            try
            {
                AccessJwt = accessCredentials.AccessJwt;
                RefreshToken = accessCredentials.RefreshToken;
            }
            finally
            {
                ReaderWriterLockSlim.ExitWriteLock();
            }
        }

        /// <summary>
        /// Extracts the DID and expiration date from the specified jwt and sets the <see cref="Did"/> and <see cref="ExpiresOn"/> properties.
        /// </summary>
        /// <param name="jwt">A string representation of the jwt to extract the properties from</param>.
        /// <exception cref="ArgumentException">Thrown when <paramref name="jwt"/> is null or whitespace.</exception>
        [MemberNotNull(nameof(Did))]
        [MemberNotNull(nameof(ExpiresOn))]
        protected void ExtractJwtProperties(string jwt)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(jwt);

            JsonWebToken token = new(jwt);
            Did = new Did(token.Subject);
            ExpiresOn = DateTime.SpecifyKind(token.ValidTo, DateTimeKind.Utc);
        }
    }
}
