// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Security.Cryptography;

using idunno.AtProto.Repo;

namespace idunno.AtProto.Labels;

/// <summary>
/// Verifies signatures on issued labels.
/// </summary>
public static class LabelSignatureVerifier
{
    /// <summary>
    /// Verifies a collection of labels against one issuer's DID document.
    /// </summary>
    /// <param name="didDocument">The issuer's DID document obtained from a trusted identity resolver.</param>
    /// <param name="labels">The issued labels, all from the issuer identified by <paramref name="didDocument"/>.</param>
    /// <exception cref="ArgumentNullException"><paramref name="didDocument"/> or <paramref name="labels"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidDataException">A label is unsigned, has an unsupported version, comes from another issuer, or has an invalid signature, or the document has no matching label key.</exception>
    /// <exception cref="CryptographicException">The platform cannot use the key's curve.</exception>
    /// <remarks>
    /// <para>This method makes no network requests and stops at the first invalid label. It returns normally only if every label verifies.
    /// Verification does not establish trust in the issuer or evaluate expiry, negation, or supersession.</para>
    /// </remarks>
    public static void Verify(DidDocument didDocument, IEnumerable<Label> labels)
    {
        ArgumentNullException.ThrowIfNull(didDocument);
        ArgumentNullException.ThrowIfNull(labels);
        SigningKey key = SigningKeyVerifier.GetKey(didDocument, didDocument.Id, SigningKeyVerifier.LabelSigningKeyFragment);
        foreach (Label label in labels)
        {
            if (label is null || label.Source != didDocument.Id)
            {
                throw new InvalidDataException("Every label must have the DID document's issuer as its source.");
            }

            VerifyLabel(label, key);
        }
    }

    internal static void VerifyLabel(Label label, SigningKey key)
    {
        key.VerifyLabel(GetUnsignedLabel(label), label.Signature!.ToBytes());
    }

    internal static byte[] GetUnsignedLabel(Label label)
    {
        if (label.Version != 1 || label.Signature is null)
        {
            throw new InvalidDataException("A signed label must have version 1 and a signature.");
        }

        return LabelEncoding.Encode(label);
    }
}
