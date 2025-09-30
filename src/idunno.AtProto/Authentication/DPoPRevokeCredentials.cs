// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using Duende.IdentityModel.Client;
using Duende.IdentityModel.OidcClient.DPoP;

namespace idunno.AtProto.Authentication
{
    internal class DPoPRevokeCredentials : AtProtoCredential, IDPoPBoundCredential
    {
        private string _dPoPProofKey;
        private string _dPoPNonce;
        private readonly string _token;

        /// <summary>
        /// Creates a new instance of <see cref="DPoPRevokeCredentials"/> with the specified <paramref name="token"/>,
        /// <paramref name="dPoPProofKey"/> and <paramref name="dPoPNonce"/>.
        /// </summary>
        /// <param name="service">The <see cref="Uri"/> of the service the credentials were issued from.</param>
        /// <param name="token">A string representation of the JWT to be revoked.</param>
        /// <param name="dPoPProofKey">An optional string representation of the DPoP proof key to use when signing requests.</param>
        /// <param name="dPoPNonce">An optional string representation of the DPoP nonce to use when signing requests.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="service"/> is null.</exception>
        /// <exception cref="ArgumentException">
        /// Thrown when <paramref name="token"/>,  <paramref name="dPoPProofKey"/> or <paramref name="dPoPNonce"/> is null or whitespace.
        /// </exception>
        public DPoPRevokeCredentials(Uri service, string token, string dPoPProofKey, string dPoPNonce) : base(service, AuthenticationType.OAuth)
        {
            ArgumentNullException.ThrowIfNull(service);
            ArgumentException.ThrowIfNullOrWhiteSpace(token);
            ArgumentException.ThrowIfNullOrEmpty(dPoPProofKey);
            ArgumentException.ThrowIfNullOrEmpty(dPoPNonce);

            _dPoPProofKey = dPoPProofKey;
            _dPoPNonce = dPoPNonce;
            _token = token;
        }

        /// <summary>
        /// Creates a new instance of <see cref="DPoPRevokeCredentials"/> from the supplied <paramref name="accessCredentials"/>.
        /// </summary>
        /// <param name="accessCredentials">The <see cref="DPoPAccessCredentials"/> to create the revoke credentials from.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="accessCredentials"/> is null.</exception>
        public DPoPRevokeCredentials(DPoPAccessCredentials accessCredentials) : base(accessCredentials.Service, AuthenticationType.OAuth)
        {
            ArgumentNullException.ThrowIfNull(accessCredentials);

            _dPoPProofKey = accessCredentials.DPoPProofKey;
            _dPoPNonce = accessCredentials.DPoPNonce;
            _token = accessCredentials.AccessJwt;
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
                DPoPNonce = _dPoPNonce,
                Method = httpRequestMessage.Method.ToString(),
                Url = httpRequestMessage.GetDPoPUrl()
            };

            DPoPProofTokenFactory factory = new(_dPoPProofKey);
            DPoPProof proofToken = factory.CreateProofToken(dPoPProofRequest);

            httpRequestMessage.SetDPoPToken(_token, proofToken.ProofToken);
        }

    }
}
