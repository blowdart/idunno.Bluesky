// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

namespace idunno.Bluesky.Labeler.Signing;

/// <summary>Describes the public identity of the active signing key in one label stream.</summary>
public sealed class LabelSigningKeySelection
{
    /// <summary>Initializes a new instance of the <see cref="LabelSigningKeySelection"/> class.</summary>
    /// <param name="streamId">The bound label stream identity.</param>
    /// <param name="issuer">The bound issuer DID.</param>
    /// <param name="keyId">The selected key identity.</param>
    /// <param name="publicKey">The selected public multibase key.</param>
    public LabelSigningKeySelection(Guid streamId, string issuer, string keyId, string publicKey)
    {
        StreamId = streamId;
        Issuer = issuer;
        KeyId = keyId;
        PublicKey = publicKey;
    }

    /// <summary>Gets the bound label stream identity.</summary>
    /// <value>The stream identity.</value>
    public Guid StreamId { get; }

    /// <summary>Gets the bound issuer DID.</summary>
    /// <value>The issuer DID.</value>
    public string Issuer { get; }

    /// <summary>Gets the selected key identity.</summary>
    /// <value>The key identity.</value>
    public string KeyId { get; }

    /// <summary>Gets the selected public multibase key.</summary>
    /// <value>The public key, never private material.</value>
    public string PublicKey { get; }
}
