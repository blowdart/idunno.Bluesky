// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using IdentityModel.Client;
using IdentityModel.OidcClient.DPoP;

namespace idunno.AtProto.Authentication
{
    /// <summary>
    /// Encapsulates a refresh token with proof of possession.
    /// </summary>
    public sealed class DPoPRefreshCredential : RefreshCredential, IDPoPBoundCredential
    {
        private string _dPoPProofKey;
        private string _dPoPNonce;

        /// <summary>
        /// Creates a new instance of <see cref="DPoPRefreshCredential"/> with the specified <paramref name="refreshToken"/>, <paramref name="dPoPProofKey"/> and <paramref name="dPoPNonce"/>.
        /// </summary>
        /// <param name="service">The <see cref="Uri"/> of the service the credentials were issued from.</param>
        /// <param name="refreshToken">A string representation of the JWT to use when a new access token is required.</param>
        /// <param name="dPoPProofKey">The string representation of the DPoP proof key to use when signing requests.</param>
        /// <param name="dPoPNonce">The string representation of the DPoP nonce to use when signing requests.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="service"/> is null.</exception>
        /// <exception cref="ArgumentException">
        /// Thrown when <paramref name="refreshToken"/> or <paramref name="dPoPProofKey"/>  is null or whitespace.
        /// </exception>
        public DPoPRefreshCredential(Uri service, string refreshToken, string dPoPProofKey, string dPoPNonce) : base(service, AuthenticationType.OAuth, refreshToken)
        {
            ArgumentNullException.ThrowIfNull(service);
            ArgumentException.ThrowIfNullOrWhiteSpace(refreshToken);
            ArgumentException.ThrowIfNullOrEmpty(dPoPProofKey);

            _dPoPProofKey = dPoPProofKey;
            _dPoPNonce = dPoPNonce;
        }

        /// <summary>
        /// Gets or sets a string representation of the DPoP proof key to use when signing requests.
        /// </summary>
        /// <exception cref="ArgumentException">Thrown when setting the value and the value is null or whitespace.</exception>
        public string DPoPProofKey
        {
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
        /// <exception cref="ArgumentException">Thrown when setting the value and the value is null or whitespace.</exception>
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
                    ArgumentException.ThrowIfNullOrWhiteSpace(value);

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
                AccessToken = RefreshToken,
                DPoPNonce = DPoPNonce,
                Method = httpRequestMessage.Method.ToString(),
                Url = httpRequestMessage.GetDPoPUrl()
            };

            DPoPProofTokenFactory factory = new(DPoPProofKey);
            DPoPProof proofToken = factory.CreateProofToken(dPoPProofRequest);

            httpRequestMessage.SetDPoPToken(RefreshToken, proofToken.ProofToken);
        }
    }
}
