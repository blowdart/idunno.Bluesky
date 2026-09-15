// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using Duende.IdentityModel.Client;
using Duende.IdentityModel.OidcClient.DPoP;

namespace idunno.AtProto.Authentication;

internal class DPoPRevokeCredentials : AtProtoCredential, IDPoPBoundCredential
{
#if NET9_0_OR_GREATER
    private readonly Lock _lock = new();
#else
    private readonly object _lock = new();
#endif

    public DPoPRevokeCredentials(Uri service, string token, string dPoPProofKey, string dPoPNonce) : base(service, AuthenticationType.OAuth)
    {
        ArgumentNullException.ThrowIfNull(service);
        ArgumentException.ThrowIfNullOrWhiteSpace(token);
        ArgumentException.ThrowIfNullOrEmpty(dPoPProofKey);

        DPoPProofKey = dPoPProofKey;
        DPoPNonce = dPoPNonce;
        Token = token;
    }

    public DPoPRevokeCredentials(DPoPAccessCredentials accessCredentials) : base(accessCredentials.Service, AuthenticationType.OAuth)
    {
        ArgumentNullException.ThrowIfNull(accessCredentials);

        DPoPProofKey = accessCredentials.DPoPProofKey;
        DPoPNonce = accessCredentials.DPoPNonce;
        Token = accessCredentials.AccessJwt;
    }

    public string Token
    {
        get
        {
            lock (_lock)
            {
                return field;
            }
        }

        set
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(value);

            lock (_lock)
            {
                field = value;
            }
        }
    }

    public string DPoPProofKey
    {
        get
        {
            lock (_lock)
            {
                return field;
            }
        }

        set
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(value);

            lock (_lock)
            {
                field = value;
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
            lock (_lock)
            {
                return field;
            }
        }

        set
        {
            lock (_lock)
            {
                field = value ?? string.Empty;
            }
        }
    }

    public override void SetAuthenticationHeaders(HttpRequestMessage httpRequestMessage)
    {
        ArgumentNullException.ThrowIfNull(httpRequestMessage);

        lock (_lock)
        {

            DPoPProofRequest dPoPProofRequest = new()
            {
                DPoPNonce = DPoPNonce,
                Method = httpRequestMessage.Method.ToString(),
                Url = httpRequestMessage.GetDPoPUrl()
            };

            DefaultDPoPProofTokenFactory factory = new(DPoPProofKey);
            DPoPProof proofToken = factory.CreateProofToken(dPoPProofRequest);

            httpRequestMessage.SetDPoPToken(Token, proofToken.ProofToken);
        }
    }
}