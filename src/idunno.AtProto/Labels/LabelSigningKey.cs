// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;

using idunno.AtProto.Repo;

namespace idunno.AtProto.Labels;

/// <summary>
/// Owns a local private key used to sign version-one AT Protocol labels.
/// </summary>
/// <remarks>
/// <para>Generate or import a dedicated label key explicitly, retain it for subsequent signing, and dispose it when finished.
/// Operations are serialized with disposal. This class does not store keys, publish DID documents, or access repository keys.</para>
/// <para>Private material is available only through explicit export methods. Protect exports and passwords yourself;
/// caller-owned buffers and immutable PEM strings cannot be erased by this class. Curve support depends on the platform provider.</para>
/// </remarks>
[DebuggerDisplay("{Curve}")]
[SuppressMessage("Usage", "CA1054:Uri parameters should not be strings", Justification = "Label subjects are protocol URI strings, including DIDs and AT URIs.")]
public sealed class LabelSigningKey : IDisposable
{
    private readonly ECDsa _key;
    private readonly object _sync = new();
    private bool _disposed;

    private LabelSigningKey(ECDsa key, LabelSigningCurve curve)
    {
        _key = key;
        Curve = curve;
    }

    /// <summary>
    /// Gets the key's elliptic curve.
    /// </summary>
    public LabelSigningCurve Curve { get; }

    /// <summary>
    /// Generates a new local label key using the platform cryptographic provider.
    /// </summary>
    /// <param name="curve">The curve to generate.</param>
    /// <returns>A new caller-owned signing key.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="curve"/> is not supported.</exception>
    /// <exception cref="CryptographicException">The provider cannot generate the key.</exception>
    /// <exception cref="PlatformNotSupportedException">The platform does not support the curve.</exception>
    public static LabelSigningKey Generate(LabelSigningCurve curve)
    {
        string oid = GetOid(curve);
        return new LabelSigningKey(ECDsa.Create(ECCurve.CreateFromValue(oid)), curve);
    }

    /// <summary>
    /// Imports an externally generated or previously exported unencrypted PKCS#8 private key.
    /// </summary>
    /// <param name="source">The complete binary PKCS#8 private key.</param>
    /// <returns>A new caller-owned signing key retaining the imported key's identity.</returns>
    /// <exception cref="CryptographicException">The data is malformed, contains trailing bytes, or is not a supported named-curve private key.</exception>
    /// <exception cref="PlatformNotSupportedException">The platform does not support the key.</exception>
    public static LabelSigningKey ImportPkcs8PrivateKey(ReadOnlySpan<byte> source) => ImportKey(source, default, encrypted: false);

    /// <summary>
    /// Imports a password-encrypted binary PKCS#8 private key.
    /// </summary>
    /// <param name="source">The complete encrypted binary PKCS#8 private key.</param>
    /// <param name="password">The password used to encrypt the key.</param>
    /// <returns>A new caller-owned signing key retaining the imported key's identity.</returns>
    /// <exception cref="ArgumentException"><paramref name="password"/> is empty.</exception>
    /// <exception cref="CryptographicException">The password is incorrect, the data is malformed or has trailing bytes, or the key is unsupported.</exception>
    /// <exception cref="PlatformNotSupportedException">The platform does not support the key.</exception>
    public static LabelSigningKey ImportEncryptedPkcs8PrivateKey(ReadOnlySpan<byte> source, ReadOnlySpan<char> password) =>
        ImportKey(source, password, encrypted: true);

    /// <summary>
    /// Imports an unencrypted PKCS#8 private key from <paramref name="pem"/>.
    /// </summary>
    /// <param name="pem">A single <c>PRIVATE KEY</c> PEM block, optionally surrounded by whitespace.</param>
    /// <returns>A new caller-owned signing key.</returns>
    /// <exception cref="ArgumentException"><paramref name="pem"/> does not contain exactly one unencrypted PKCS#8 block.</exception>
    /// <exception cref="CryptographicException">The private key is malformed or unsupported.</exception>
    /// <exception cref="PlatformNotSupportedException">The platform does not support the key.</exception>
    public static LabelSigningKey ImportFromPem(ReadOnlySpan<char> pem) => ImportPem(pem, default, encrypted: false);

