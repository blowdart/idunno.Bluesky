// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using Duende.IdentityModel.Client;
using Duende.IdentityModel.OidcClient.DPoP;

namespace idunno.AtProto.Authentication;

internal class DPoPRevokeCredentials : AtProtoCredential, IDPoPBoundCredential, IDisposable
{
    private bool _isDisposed;

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
            ReaderWriterLockSlim.EnterReadLock();
            try
            {
                return field;
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
                field = value;
            }
            finally
            {
                ReaderWriterLockSlim.ExitWriteLock();
            }
        }
    }

    public string DPoPProofKey
    {
        get
        {
            ReaderWriterLockSlim.EnterReadLock();
            try
            {
                return field;
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
                field = value;
            }
            finally
            {
                ReaderWriterLockSlim.ExitWriteLock();
            }
        }
    }

    public string DPoPNonce
    {
        get
        {
            ReaderWriterLockSlim.EnterReadLock();
            try
            {
                return field;
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
                field = value;
            }
            finally
            {
                ReaderWriterLockSlim.ExitWriteLock();
            }
        }
    }

    public override void SetAuthenticationHeaders(HttpRequestMessage httpRequestMessage)
    {
        ArgumentNullException.ThrowIfNull(httpRequestMessage);

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

    protected virtual void Dispose(bool disposing)
    {
        if (!_isDisposed)
        {
            if (disposing)
            {
                ReaderWriterLockSlim.Dispose();
            }

            _isDisposed = true;
        }
    }

     ~DPoPRevokeCredentials()
    {
         Dispose(disposing: false);
    }

    public void Dispose()
    {
        // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }
}
