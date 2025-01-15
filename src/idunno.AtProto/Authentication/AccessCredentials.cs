// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

using Microsoft.IdentityModel.JsonWebTokens;

namespace idunno.AtProto.Authentication
{
    /// <summary>
    /// Encapsulates the credentials and supporting values necessary to call an unauthenticated API.
    /// </summary>
    public sealed record class AccessCredentials
    {
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private readonly object _syncRoot = new();

        /// <summary>
        /// Creates a new instance of <see cref="AccessCredentials"/> with the specified <paramref name="accessJwt"/>, <paramref name="dPoPProofKey"/> and <paramref name="dPoPNonce"/>.
        /// </summary>
        /// <param name="service">The <see cref="Uri"/> of the service the credentials were issued from.</param>
        /// <param name="accessJwt">A string representation of the JWT to use when making authenticated access requests.</param>
        /// <param name="refreshJwt">A string representation of the JWT to use when a new access token is required.</param>
        /// <param name="dPoPProofKey">An optional string representation of the DPoP proof key to use when signing requests.</param>
        /// <param name="dPoPNonce">An optional string representation of the DPoP nonce to use when signing requests.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="service"/> is null.</exception>
        /// <exception cref="ArgumentException">Thrown when <paramref name="accessJwt"/> or <paramref name="refreshJwt"/> is null or whitespace</exception>
        public AccessCredentials(Uri service, string? accessJwt, string refreshJwt, string? dPoPProofKey = null, string? dPoPNonce = null)
        {
            ArgumentNullException.ThrowIfNull(service);
            ArgumentException.ThrowIfNullOrWhiteSpace(refreshJwt);

            Service = service;

            AccessJwt = accessJwt;
            RefreshJwt = refreshJwt;

            DPoPProofKey = dPoPProofKey;
            DPoPNonce = dPoPNonce;

            if (AccessJwt is not null)
            {
                AccessJwtExpiresOn = GetJwtExpiry(AccessJwt);
            }
        }

        /// <summary>
        /// Gets a string representation of the JWT to use when making authenticated access requests.
        /// </summary>
        public string? AccessJwt { get; private set; }

        /// <summary>
        /// Gets the <see cref="DateTimeOffset"/> the <see cref="AccessJwt"/> expires on, if one is present, otherwise null..
        /// </summary>
        /// <returns>The value of the 'exp' claim converted to a <see cref="DateTimeOffset"/> from the jwt.</returns>
        /// <remarks>
        /// <para>Identifies the expiration time on or after which the JWT MUST NOT be accepted for processing. See: https://datatracker.ietf.org/doc/html/rfc7519#section-4.1.4.</para>
        /// <para>If the 'exp' claim is not found, then <see cref="DateTimeOffset.MinValue">MinValue</see> is returned.</para>
        /// </remarks>
        public DateTimeOffset? AccessJwtExpiresOn { get; private set; }

        /// <summary>
        /// Gets a string representation of the JWT to use when a new access token is required.
        /// </summary>
        public string RefreshJwt { get; private set; }

        /// <summary>
        /// Gets a string representation of the DPoP proof key to use when signing requests.
        /// </summary>
        public string? DPoPProofKey { get; internal set; }

        /// <summary>
        /// Gets a string representation of the DPoP nonce to use when signing requests.
        /// </summary>
        public string? DPoPNonce { get; internal set; }

        /// <summary>
        /// Flag indicating whether or not this instance has DPoP information and that api requests need to be signed.
        /// </summary>
        [MemberNotNullWhen(true, nameof(DPoPProofKey))]
        [MemberNotNullWhen(true, nameof(DPoPNonce))]
        public bool RequiresDPoP
        {
            get
            {
                return !string.IsNullOrWhiteSpace(DPoPProofKey) && !string.IsNullOrWhiteSpace(DPoPNonce);
            }
        }

        /// <summary>
        /// Gets the service the credentials were issued from.
        /// </summary>
        public Uri Service { get; init; }

        internal void UpdateAccessTokens(string accessJwt, string refreshJwt)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(accessJwt);
            ArgumentException.ThrowIfNullOrWhiteSpace(refreshJwt);

            lock (_syncRoot)
            {
                AccessJwt = accessJwt;
                RefreshJwt = refreshJwt;
                AccessJwtExpiresOn = GetJwtExpiry(AccessJwt);
            }
        }

        internal void Update(AccessCredentials accessCredentials)
        {
            ArgumentNullException.ThrowIfNull(accessCredentials);
            ArgumentException.ThrowIfNullOrWhiteSpace(accessCredentials.AccessJwt);
            ArgumentException.ThrowIfNullOrWhiteSpace(accessCredentials.RefreshJwt);

            lock (_syncRoot)
            {
                AccessJwt = accessCredentials.AccessJwt;
                RefreshJwt = accessCredentials.RefreshJwt;

                DPoPProofKey = accessCredentials.DPoPProofKey;
                DPoPNonce = accessCredentials.DPoPNonce;

                AccessJwtExpiresOn = GetJwtExpiry(AccessJwt);
            }
        }

        private static DateTimeOffset GetJwtExpiry(string jwt)
        {
            JsonWebToken token = new(jwt);
            DateTime expiryDateTime = DateTime.SpecifyKind(token.ValidTo, DateTimeKind.Utc);
            DateTimeOffset expiryDateTimeOffset = expiryDateTime;

            return expiryDateTimeOffset;
        }
    }
}
