// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

namespace idunno.AtProto.Authentication
{
    /// <summary>
    /// Base class for authorization credentials to access an ATProto service.
    /// </summary>
    /// <param name="service"></param>
    public abstract class AtProtoCredentials(Uri service)
    {
        /// <summary>
        /// Finalizes this instance of <see cref="AtProtoCredentials"/>.
        /// </summary>
        ~AtProtoCredentials()
        {
            ReaderWriterLockSlim?.Dispose();
        }

        /// <summary>
        /// Gets the service the credentials were issued from.
        /// </summary>
        public Uri Service { get; } = service;

        /// <summary>
        /// Add authentication headers to the specified <paramref name="httpRequestMessage"/>.
        /// </summary>
        /// <param name="httpRequestMessage">The <see cref="HttpRequestMessage"/> to add authentication headers to.</param>
        public abstract void SetAuthenticationHeaders(HttpRequestMessage httpRequestMessage);

        /// <summary>
        /// Gets a <see cref="ReaderWriterLock"/> used to guard access to properties.
        /// </summary>
        protected ReaderWriterLockSlim ReaderWriterLockSlim { get; } = new();

        /// <summary>
        /// Creates an appropriate AtProtoCredentials instance from the specified parameters.
        /// </summary>
        /// <param name="service">The service the credentials were issued from.</param>
        /// <param name="accessJwt">A string representation of the access jwt to us when authenticating to the service.</param>
        /// <param name="refreshToken">A string representation of the access jwt to us when request a new access token from the service.</param>
        /// <param name="dPoPProofKey">The string representation of the DPoP proof key to use when signing requests.</param>
        /// <param name="dPoPNonce">The string representation of the DPoP nonce to use when signing requests.</param>
        /// <returns>An appropriate subtype of <see cref="AtProtoCredentials"/>, depending on the combination of the specified parameters.</returns>
        /// <exception cref="AtProtoException">Thrown when the combination of parameters does not match a known AtProtoCredentials subtype.</exception>
        public static AtProtoCredentials Create(Uri service, string? accessJwt, string? refreshToken, string? dPoPProofKey = null, string? dPoPNonce = null)
        {
            ArgumentNullException.ThrowIfNull(service);

            if (string.IsNullOrWhiteSpace(accessJwt) && !string.IsNullOrWhiteSpace(refreshToken))
            {
                if (string.IsNullOrWhiteSpace(dPoPProofKey) && string.IsNullOrWhiteSpace(dPoPNonce))
                {
                    return new RefreshCredential(
                        service: service,
                        value: refreshToken);
                }
                else if (!string.IsNullOrWhiteSpace(dPoPProofKey) && !string.IsNullOrWhiteSpace(dPoPNonce))
                {
                    return new DPoPRefreshCredentials(
                        service: service,
                        refreshToken: refreshToken,
                        dPoPProofKey: dPoPProofKey,
                        dPoPNonce: dPoPNonce);
                }
            }
            else if (!string.IsNullOrWhiteSpace(accessJwt) && !string.IsNullOrWhiteSpace(refreshToken))
            {
                if (string.IsNullOrWhiteSpace(dPoPProofKey) && string.IsNullOrWhiteSpace(dPoPNonce))
                {
                    return new AccessCredentials(
                        service: service,
                        accessJwt: accessJwt,
                        refreshToken: refreshToken);
                }
                else if (!string.IsNullOrWhiteSpace(dPoPProofKey) && !string.IsNullOrWhiteSpace(dPoPNonce))
                {
                    return new DPoPAccessCredentials(
                        service: service,
                        accessJwt: accessJwt,
                        refreshToken: refreshToken,
                        dPoPProofKey: dPoPProofKey,
                        dPoPNonce: dPoPNonce);
                }
            }

            throw new AtProtoException("Cannot create an AtProtoCredentials from the specified parameter combination.");
        }
    }
}
