// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.


namespace idunno.AtProto.Authentication
{
    /// <summary>
    /// Properties for a credential that has an access jwt.
    /// </summary>
    public interface IAccessCredential
    {
        /// <summary>
        /// Gets a string representation of the JWT to use when making authenticated access requests.
        /// </summary>
        public string AccessJwt { get; }

        /// <summary>
        /// Gets the <see cref="DateTimeOffset"/> the <see cref="AccessJwt"/> expires on.
        /// </summary>
        /// <remarks>
        /// <para>Identifies the expiration time on or after which the JWT MUST NOT be accepted for processing. See: https://datatracker.ietf.org/doc/html/rfc7519#section-4.1.4.</para>
        /// <para>If the 'exp' claim is not found, then <see cref="DateTimeOffset.MinValue">MinValue</see> is returned.</para>
        /// </remarks>
        public DateTimeOffset ExpiresOn { get; }

        /// <summary>
        /// Gets a value indicating whether the access token is expired.
        /// </summary>
        public bool IsExpired => DateTimeOffset.UtcNow >= ExpiresOn;

        /// <summary>
        /// Gets the <see cref="AtProto.Did"/> the access token was issued for.
        /// </summary>
        public Did Did { get; }
    }
}
