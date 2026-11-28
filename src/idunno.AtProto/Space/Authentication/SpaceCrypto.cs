// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Security.Cryptography;
using System.Text;

using idunno.AtProto.Repo;

#pragma warning disable IDE0130 // Namespace does not match folder structure.
namespace idunno.AtProto.Space;
#pragma warning restore IDE0130 // Namespace does not match folder structure.

/// <summary>
/// Implements the export-only P-256 HPKE suite specified by Spaces, using platform ECDH and HKDF.
/// </summary>
internal static class SpaceCrypto
{
    private static readonly byte[] s_kemId = [0x4b, 0x45, 0x4d, 0x00, 0x10];
    private static readonly byte[] s_suiteId = [0x48, 0x50, 0x4b, 0x45, 0x00, 0x10, 0x00, 0x01, 0xff, 0xff];

    /// <summary>Encodes a P-256 public point as a compressed multikey DID.</summary>
    internal static Did ToDidKey(ECParameters parameters)
    {
        byte[] x = parameters.Q.X ?? throw new InvalidDataException("The P-256 public key has no x coordinate.");
        byte[] y = parameters.Q.Y ?? throw new InvalidDataException("The P-256 public key has no y coordinate.");
        if (parameters.Curve.Oid.Value != ECCurve.NamedCurves.nistP256.Oid.Value || x.Length != 32 || y.Length != 32)
        {
            throw new InvalidDataException("The public key must use P-256.");
        }

        byte[] multikey = new byte[35];
        multikey[0] = 0x80;
        multikey[1] = 0x24;
        multikey[2] = (byte)(0x02 | (y[^1] & 1));
        x.CopyTo(multikey, 3);
        return new Did("did:key:z" + SimpleBase.Base58.Bitcoin.Encode(multikey));
    }

    /// <summary>Parses a compressed P-256 key DID without resolving it over the network.</summary>
    internal static ECDiffieHellman ParseDidKey(Did did)
    {
        const string prefix = "did:key:";
        if (!did.Value.StartsWith(prefix, StringComparison.Ordinal))
        {
            throw new InvalidDataException("A P-256 did:key is required.");
        }

        return SigningKeyVerifier.ParseKey(did.Value[prefix.Length..]).CreateKeyAgreement();
    }

    /// <summary>Encodes a P-256 public point in HPKE's uncompressed form.</summary>
    internal static byte[] EncodePoint(ECParameters parameters)
    {
        byte[] x = parameters.Q.X ?? throw new InvalidDataException("The public point has no x coordinate.");
        byte[] y = parameters.Q.Y ?? throw new InvalidDataException("The public point has no y coordinate.");
        if (x.Length != 32 || y.Length != 32)
        {
            throw new InvalidDataException("The public point must use P-256.");
        }

        byte[] result = new byte[65];
        result[0] = 0x04;
        x.CopyTo(result, 1);
        y.CopyTo(result, 33);
        return result;
    }

    /// <summary>
    /// Derives an RFC 9180 base-mode export for KEM 0x0010, KDF 0x0001 and AEAD 0xffff.
    /// </summary>
    /// <param name="sender">The ephemeral private key.</param>
    /// <param name="recipient">The author's public KEM key.</param>
    /// <param name="info">The protocol label, normally <c>atproto-mac</c>.</param>
    /// <param name="exporterContext">The exporter context, empty for Spaces.</param>
    /// <param name="length">The number of exported bytes, 32 for Spaces.</param>
    /// <returns>The exported secret, which the caller must erase after verifying the response.</returns>
    internal static byte[] Export(
        ECDiffieHellman sender,
        ECDiffieHellman recipient,
        ReadOnlySpan<byte> info,
        ReadOnlySpan<byte> exporterContext,
        int length)
    {
        using ECDiffieHellmanPublicKey publicKey = recipient.PublicKey;
        byte[] dh = sender.DeriveRawSecretAgreement(publicKey);
        List<byte[]> secrets = [dh];
        try
        {
            byte[] eae = Extract(id: s_kemId, salt: [], label: "eae_prk", input: dh);
            secrets.Add(eae);
            byte[] context = [
                .. EncodePoint(sender.ExportParameters(false)),
                .. EncodePoint(recipient.ExportParameters(false))];
            byte[] kemSecret = Expand(id: s_kemId, key: eae, label: "shared_secret", info: context, length: 32);
            secrets.Add(kemSecret);
            byte[] pskHash = Extract(id: s_suiteId, salt: [], label: "psk_id_hash", input: []);
            byte[] infoHash = Extract(id: s_suiteId, salt: [], label: "info_hash", input: info);
            byte[] secret = Extract(id: s_suiteId, salt: kemSecret, label: "secret", input: []);
            secrets.Add(secret);
            byte[] exporter = Expand(id: s_suiteId, key: secret, label: "exp", info: [0, .. pskHash, .. infoHash], length: 32);
            secrets.Add(exporter);
            return Expand(id: s_suiteId, key: exporter, label: "sec", info: exporterContext, length: length);
        }
        finally
        {
            foreach (byte[] secret in secrets)
            {
                CryptographicOperations.ZeroMemory(secret);
            }
        }
    }

    /// <summary>Performs HPKE's domain-separated HKDF extract.</summary>
    private static byte[] Extract(byte[] id, ReadOnlySpan<byte> salt, string label, ReadOnlySpan<byte> input)
    {
        byte[] labeled = [.. "HPKE-v1"u8, .. id, .. Encoding.ASCII.GetBytes(label), .. input];
        byte[] output = new byte[32];
        try
        {
            HKDF.Extract(hashAlgorithmName: HashAlgorithmName.SHA256, ikm: labeled, salt: salt, prk: output);
            return output;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(labeled);
        }
    }

    /// <summary>Performs HPKE's domain-separated HKDF expand.</summary>
    private static byte[] Expand(byte[] id, byte[] key, string label, ReadOnlySpan<byte> info, int length)
    {
        byte[] labeled = [
            (byte)(length >> 8), (byte)length, .. "HPKE-v1"u8, .. id, .. Encoding.ASCII.GetBytes(label), .. info];
        byte[] result = new byte[length];
        HKDF.Expand(hashAlgorithmName: HashAlgorithmName.SHA256, prk: key, output: result, info: labeled);
        return result;
    }
}
