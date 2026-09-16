// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using Duende.IdentityModel.Client;
using Duende.IdentityModel.OidcClient.DPoP;

namespace idunno.AtProto.Authentication;

/// <summary>
/// Encapsulates a refresh token with proof of possession.
/// </summary>
public sealed class DPoPRefreshCredential : RefreshCredential, IDPoPBoundCredential
{
#if NET9_0_OR_GREATER
    private readonly Lock _lock = new();
#else
    private readonly object _lock = new();
#endif

    private string _dPoPProofKey;
    private string _dPoPNonce;

    /// <summary>
    /// Creates a new instance of <see cref="DPoPRefreshCredential"/> with the specified <paramref name="refreshToken"/>, <paramref name="dPoPProofKey"/> and <paramref name="dPoPNonce"/>.
    /// </summary>
    /// <param name="service">The <see cref="Uri"/> of the service the credentials were issued from.</param>
    /// <param name="refreshToken">A string representation of the JWT to use when a new access token is required.</param>
    /// <param name="dPoPProofKey">The string representation of the DPoP proof key to use when signing requests.</param>
    /// <param name="dPoPNonce">The string representation of the DPoP nonce to use when signing requests, if one is known.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="service"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="refreshToken"/> or <paramref name="dPoPProofKey"/> is <see langword="null"/> or whitespace.
    /// </exception>
    /// <remarks>
    /// <para>
    ///   The <paramref name="dPoPNonce"/> may be <see langword="null"/> or empty, in which case it is stored as an empty string.
    ///   See <see cref="DPoPNonce"/>.
    /// </para>
    /// </remarks>
    public DPoPRefreshCredential(Uri service, string refreshToken, string dPoPProofKey, string dPoPNonce) : base(service, AuthenticationType.OAuth, refreshToken)
    {
        ArgumentNullException.ThrowIfNull(service);
        ArgumentException.ThrowIfNullOrWhiteSpace(refreshToken);
        ArgumentException.ThrowIfNullOrEmpty(dPoPProofKey);

        _dPoPProofKey = dPoPProofKey;
        _dPoPNonce = dPoPNonce ?? string.Empty;
    }

    /// <summary>
    /// Creates a new instance of <see cref="DPoPRefreshCredential"/> from the specified <paramref name="dPoPAccessCredentials"/>.
    /// </summary>
    /// <param name="dPoPAccessCredentials">An instance of <see cref="DPoPAccessCredentials"/> to create the refresh token from/</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="dPoPAccessCredentials"/> is <see langword="null"/>.</exception>
    public DPoPRefreshCredential(DPoPAccessCredentials dPoPAccessCredentials) : this(
        dPoPAccessCredentials != null ? dPoPAccessCredentials.Service : throw new ArgumentNullException(nameof(dPoPAccessCredentials)),
        dPoPAccessCredentials.RefreshToken,
        dPoPAccessCredentials.DPoPProofKey,
        dPoPAccessCredentials.DPoPNonce)
    {
    }

    /// <summary>
    /// Gets or sets a string representation of the DPoP proof key to use when signing requests.
    /// </summary>
    /// <exception cref="ArgumentException">Thrown when setting the value and the value is <see langword="null"/> or whitespace.</exception>
    public string DPoPProofKey
    {
        get
        {
            lock (_lock)
            {
                return _dPoPProofKey;
            }
        }

        set
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(value);

            lock (_lock)
            {
                _dPoPProofKey = value;
            }
        }
    }

    /// <summary>
    /// Gets or sets a string representation of the DPoP nonce to use when signing requests.
    /// </summary>
    /// <remarks>
    /// <para>
    ///   This may be empty. A refresh request is made to an authorization server which only supplies a nonce in
    ///   response to the first request, so the first proof has to be signed without one. Setting this to
    ///   <see langword="null"/> stores an empty string.
    /// </para>
    /// </remarks>
    public string DPoPNonce
    {
        get
        {
            lock (_lock)
            {
                return _dPoPNonce;
            }
        }

        set
        {
            lock (_lock)
            {
                _dPoPNonce = value ?? string.Empty;
            }
        }
    }

    /// <summary>
    /// Add authentication headers to the specified <paramref name="httpRequestMessage"/>.
    /// </summary>
    /// <param name="httpRequestMessage">The <see cref="HttpRequestMessage"/> to add authentication headers to.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="httpRequestMessage"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// <para>
    ///   The <see cref="RefreshCredential.RefreshToken">refresh token</see> is read once so that the proof token's <c>ath</c>
    ///   claim and the token presented in the authorization header are always bound to the same value.
    /// </para>
    /// </remarks>
    public override void SetAuthenticationHeaders(HttpRequestMessage httpRequestMessage)
    {
        ArgumentNullException.ThrowIfNull(httpRequestMessage);

        lock (_lock)
        {
            string refreshToken = RefreshToken;

            DPoPProofRequest dPoPProofRequest = new()
            {
                AccessToken = refreshToken,
                DPoPNonce = _dPoPNonce,
                Method = httpRequestMessage.Method.ToString(),
                Url = httpRequestMessage.GetDPoPUrl()
            };

            DefaultDPoPProofTokenFactory factory = new(_dPoPProofKey);
            DPoPProof proofToken = factory.CreateProofToken(dPoPProofRequest);

            httpRequestMessage.SetDPoPToken(refreshToken, proofToken.ProofToken);
        }
    }
}