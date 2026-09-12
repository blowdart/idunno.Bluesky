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
            ArgumentException.ThrowIfNullOrWhiteSpace(value);

            lock (_lock)
            {
                field = value;
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