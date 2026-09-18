// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using Duende.IdentityModel.Client;
using Duende.IdentityModel.OidcClient.DPoP;

namespace idunno.AtProto.Authentication;

/// <summary>
/// Encapsulates the credentials and supporting values necessary to call an authenticated API, with proof of possession.
/// </summary>
public sealed class DPoPAccessCredentials : AccessCredentials, IDPoPBoundCredential
{
#if NET9_0_OR_GREATER
    private readonly Lock _dPoPAccessCredentialsLock = new();
#else
    private readonly object _dPoPAccessCredentialsLock = new();
#endif

    private string _dPoPProofKey;
    private string _dPoPNonce;
    private DefaultDPoPProofTokenFactory? _proofTokenFactory;

    /// <summary>
    /// Creates a new instance of <see cref="DPoPAccessCredentials"/> with the specified <paramref name="accessJwt"/>, <paramref name="refreshToken"/>,
    /// <paramref name="dPoPProofKey"/> and <paramref name="dPoPNonce"/>.
    /// </summary>
    /// <param name="service">The <see cref="Uri"/> of the service the credentials were issued from.</param>
    /// <param name="accessJwt">A string representation of the JWT to use when making authenticated access requests.</param>
    /// <param name="refreshToken">A string representation of the refresh token to use when a new access token is required.</param>
    /// <param name="dPoPProofKey">An optional string representation of the DPoP proof key to use when signing requests.</param>
    /// <param name="dPoPNonce">An optional string representation of the DPoP nonce to use when signing requests.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="service"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="accessJwt"/>, <paramref name="refreshToken"/>, <paramref name="dPoPProofKey"/> or <paramref name="dPoPNonce"/> is <see langword="null"/> or whitespace.
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
    /// <exception cref="ArgumentException">Thrown when setting the value and the value is <see langword="null"/> or whitespace.</exception>
    public string DPoPProofKey
    {
        get
        {
            lock (_dPoPAccessCredentialsLock)
            {
                return _dPoPProofKey;
            }
        }

        set
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(value);

            lock (_dPoPAccessCredentialsLock)
            {
                _dPoPProofKey = value;
                _proofTokenFactory = null;
            }
        }
    }

    /// <summary>
    /// Gets a string representation of the DPoP nonce to use when signing requests.
    /// </summary>
    /// <exception cref="ArgumentException">Thrown when setting the value and the value is <see langword="null"/> or whitespace.</exception>
    public string DPoPNonce
    {
        get
        {
            lock (_dPoPAccessCredentialsLock)
            {
                return _dPoPNonce;
            }
        }

        set
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(value);

            lock (_dPoPAccessCredentialsLock)
            {
                _dPoPNonce = value;
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
    ///   The <see cref="AccessCredentials.AccessJwt">access token</see> is read once so that the proof token's <c>ath</c>
    ///   claim and the token presented in the authorization header are always bound to the same value.
    /// </para>
    /// <para>
    ///   The proof token factory is cached and rebuilt only when the <see cref="DPoPProofKey"/> changes. Building it imports
    ///   the key, which every request sharing this credential would otherwise pay for whilst holding the lock.
    /// </para>
    /// </remarks>
    public override void SetAuthenticationHeaders(HttpRequestMessage httpRequestMessage)
    {
        ArgumentNullException.ThrowIfNull(httpRequestMessage);

        lock (_dPoPAccessCredentialsLock)
        {
            string accessJwt = AccessJwt;

            DPoPProofRequest dPoPProofRequest = new()
            {
                AccessToken = accessJwt,
                DPoPNonce = _dPoPNonce,
                Method = httpRequestMessage.Method.ToString(),
                Url = httpRequestMessage.GetDPoPUrl()
            };

            _proofTokenFactory ??= new DefaultDPoPProofTokenFactory(_dPoPProofKey);
            DPoPProof proofToken = _proofTokenFactory.CreateProofToken(dPoPProofRequest);

            httpRequestMessage.SetDPoPToken(accessJwt, proofToken.ProofToken);
        }
    }
}
