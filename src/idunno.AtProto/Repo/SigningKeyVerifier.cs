// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Globalization;
using System.Numerics;
using System.Security.Cryptography;

namespace idunno.AtProto.Repo;

/// <summary>
/// Verifies AT Proto signatures against a verification method published in a DID document.
/// </summary>
/// <remarks>
/// <para>Supports compressed secp256k1 and P-256 multikeys, which are the key types AT Proto uses for repository commits and labels.</para>
/// </remarks>
internal static class SigningKeyVerifier
{
    /// <summary>
    /// The verification method fragment used to sign repository commits.
    /// </summary>
    internal const string RepositorySigningKeyFragment = "atproto";

    /// <summary>
    /// The verification method fragment used to sign labels.
    /// </summary>
    internal const string LabelSigningKeyFragment = "atproto_label";

    /// <summary>
    /// Verifies <paramref name="signature"/> over <paramref name="signedData"/> using the key identified by <paramref name="fragment"/>
    /// in <paramref name="didDocument"/>.
    /// </summary>
    /// <param name="didDocument">The resolved DID document for <paramref name="did"/>.</param>
    /// <param name="did">The DID whose key signed the data.</param>
    /// <param name="fragment">The fragment, without a leading <c>#</c>, naming the verification method to use.</param>
    /// <param name="signedData">The bytes which were signed.</param>
    /// <param name="signature">The 64 byte compact signature.</param>
    /// <param name="subject">A description of what was signed, used in exception messages.</param>
    /// <exception cref="InvalidDataException">The DID document has no usable key, or the signature is invalid.</exception>
    /// <exception cref="CryptographicException">The platform cannot use the signing key's curve.</exception>
    internal static void Verify(
        DidDocument? didDocument,
        Did did,
        string fragment,
        ReadOnlySpan<byte> signedData,
        ReadOnlySpan<byte> signature,
        string subject) => GetKey(didDocument, did, fragment).Verify(signedData, signature, subject);

    /// <summary>
    /// Gets the public key identified by <paramref name="fragment"/> in <paramref name="didDocument"/>.
    /// </summary>
    /// <param name="didDocument">The resolved DID document for <paramref name="did"/>.</param>
    /// <param name="did">The DID whose key is wanted.</param>
    /// <param name="fragment">The fragment, without a leading <c>#</c>, naming the verification method to use.</param>
    /// <returns>The public key.</returns>
    /// <exception cref="InvalidDataException">The DID document is missing, is for another DID, or has no usable key.</exception>
    internal static SigningKey GetKey(DidDocument? didDocument, Did did, string fragment)
    {
        if (didDocument is null || didDocument.Id != did)
        {
            throw new InvalidDataException($"The DID document for '{did}' could not be resolved.");
        }

        string methodId = $"{did}#{fragment}";
        VerificationMethod? verificationMethod = null;
        foreach (VerificationMethod method in didDocument.VerificationMethods ?? [])
        {
            if (method.Id == methodId)
            {
                if (verificationMethod is not null)
                {
                    throw new InvalidDataException($"The DID document for '{did}' contains multiple #{fragment} verification keys.");
                }

                verificationMethod = method;
            }
        }

        if (verificationMethod is null ||
            verificationMethod.Controller != did ||
            string.IsNullOrEmpty(verificationMethod.PublicKeyMultibase))
        {
            throw new InvalidDataException($"The DID document for '{did}' has no usable #{fragment} verification key.");
        }

        byte[] publicKeyBytes;
        try
        {
            publicKeyBytes = SimpleBase.Multibase.Decode(verificationMethod.PublicKeyMultibase);
        }
        catch (Exception exception) when (exception is FormatException or ArgumentException)
        {
            throw new InvalidDataException("The DID document contains an invalid multibase public key.", exception);
        }

        string curveOid;
        BigInteger prime;
        BigInteger curveA;
        BigInteger curveB;
        if (publicKeyBytes.Length != 35)
        {
            throw new InvalidDataException("The signing key must be a compressed P-256 or secp256k1 multikey.");
        }

        if (publicKeyBytes[0] == 0xE7 && publicKeyBytes[1] == 0x01)
        {
            curveOid = "1.3.132.0.10";
            prime = (BigInteger.One << 256) - (BigInteger.One << 32) - 977;
            curveA = BigInteger.Zero;
            curveB = 7;
        }
        else if (publicKeyBytes[0] == 0x80 && publicKeyBytes[1] == 0x24)
        {
            curveOid = "1.2.840.10045.3.1.7";
            prime = (BigInteger.One << 256) - (BigInteger.One << 224) + (BigInteger.One << 192) +
                (BigInteger.One << 96) - 1;
            curveA = prime - 3;
            curveB = BigInteger.Parse(
                "5AC635D8AA3A93E7B3EBBD55769886BC651D06B0CC53B0F63BCE3C3E27D2604B",
                NumberStyles.HexNumber,
                CultureInfo.InvariantCulture);
        }
        else
        {
            throw new InvalidDataException("The signing key uses an unsupported multikey curve.");
        }

        byte[] compressedPoint = publicKeyBytes.AsSpan(2).ToArray();
        if (compressedPoint[0] is not 0x02 and not 0x03)
        {
            throw new InvalidDataException("The signing key is not a compressed elliptic curve public key.");
        }

        byte[] x = compressedPoint.AsSpan(1).ToArray();
        byte[] y = DecompressPoint(compressedPoint[0], x, prime, curveA, curveB);

        return new SigningKey(curveOid, x, y);
    }