    /// <summary>
    /// Imports a password-encrypted PKCS#8 private key from <paramref name="pem"/>.
    /// </summary>
    /// <param name="pem">A single <c>ENCRYPTED PRIVATE KEY</c> PEM block, optionally surrounded by whitespace.</param>
    /// <param name="password">The password used to encrypt the key.</param>
    /// <returns>A new caller-owned signing key.</returns>
    /// <exception cref="ArgumentException"><paramref name="pem"/> is not exactly one encrypted PKCS#8 block, or <paramref name="password"/> is empty.</exception>
    /// <exception cref="CryptographicException">The password is incorrect or the private key is malformed or unsupported.</exception>
    /// <exception cref="PlatformNotSupportedException">The platform does not support the key.</exception>
    public static LabelSigningKey ImportFromEncryptedPem(ReadOnlySpan<char> pem, ReadOnlySpan<char> password) =>
        ImportPem(pem, password, encrypted: true);

    /// <summary>
    /// Exports the private key as unencrypted binary PKCS#8.
    /// </summary>
    /// <returns>A secret buffer which the caller must protect and clear after use.</returns>
    /// <exception cref="ObjectDisposedException">The key has been disposed.</exception>
    /// <exception cref="CryptographicException">The key cannot be exported.</exception>
    public byte[] ExportPkcs8PrivateKey()
    {
        lock (_sync)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            return _key.ExportPkcs8PrivateKey();
        }
    }

    /// <summary>
    /// Exports the private key as password-encrypted binary PKCS#8.
    /// </summary>
    /// <param name="password">A nonempty password for the export.</param>
    /// <param name="pbeParameters">The explicit encryption and password-derivation parameters.</param>
    /// <returns>A new buffer containing the encrypted private key.</returns>
    /// <exception cref="ArgumentException"><paramref name="password"/> is empty, or the encryption parameters are invalid.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="pbeParameters"/> is <see langword="null"/>.</exception>
    /// <exception cref="ObjectDisposedException">The key has been disposed.</exception>
    /// <exception cref="CryptographicException">The key cannot be exported with the specified encryption.</exception>
    public byte[] ExportEncryptedPkcs8PrivateKey(ReadOnlySpan<char> password, PbeParameters pbeParameters)
    {
        lock (_sync)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            ValidatePassword(password);
            ArgumentNullException.ThrowIfNull(pbeParameters);
            return _key.ExportEncryptedPkcs8PrivateKey(password, pbeParameters);
        }
    }

    /// <summary>
    /// Exports the private key as unencrypted PKCS#8 PEM.
    /// </summary>
    /// <returns>A secret immutable string which cannot be cleared from memory.</returns>
    /// <exception cref="ObjectDisposedException">The key has been disposed.</exception>
    /// <exception cref="CryptographicException">The key cannot be exported.</exception>
    public string ExportPkcs8PrivateKeyPem()
    {
        byte[] encoded = ExportPkcs8PrivateKey();
        try
        {
            return PemEncoding.WriteString("PRIVATE KEY", encoded);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(encoded);
        }
    }

    /// <summary>
    /// Exports the private key as password-encrypted PKCS#8 PEM.
    /// </summary>
    /// <param name="password">A nonempty password for the export.</param>
    /// <param name="pbeParameters">The explicit encryption and password-derivation parameters.</param>
    /// <returns>A string containing the encrypted private key.</returns>
    /// <exception cref="ArgumentException"><paramref name="password"/> is empty, or the encryption parameters are invalid.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="pbeParameters"/> is <see langword="null"/>.</exception>
    /// <exception cref="ObjectDisposedException">The key has been disposed.</exception>
    /// <exception cref="CryptographicException">The key cannot be exported with the specified encryption.</exception>
    public string ExportEncryptedPkcs8PrivateKeyPem(ReadOnlySpan<char> password, PbeParameters pbeParameters)
    {
        byte[] encoded = ExportEncryptedPkcs8PrivateKey(password, pbeParameters);
        try
        {
            return PemEncoding.WriteString("ENCRYPTED PRIVATE KEY", encoded);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(encoded);
        }
    }

    /// <summary>
    /// Exports the public key as an AT Protocol compressed multikey.
    /// </summary>
    /// <returns>A base58btc multibase string containing the curve codec and compressed public point.</returns>
    /// <exception cref="ObjectDisposedException">The key has been disposed.</exception>
    /// <exception cref="CryptographicException">The public key cannot be exported.</exception>
    /// <remarks>
    /// <para>This contains no private material and does not publish or modify a DID document.</para>
    /// </remarks>
    public string ExportPublicKeyMultibase()
    {
        lock (_sync)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            ECPoint point = _key.ExportParameters(includePrivateParameters: false).Q;
            byte[] prefix = Curve == LabelSigningCurve.P256 ? [0x80, 0x24] : [0xE7, 0x01];
            byte[] encoded = [.. prefix, (point.Y![^1] & 1) == 0 ? (byte)0x02 : (byte)0x03, .. point.X!];
            return $"z{SimpleBase.Base58.Bitcoin.Encode(encoded)}";
        }
    }

    /// <summary>
    /// Signs a version-one label, returning a copy with a compact low-S signature.
    /// </summary>
    /// <param name="label">The label to sign or re-sign.</param>
    /// <returns>A new signed label preserving every signed field and its retained representation.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="label"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidDataException">The label has invalid signed fields or a missing or unsupported version.</exception>
    /// <exception cref="ObjectDisposedException">The key has been disposed.</exception>
    /// <exception cref="CryptographicException">The provider cannot sign the payload.</exception>
    /// <remarks>
    /// <para>No timestamps are changed. This method does not resolve the issuer or establish that its DID document
    /// publishes this key. Signing does not evaluate trust, expiry, or negation history.</para>
    /// </remarks>
    public Label Sign(Label label)
    {
        lock (_sync)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            ArgumentNullException.ThrowIfNull(label);
            byte[] signature = _key.SignData(label.GetSigningBytes(), HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
            SigningCurve.NormalizeSignature(signature, GetOid(Curve));
            return label with { Signature = new Bytes(signature) };
        }
    }

    /// <summary>
    /// Creates and signs a version-one ordinary label with the negation field omitted.
    /// </summary>
    /// <param name="source">The issuer DID.</param>
    /// <param name="uri">The subject URI.</param>
    /// <param name="value">The label value, at most 128 UTF-8 bytes.</param>
    /// <param name="creationTimestamp">The explicit creation time.</param>
    /// <param name="cid">An optional subject version.</param>
    /// <param name="expiresAt">An optional expiry time.</param>
    /// <returns>A new signed ordinary label.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/>, <paramref name="uri"/>, or <paramref name="value"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="value"/> exceeds 128 UTF-8 bytes.</exception>
    /// <exception cref="InvalidDataException">The subject or timestamps are invalid.</exception>
    /// <exception cref="ObjectDisposedException">The key has been disposed.</exception>
    /// <exception cref="CryptographicException">The provider cannot sign the label.</exception>
    public Label CreateLabel(Did source, string uri, string value, DateTimeOffset creationTimestamp, Cid? cid = null, DateTimeOffset? expiresAt = null) =>
        CreateSignedLabel(source, uri, value, creationTimestamp, cid, expiresAt, negation: null);

    /// <summary>
    /// Creates and signs a version-one negation label.
    /// </summary>
    /// <param name="source">The issuer DID.</param>
    /// <param name="uri">The subject URI of the label to retract.</param>
    /// <param name="value">The value of the label to retract, at most 128 UTF-8 bytes.</param>
    /// <param name="creationTimestamp">The explicit creation time, chosen by the caller to supersede the earlier label.</param>
    /// <param name="cid">An optional subject version.</param>
    /// <param name="expiresAt">An optional expiry time.</param>
    /// <returns>A new signed label with <c>neg: true</c>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/>, <paramref name="uri"/>, or <paramref name="value"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="value"/> exceeds 128 UTF-8 bytes.</exception>
    /// <exception cref="InvalidDataException">The subject or timestamps are invalid.</exception>
    /// <exception cref="ObjectDisposedException">The key has been disposed.</exception>
    /// <exception cref="CryptographicException">The provider cannot sign the label.</exception>
    /// <remarks>
    /// <para>This method does not find, authorize, persist, or publish a previous label or its negation.</para>
    /// </remarks>
    public Label CreateNegation(Did source, string uri, string value, DateTimeOffset creationTimestamp, Cid? cid = null, DateTimeOffset? expiresAt = null) =>
        CreateSignedLabel(source, uri, value, creationTimestamp, cid, expiresAt, negation: true);

    /// <summary>
    /// Disposes the owned cryptographic key.
    /// </summary>
    public void Dispose()
    {
        lock (_sync)
        {
            if (!_disposed)
            {
                _key.Dispose();
                _disposed = true;
            }
        }
    }

    private Label CreateSignedLabel(Did source, string uri, string value, DateTimeOffset creationTimestamp, Cid? cid, DateTimeOffset? expiresAt, bool? negation) =>
        Sign(new Label(1, source, uri, cid, value, negation ?? false, creationTimestamp, signature: null)
        {
            Negation = negation,
            ExpiresAt = expiresAt
        });

    private static LabelSigningKey ImportKey(ReadOnlySpan<byte> source, ReadOnlySpan<char> password, bool encrypted)
    {
        if (encrypted)
        {
            ValidatePassword(password);
        }

        ECDsa? key = null;
        try
        {
            key = ECDsa.Create();
            int bytesRead;
            if (encrypted)
            {
                key.ImportEncryptedPkcs8PrivateKey(password, source, out bytesRead);
            }
            else
            {
                key.ImportPkcs8PrivateKey(source, out bytesRead);
            }

            if (bytesRead != source.Length)
            {
                throw new CryptographicException("The PKCS#8 input contains trailing data.");
            }

            ECCurve curve = key.ExportParameters(includePrivateParameters: false).Curve;
            LabelSigningCurve supportedCurve = curve.IsNamed ? curve.Oid.Value switch
            {
                SigningCurve.P256Oid => LabelSigningCurve.P256,
                SigningCurve.Secp256k1Oid => LabelSigningCurve.Secp256k1,
                _ => throw new CryptographicException("The private key uses an unsupported curve.")
            } : throw new CryptographicException("The private key must use a supported named curve.");

            LabelSigningKey result = new(key, supportedCurve);
            key = null;
            return result;
        }
        finally
        {
            key?.Dispose();
        }
    }

    private static LabelSigningKey ImportPem(ReadOnlySpan<char> pem, ReadOnlySpan<char> password, bool encrypted)
    {
        string label = encrypted ? "ENCRYPTED PRIVATE KEY" : "PRIVATE KEY";
        if (!PemEncoding.TryFind(pem, out PemFields fields) ||
            !pem[fields.Label].SequenceEqual(label) ||
            !pem[..fields.Location.Start.GetOffset(pem.Length)].Trim().IsEmpty ||
            !pem[fields.Location.End.GetOffset(pem.Length)..].Trim().IsEmpty)
        {
            throw new ArgumentException("Expected exactly one PKCS#8 PEM block of the requested kind.", nameof(pem));
        }

        byte[] encoded = new byte[fields.DecodedDataLength];
        try
        {
            if (!Convert.TryFromBase64Chars(pem[fields.Base64Data], encoded, out int bytesWritten) || bytesWritten != encoded.Length)
            {
                throw new ArgumentException("The PEM encoding is invalid.", nameof(pem));
            }

            return ImportKey(encoded, password, encrypted);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(encoded);
        }
    }

    private static void ValidatePassword(ReadOnlySpan<char> password)
    {
        if (password.IsEmpty)
        {
            throw new ArgumentException("A nonempty password is required.", nameof(password));
        }
    }

    private static string GetOid(LabelSigningCurve curve) => curve switch
    {
        LabelSigningCurve.P256 => SigningCurve.P256Oid,
        LabelSigningCurve.Secp256k1 => SigningCurve.Secp256k1Oid,
        _ => throw new ArgumentOutOfRangeException(nameof(curve))
    };
}
