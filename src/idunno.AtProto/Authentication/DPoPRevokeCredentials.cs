// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using Duende.IdentityModel.Client;
using Duende.IdentityModel.OidcClient.DPoP;

namespace idunno.AtProto.Authentication;

internal class DPoPRevokeCredentials : AtProtoCredential, IDPoPBoundCredential
{
#if NET9_0_OR_GREATER
    private readonly Lock _dPoPRevokeCredentialsLock = new();
#else
    private readonly object _dPoPRevokeCredentialsLock = new();
#endif

    private DefaultDPoPProofTokenFactory? _proofTokenFactory;

    public DPoPRevokeCredentials(Uri service, string token, string dPoPProofKey, string dPoPNonce) : base(service, AuthenticationType.OAuth)
    {
        ArgumentNullException.ThrowIfNull(service);
        ArgumentException.ThrowIfNullOrWhiteSpace(token);
        ArgumentException.ThrowIfNullOrEmpty(dPoPProofKey);

        DPoPProofKey = dPoPProofKey;
        DPoPNonce = dPoPNonce;
        Token = token;
    }

    public DPoPRevokeCredentials(DPoPAccessCredentials accessCredentials) : base(
        accessCredentials != null ? accessCredentials.Service : throw new ArgumentNullException(nameof(accessCredentials)),
        AuthenticationType.OAuth)
    {
        DPoPProofKey = accessCredentials.DPoPProofKey;
        DPoPNonce = accessCredentials.DPoPNonce;
        Token = accessCredentials.AccessJwt;
    }

    public string Token
    {
        get
        {
            lock (_dPoPRevokeCredentialsLock)
            {
                return field;
            }
        }

        set
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(value);

            lock (_dPoPRevokeCredentialsLock)
            {
                field = value;
            }
        }
    }

    public string DPoPProofKey
    {
        get
        {
            lock (_dPoPRevokeCredentialsLock)
            {
                return field;
            }
        }

        set
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(value);

            lock (_dPoPRevokeCredentialsLock)
            {
                field = value;
                _proofTokenFactory = null;
            }
        }
    }

    /// <summary>
    /// Gets or sets a string representation of the DPoP nonce to use when signing requests.
    /// </summary>
    /// <remarks>
    /// <para>
    ///   Unlike the nonce on credentials issued by a token endpoint this may be empty. A revocation request is made to
    ///   an authorization server the agent has not necessarily called before, which only supplies a nonce in response
    ///   to the first request, so the first proof has to be signed without one.
    /// </para>
    /// </remarks>
    public string DPoPNonce
    {
        get
        {
            lock (_dPoPRevokeCredentialsLock)
            {
                return field;
            }
        }

        set
        {
            lock (_dPoPRevokeCredentialsLock)
            {
                field = value ?? string.Empty;
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
    ///   The proof for a revocation request deliberately does not carry an <c>ath</c> claim, so
    ///   <see cref="DPoPProofRequest.AccessToken"/> is left unset. Revocation is not a protected resource request, and
    ///   setting it has been tested against a live authorization server and rejected. Do not add it back.
    /// </para>
    /// <para>
    ///   The proof token factory is cached and rebuilt only when the <see cref="DPoPProofKey"/> changes. Building it imports
    ///   the key, which every request sharing this credential would otherwise pay for whilst holding the lock.
    /// </para>
    /// </remarks>
    public override void SetAuthenticationHeaders(HttpRequestMessage httpRequestMessage)
    {
        ArgumentNullException.ThrowIfNull(httpRequestMessage);

        lock (_dPoPRevokeCredentialsLock)
        {
            // Do not set AccessToken here. See the remarks above.
            DPoPProofRequest dPoPProofRequest = new()
            {
                DPoPNonce = DPoPNonce,
                Method = httpRequestMessage.Method.ToString(),
                Url = httpRequestMessage.GetDPoPUrl()
            };

            _proofTokenFactory ??= new DefaultDPoPProofTokenFactory(DPoPProofKey);
            DPoPProof proofToken = _proofTokenFactory.CreateProofToken(dPoPProofRequest);

            httpRequestMessage.SetDPoPToken(Token, proofToken.ProofToken);
        }
    }
}