    private static byte[] DecompressPoint(byte prefix, byte[] xBytes, BigInteger prime, BigInteger curveA, BigInteger curveB)
    {
        BigInteger x = new(xBytes, isUnsigned: true, isBigEndian: true);
        if (x >= prime)
        {
            throw new InvalidDataException("The signing key has an invalid elliptic curve point.");
        }

        BigInteger ySquared = Mod(BigInteger.Pow(x, 3) + (curveA * x) + curveB, prime);
        BigInteger y = BigInteger.ModPow(ySquared, (prime + 1) >> 2, prime);
        if (Mod(BigInteger.Pow(y, 2), prime) != ySquared)
        {
            throw new InvalidDataException("The signing key has an invalid elliptic curve point.");
        }

        bool yIsOdd = !y.IsEven;
        if (yIsOdd != (prefix == 0x03))
        {
            y = prime - y;
        }

        if (y >= prime)
        {
            throw new InvalidDataException("The signing key has an invalid elliptic curve point.");
        }

        byte[] yBytes = y.ToByteArray(isUnsigned: true, isBigEndian: true);
        byte[] paddedY = new byte[32];
        yBytes.CopyTo(paddedY, paddedY.Length - yBytes.Length);
        return paddedY;
    }

    private static BigInteger Mod(BigInteger value, BigInteger modulus)
    {
        BigInteger result = value % modulus;
        return result.Sign < 0 ? result + modulus : result;
    }
}

/// <summary>
/// An uncompressed P-256 or secp256k1 public key, taken from a DID document verification method.
/// </summary>
/// <param name="curveOid">The object identifier of the key's curve.</param>
/// <param name="x">The x coordinate of the public point.</param>
/// <param name="y">The y coordinate of the public point.</param>
internal sealed class SigningKey(string curveOid, byte[] x, byte[] y)
{
    /// <summary>
    /// Verifies <paramref name="signature"/> over <paramref name="signedData"/>.
    /// </summary>
    /// <param name="signedData">The bytes which were signed.</param>
    /// <param name="signature">The 64 byte compact signature.</param>
    /// <param name="subject">A description of what was signed, used in exception messages.</param>
    /// <exception cref="InvalidDataException">The signature is invalid.</exception>
    /// <exception cref="CryptographicException">The platform cannot use the key's curve.</exception>
    internal void Verify(ReadOnlySpan<byte> signedData, ReadOnlySpan<byte> signature, string subject)
    {
        using ECDsa verifier = ECDsa.Create(new ECParameters
        {
            Curve = ECCurve.CreateFromValue(curveOid),
            Q = new ECPoint { X = x, Y = y }
        });

        byte[] digest = SHA256.HashData(signedData);
        if (signature.Length != 64 ||
            !verifier.VerifyHash(digest, signature, DSASignatureFormat.IeeeP1363FixedFieldConcatenation))
        {
            throw new InvalidDataException($"The {subject} signature is invalid.");
        }
    }
}
