// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Security.Claims;

namespace idunno.AtProto.Authentication;

/// <summary>
/// Base class for credentials to access an ATProto service.
/// </summary>
/// <param name="service">The service the credentials were issued from.</param>
/// <param name="authenticationType">The type of authentication.</param>
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
    /// Returns a static string to prevent sensitive information from being included in logs or error messages.
    /// </summary>
    /// <returns>A static string to prevent sensitive information from being included in logs or error messages.</returns>
    public override string ToString() => "[REDACTED]";

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
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="service"/> is <see langword="null"/>.</exception>
    /// <exception cref="AtProtoException">Thrown when the combination of parameters does not match a known AtProtoCredentials subtype.</exception>
    /// <exception cref="ArgumentException">Thrown when required parameters for the specified <paramref name="authenticationType"/> are missing or invalid.</exception>
    public static AtProtoCredential Create(
        Uri service,
        AuthenticationType authenticationType,
        string? accessJwt = null,
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
    /// Tries to create a <see cref="DPoPAccessCredentials"/> instance from the <paramref name="principal"/>.
    /// </summary>
    /// <param name="principal">The <see cref="ClaimsPrincipal"/> containing appropriate claims.</param>
    /// <param name="credentials">When this method returns, contains the <see cref="DPoPAccessCredentials"/> created from the claims in the specified <paramref name="principal"/>, or <see langword="null"/> if the creation failed.</param>
    /// <returns><see langword="true"/> if the <see cref="DPoPAccessCredentials"/> was successfully created; otherwise, <see langword="false"/>.</returns>
    public static bool TryCreate(ClaimsPrincipal principal, out DPoPAccessCredentials? credentials)
    {
        if (principal == null || principal.Identity == null || principal.Identities.Count() != 1)
        {
            credentials = null;
            return false;
        }

        if (principal.Identity is not ClaimsIdentity claimsIdentity)
        {
            credentials = null;
            return false;
        }

        if (!claimsIdentity.IsAuthenticated)
        {
            credentials = null;
            return false;
        }

        return TryCreate(claimsIdentity, out credentials);
    }

    /// <summary>
    /// Tries to create a <see cref="DPoPAccessCredentials"/> instance from the <paramref name="claimsIdentity"/>.
    /// </summary>
    /// <param name="claimsIdentity">The <see cref="ClaimsIdentity"/> containing appropriate claims.</param>
    /// <param name="credentials">When this method returns, contains the <see cref="DPoPAccessCredentials"/> created from the claims in the specified <paramref name="claimsIdentity"/>, or <see langword="null"/> if the creation failed.</param>
    /// <returns><see langword="true"/> if the <see cref="DPoPAccessCredentials"/> was successfully created; otherwise, <see langword="false"/>.</returns>
    /// <exception cref="CredentialException">Thrown when the <paramref name="claimsIdentity"/> does not contain the required claims, or the claim values are invalid.</exception>
    /// <exception cref="ArgumentNullException">Thrown when the <paramref name="claimsIdentity"/> is <see langword="null"/>.</exception>
    public static bool TryCreate(ClaimsIdentity claimsIdentity, out DPoPAccessCredentials? credentials)
    {
        if (claimsIdentity is null)
        {
            credentials = null;
            return false;
        }

        if (!claimsIdentity.IsAuthenticated)
        {
            credentials = null;
            return false;
        }

        string? didAsString = claimsIdentity.Claims?.FirstOrDefault(x => x.Type.Equals(AtProtoClaims.Did, StringComparison.Ordinal))?.Value;
        string? accessJwt = claimsIdentity.Claims?.FirstOrDefault(x => x.Type.Equals(AtProtoClaims.AccessToken, StringComparison.Ordinal))?.Value;
        string? refreshToken = claimsIdentity.Claims?.FirstOrDefault(x => x.Type.Equals(AtProtoClaims.RefreshToken, StringComparison.Ordinal))?.Value;
        string? dPoPProofKey = claimsIdentity.Claims?.FirstOrDefault(x => x.Type.Equals(AtProtoClaims.DPoPProof, StringComparison.Ordinal))?.Value;
        string? dPoPNonce = claimsIdentity.Claims?.FirstOrDefault(x => x.Type.Equals(AtProtoClaims.DPoPNonce, StringComparison.Ordinal))?.Value;
        string? serviceAsString = claimsIdentity.Claims?.FirstOrDefault(x => x.Type.Equals(AtProtoClaims.Did, StringComparison.Ordinal))?.Issuer;

        if (didAsString is null ||
            accessJwt is null ||
            refreshToken is null ||
            dPoPProofKey is null ||
            dPoPNonce is null ||
            serviceAsString is null)
        {
            credentials = null;
            return false;
        }

        if (!Did.TryParse(didAsString, out Did? _))
        {
            credentials = null;
            return false;
        }

        if (!Uri.TryCreate(
            serviceAsString,
            new UriCreationOptions(),
            out Uri? service))
        {
            credentials = null;
            return false;
        }

        credentials = new DPoPAccessCredentials(
            service: service,
            accessJwt: accessJwt,
            refreshToken: refreshToken,
            dPoPProofKey: dPoPProofKey,
            dPoPNonce: dPoPNonce);

        return true;
    }
}