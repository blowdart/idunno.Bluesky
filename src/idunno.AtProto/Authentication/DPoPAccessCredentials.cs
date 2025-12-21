// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using Duende.IdentityModel.Client;
using Duende.IdentityModel.OidcClient.DPoP;

namespace idunno.AtProto.Authentication
{
    /// <summary>
    /// Encapsulates the credentials and supporting values necessary to call an authenticated API, with proof of possession.
    /// </summary>
    public sealed class DPoPAccessCredentials : AccessCredentials, IDPoPBoundCredential
    {
        private string _dPoPProofKey;
        private string _dPoPNonce;

        /// <summary>
        /// Creates a new instance of <see cref="DPoPAccessCredentials"/> with the specified <paramref name="accessJwt"/>, <paramref name="refreshToken"/>,
        /// <paramref name="dPoPProofKey"/> and <paramref name="dPoPNonce"/>.
        /// </summary>
        /// <param name="service">The <see cref="Uri"/> of the service the credentials were issued from.</param>
        /// <param name="accessJwt">A string representation of the JWT to use when making authenticated access requests.</param>
        /// <param name="refreshToken">A string representation of the refresh token to use when a new access token is required.</param>
        /// <param name="dPoPProofKey">An optional string representation of the DPoP proof key to use when signing requests.</param>
        /// <param name="dPoPNonce">An optional string representation of the DPoP nonce to use when signing requests.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="service"/> is null.</exception>
        /// <exception cref="ArgumentException">
        /// Thrown when <paramref name="accessJwt"/>, <paramref name="refreshToken"/>, <paramref name="dPoPProofKey"/> or <paramref name="dPoPNonce"/> is null or whitespace.
        /// </exception>
        public DPoPAccessCredentials(Uri service, string accessJwt, string refreshToken, string dPoPProofKey, string dPoPNonce) : base(service, AuthenticationType.OAuth, accessJwt, refreshToken)
        {
            ArgumentNullException.ThrowIfNull(service);
            ArgumentException.ThrowIfNullOrWhiteSpace(accessJwt);
            ArgumentException.ThrowIfNullOrWhiteSpace(refreshToken);
            ArgumentException.ThrowIfNullOrEmpty(dPoPProofKey);
            ArgumentException.ThrowIfNullOrEmpty(dPoPNonce);

            _dPoPProofKey = dPoPProofKey;
            _dPoPNonce = dPoPNonce;
        }

        /// <summary>
        /// Gets or sets a string representation of the DPoP proof key to use when signing requests.
        /// </summary>
        /// <exception cref="ArgumentException">Thrown when setting the value and the value is null or whitespace.</exception>
        public string DPoPProofKey {
            get
            {
                ReaderWriterLockSlim.EnterReadLock();
                try
                {
                    return _dPoPProofKey;
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
                    _dPoPProofKey = value;
                }
                finally
                {
                    ReaderWriterLockSlim.ExitWriteLock();
                }
            }
        }

        /// <summary>
        /// Gets a string representation of the DPoP nonce to use when signing requests.
        /// </summary>
        public string DPoPNonce
        {
            get
            {
                ReaderWriterLockSlim.EnterReadLock();
                try
                {
                    return _dPoPNonce;
                }
                finally
                {
                    ReaderWriterLockSlim.ExitReadLock();
                }
            }

            set
            {
                ReaderWriterLockSlim.EnterWriteLock();
                try
                {
                    _dPoPNonce = value;
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
        public override void SetAuthenticationHeaders(HttpRequestMessage httpRequestMessage)
        {
            ArgumentNullException.ThrowIfNull(httpRequestMessage);

            DPoPProofRequest dPoPProofRequest = new()
            {
                AccessToken = AccessJwt,
                DPoPNonce = DPoPNonce,
                Method = httpRequestMessage.Method.ToString(),
                Url = httpRequestMessage.GetDPoPUrl()
            };

            DefaultDPoPProofTokenFactory factory = new(DPoPProofKey);
            DPoPProof proofToken = factory.CreateProofToken(dPoPProofRequest);

            httpRequestMessage.SetDPoPToken(AccessJwt, proofToken.ProofToken);
        }

        internal void Update(DPoPAccessCredentials dPoPAccessCredentials)
        {
            ArgumentNullException.ThrowIfNull(dPoPAccessCredentials);
            ArgumentException.ThrowIfNullOrWhiteSpace(dPoPAccessCredentials.AccessJwt);
            ArgumentException.ThrowIfNullOrWhiteSpace(dPoPAccessCredentials.RefreshToken);
            ArgumentException.ThrowIfNullOrWhiteSpace(dPoPAccessCredentials.DPoPProofKey);

            ReaderWriterLockSlim.EnterWriteLock();

            try
            {
                Update(dPoPAccessCredentials as AccessCredentials);
                _dPoPProofKey = dPoPAccessCredentials.DPoPProofKey;
                _dPoPNonce = dPoPAccessCredentials.DPoPNonce;
            }
            finally
            {
                ReaderWriterLockSlim.ExitWriteLock();
            }
        }
    }
}
