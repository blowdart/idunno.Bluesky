// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Numerics;
using System.Security.Cryptography;

namespace idunno.AtProto.Repo;

/// <summary>
/// Provides the orders of the supported signature curves.
/// </summary>
internal static class SigningCurve
{
    internal const string P256Oid = "1.2.840.10045.3.1.7";
    internal const string Secp256k1Oid = "1.3.132.0.10";

    private static readonly BigInteger s_p256Order = new(
        Convert.FromHexString("FFFFFFFF00000000FFFFFFFFFFFFFFFFBCE6FAADA7179E84F3B9CAC2FC632551"),
        isUnsigned: true, isBigEndian: true);
    private static readonly BigInteger s_secp256k1Order = new(
        Convert.FromHexString("FFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFEBAAEDCE6AF48A03BBFD25E8CD0364141"),
        isUnsigned: true, isBigEndian: true);

    internal static BigInteger GetOrder(string oid) => oid switch
    {
        P256Oid => s_p256Order,
        Secp256k1Oid => s_secp256k1Order,
        _ => throw new CryptographicException("The signing key uses an unsupported curve.")
    };

    internal static void NormalizeSignature(Span<byte> signature, string oid)
    {
        BigInteger order = GetOrder(oid);
        if (signature.Length != 64)
        {
            throw new CryptographicException("The signing provider did not produce a compact signature.");
        }

        BigInteger r = new(signature[..32], isUnsigned: true, isBigEndian: true);
        BigInteger s = new(signature[32..], isUnsigned: true, isBigEndian: true);
        if (r <= 0 || r >= order || s <= 0 || s >= order)
        {
            throw new CryptographicException("The signing provider produced invalid signature scalars.");
        }

        if (s > order / 2)
        {
            byte[] lowS = (order - s).ToByteArray(isUnsigned: true, isBigEndian: true);
            signature[32..].Clear();
            lowS.CopyTo(signature[(64 - lowS.Length)..]);
        }
    }
}
