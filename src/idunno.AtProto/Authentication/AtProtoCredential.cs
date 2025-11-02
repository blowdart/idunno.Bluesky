// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Security.Claims;

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
        public AuthenticationType AuthenticationType { get; protected set; } = authenticationType;

        /// <summary>
        /// Gets the service the credentials were issued for.
        /// </summary>
        public Uri Service { get; protected set; } = service;

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

        /// <summary>
        /// Creates a <see cref="DPoPAccessCredentials"/> instance from the <paramref name="principal"/>.
        /// </summary>
        /// <param name="principal">The <see cref="ClaimsPrincipal"/> containing appropriate claims.</param>
        /// <returns>A <see cref="DPoPAccessCredentials"/> created from the claims in the specified <paramref name="principal"/>.</returns>
        /// <exception cref="ArgumentNullException">
        ///   Thrown when the <paramref name="principal"/> is null, or its Identity property is null,
        ///   or its Identity property is not a <see cref="ClaimsIdentity"/>.
        /// </exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the <paramref name="principal"/> contains more than one identity.</exception>

        public static DPoPAccessCredentials Create(ClaimsPrincipal principal)
        {
            ArgumentNullException.ThrowIfNull(principal);
            ArgumentNullException.ThrowIfNull(principal.Identity);
            ArgumentOutOfRangeException.ThrowIfNotEqual(principal.Identities.Count(), 1);

            ClaimsIdentity? claimsIdentity = principal.Identity as ClaimsIdentity;

            if (claimsIdentity is null)
            {
                ArgumentNullException.ThrowIfNull(claimsIdentity);
            }

            return Create(claimsIdentity);
        }

        /// <summary>
        /// Creates a <see cref="DPoPAccessCredentials"/> instance from the <paramref name="identity"/>.
        /// </summary>
        /// <param name="identity">The <see cref="ClaimsIdentity"/> containing appropriate claims.</param>
        /// <returns>A <see cref="DPoPAccessCredentials"/> created from the claims in the specified <paramref name="identity"/>.</returns>
        /// <exception cref="CredentialException">Thrown when the <paramref name="identity"/> does not contain the required claims, or the claim values are invalid.</exception>
        public static DPoPAccessCredentials Create(ClaimsIdentity identity)
        {
            ArgumentNullException.ThrowIfNull(identity);

            string? didAsString = identity.Claims?.FirstOrDefault(x => x.Type.Equals(AtProtoClaims.Did, StringComparison.Ordinal))?.Value;
            string? accessJwt = identity.Claims?.FirstOrDefault(x => x.Type.Equals(AtProtoClaims.AccessToken, StringComparison.Ordinal))?.Value;
            string? refreshToken = identity.Claims?.FirstOrDefault(x => x.Type.Equals(AtProtoClaims.RefreshToken, StringComparison.Ordinal))?.Value;
            string? dPoPProofKey = identity.Claims?.FirstOrDefault(x => x.Type.Equals(AtProtoClaims.DPoPProof, StringComparison.Ordinal))?.Value;
            string? dPoPNonce = identity.Claims?.FirstOrDefault(x => x.Type.Equals(AtProtoClaims.DPoPNonce, StringComparison.Ordinal))?.Value;
            string? serviceAsString = identity.Claims?.FirstOrDefault(x => x.Type.Equals(AtProtoClaims.Did, StringComparison.Ordinal))?.Issuer;

            if (didAsString is null)
            {
                throw new CredentialException("Missing DID claim");
            }

            if (accessJwt is null)
            {
                throw new CredentialException("Missing Access Token claim");
            }

            if (refreshToken is null)
            {
                throw new CredentialException("Missing Refresh Token claim");
            }

            if (dPoPProofKey is null)
            {
                throw new CredentialException("Missing DPoP Proof claim");
            }

            if (dPoPNonce is null)
            {
                throw new CredentialException("Missing DPoP Nonce claim");
            }

            if (serviceAsString is null)
            {
                throw new CredentialException("Missing issuer on DID claim.");
            }

            if (!Did.TryParse(didAsString, out Did? _))
            {
                throw new CredentialException("Invalid DID.");
            }

            if (!Uri.TryCreate(
                serviceAsString,
                new UriCreationOptions(),
                out Uri? service))
            {
                throw new CredentialException("Invalid issuer on DID claim.");
            }

            return new DPoPAccessCredentials(
                service: service,
                accessJwt: accessJwt,
                refreshToken: refreshToken,
                dPoPProofKey: dPoPProofKey,
                dPoPNonce: dPoPNonce);
        }
    }
}
