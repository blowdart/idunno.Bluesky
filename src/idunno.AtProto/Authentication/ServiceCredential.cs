// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;
using System.Net.Http.Headers;

using Microsoft.IdentityModel.JsonWebTokens;

namespace idunno.AtProto.Authentication
{
    /// <summary>
    /// Encapsulates the credential to call an authenticated service API.
    /// Service credentials only have an Access JWT, and have no refresh token.
    /// </summary>

    public class ServiceCredential : AtProtoCredentials, IAccessCredential
    {
        private string _accessToken;

        /// <summary>
        /// Creates a new instance of <see cref="AccessCredentials"/> with the specified <paramref name="accessJwt"/>.
        /// </summary>
        /// <param name="service">The <see cref="Uri"/> of the service the credentials were issued from.</param>
        /// <param name="accessJwt">A string representation of the JWT to use when making authenticated access requests.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="service"/> is null.</exception>
        /// <exception cref="ArgumentException">Thrown when <paramref name="accessJwt"/>.</exception>
        public ServiceCredential(Uri service, string accessJwt) : base(service)
        {
            ArgumentNullException.ThrowIfNull(service);
            ArgumentException.ThrowIfNullOrWhiteSpace(accessJwt);

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

        [MemberNotNull(nameof(ExpiresOn))]
        [MemberNotNull(nameof(Did))]
        private void ExtractJwtProperties(string jwt)
        {
            JsonWebToken token = new(jwt);
            Did = new Did(token.Subject);
            ExpiresOn = DateTime.SpecifyKind(token.ValidTo, DateTimeKind.Utc);
        }
    }
}
