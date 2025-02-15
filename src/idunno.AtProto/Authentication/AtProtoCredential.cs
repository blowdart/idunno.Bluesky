// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

namespace idunno.AtProto.Authentication
{
    /// <summary>
    /// Base class for credentials to access an ATProto service.
    /// </summary>
    /// <param name="service">The service the credentials were issued from.</param>
    /// <param name="authenticationType">The type of authentication.</param>
    [Serializable]
    public abstract class AtProtoCredential(Uri service, AuthenticationType authenticationType)
    {

        /// <summary>
        /// Finalizes this instance of <see cref="AtProtoCredential"/>.
        /// </summary>
        ~AtProtoCredential()
        {
            ReaderWriterLockSlim?.Dispose();
        }

        /// <summary>
        /// The type of authentication used to acquire the credentials.
        /// </summary>
        public AuthenticationType AuthenticationType { get; init; } = authenticationType;

        /// <summary>
        /// Gets the service the credentials were issued for.
        /// </summary>
        public Uri Service { get; init; } = service;

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
        /// <param name="authenticationType">The type of authentication used to acquire the credentials.</param>
        /// <param name="accessJwt">A string representation of the access jwt to us when authenticating to the service.</param>
        /// <param name="refreshToken">A string representation of the access jwt to us when request a new access token from the service.</param>
        /// <param name="dPoPProofKey">The string representation of the DPoP proof key to use when signing requests.</param>
        /// <param name="dPoPNonce">The string representation of the DPoP nonce to use when signing requests.</param>
        /// <returns>An appropriate subtype of <see cref="AtProtoCredential"/>, depending on the combination of the specified parameters.</returns>
        /// <exception cref="AtProtoException">Thrown when the combination of parameters does not match a known AtProtoCredentials subtype.</exception>
        public static AtProtoCredential Create(
            Uri service,
            AuthenticationType authenticationType,
            string? accessJwt=null,
            string? refreshToken = null,
            string? dPoPProofKey = null,
            string? dPoPNonce = null)
        {
            ArgumentNullException.ThrowIfNull(service);
            ArgumentNullException.ThrowIfNull(authenticationType);

            switch (authenticationType)
            {
                case AuthenticationType.UsernamePassword:
                case AuthenticationType.UsernamePasswordAuthFactorToken:
                    ArgumentException.ThrowIfNullOrWhiteSpace(refreshToken);

                    if (!string.IsNullOrWhiteSpace(accessJwt))
                    {
                        return new AccessCredentials(service, authenticationType, accessJwt, refreshToken);
                    }
                    else
                    {
                        return new RefreshCredential(service, authenticationType, refreshToken);
                    }

                case AuthenticationType.Service:
                    ArgumentException.ThrowIfNullOrWhiteSpace(accessJwt);
                    return new ServiceCredential(service, accessJwt);

                case AuthenticationType.OAuth:
                    ArgumentException.ThrowIfNullOrWhiteSpace(refreshToken);
                    ArgumentException.ThrowIfNullOrWhiteSpace(dPoPProofKey);
                    ArgumentException.ThrowIfNullOrWhiteSpace(dPoPNonce);

                    if (!string.IsNullOrWhiteSpace(accessJwt))
                    {
                        return new DPoPAccessCredentials(
                            service: service,
                            accessJwt: accessJwt,
                            refreshToken: refreshToken,
                            dPoPProofKey: dPoPProofKey,
                            dPoPNonce: dPoPNonce);
                    }
                    else
                    {
                        return new DPoPRefreshCredential(
                            service: service,
                            refreshToken: refreshToken,
                            dPoPProofKey: dPoPProofKey,
                            dPoPNonce: dPoPNonce);
                    }

                default:
                    throw new ArgumentException("Unknown authenticationType", nameof(authenticationType));
            }
        }
    }
}
