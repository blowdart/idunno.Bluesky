// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Numerics;
using System.Security.Cryptography;
using System.Text.Json;

using idunno.AtProto.Labels;
using idunno.AtProto.Repo;

namespace idunno.AtProto.Test;

[ExcludeFromCodeCoverage]
public class LabelSigningTests
{
    private const string LabelJson = """
        {"ver":1,"src":"did:plc:issuer","uri":"did:plc:subject","val":"spam","cts":"2026-01-02T03:04:05Z"}
        """;

    // Fixed schema-order fixture, specified independently of the production encoder.
    private const string PayloadHex = "A56363747374323032362D30312D30325430333A30343A30355A637372636E6469643A706C633A697373756572637572696F6469643A706C633A7375626A6563746376616C647370616D6376657201";

    private static Label ReadLabel() => JsonSerializer.Deserialize<Label>(LabelJson, AtProtoServer.AtProtoJsonSerializerOptions)!;

    [Theory]
    [InlineData("z0")]
    [InlineData("z!")]
    [InlineData("z")]
    [InlineData("mAAAA")]
    public void PublicKeyValidationRejectsMalformedMultikeys(string publicKey)
    {
        Assert.Throws<ArgumentException>(() => LabelSignatureVerifier.ValidatePublicKey(publicKey));
    }

    [Theory]
    [InlineData(LabelSigningCurve.P256)]
    [InlineData(LabelSigningCurve.Secp256k1)]
    public void PublicKeyValidationAndExplicitVerificationRequireTheExpectedKey(LabelSigningCurve curve)
    {
        using LabelSigningKey key = LabelSigningKey.Generate(curve);
        using LabelSigningKey other = LabelSigningKey.Generate(curve);
        Label label = key.Sign(ReadLabel());
        LabelSignatureVerifier.ValidatePublicKey(key.ExportPublicKeyMultibase());
        LabelSignatureVerifier.Verify(label.Source, key.ExportPublicKeyMultibase(), [label]);
        Assert.Throws<InvalidDataException>(() =>
            LabelSignatureVerifier.Verify(label.Source, other.ExportPublicKeyMultibase(), [label]));
        Assert.Throws<InvalidDataException>(() =>
            LabelSignatureVerifier.Verify(new Did("did:plc:other"), key.ExportPublicKeyMultibase(), [label]));
        byte[] invalidPoint = [0x80, 0x24, 0x02, .. Enumerable.Repeat((byte)0xff, 32)];
        string invalidKey = "z" + SimpleBase.Base58.Bitcoin.Encode(invalidPoint);
        Assert.Throws<ArgumentException>(() => LabelSignatureVerifier.ValidatePublicKey(invalidKey));
    }

    [Theory]
    [InlineData("", "A5")]
    [InlineData(",\"neg\":false", "A6")]
    [InlineData(",\"neg\":true", "A6")]
    public void OptionalNegationHasFrozenEncoding(string field, string mapHeader)
    {
        Label label = JsonSerializer.Deserialize<Label>(LabelJson.Replace("\"ver\":1", "\"ver\":1" + field, StringComparison.Ordinal),
            AtProtoServer.AtProtoJsonSerializerOptions)!;
        string hex = mapHeader + PayloadHex[2..];
        if (field.Length != 0)
        {
            hex = hex.Replace("63737263", field.EndsWith("false", StringComparison.Ordinal) ? "636E6567F463737263" : "636E6567F563737263", StringComparison.Ordinal);
        }

        Assert.Equal(Convert.FromHexString(hex), label.GetCanonicalUnsignedBytes());
    }

    [Theory]
    [InlineData(LabelSigningCurve.P256)]
    [InlineData(LabelSigningCurve.Secp256k1)]
    public void SignedCopiesMatchFrozenPayloadAndVerifyIndependently(LabelSigningCurve curve)
    {
        using LabelSigningKey key = LabelSigningKey.Generate(curve);
        Label original = ReadLabel();
        byte[] expected = Convert.FromHexString(PayloadHex);
        Assert.Equal(expected, original.GetCanonicalUnsignedBytes());
        Label signed = key.Sign(original);
        Assert.NotSame(original, signed);
        Assert.Null(original.Signature);
        Assert.Equal(expected, signed.GetCanonicalUnsignedBytes());
        Assert.Equal(expected, (signed with { }).GetCanonicalUnsignedBytes());

        byte[] privateKey = key.ExportPkcs8PrivateKey();
        try
        {
            using ECDsa independent = ECDsa.Create();
            independent.ImportPkcs8PrivateKey(privateKey, out _);
            byte[] signature = signed.Signature!.ToBytes();
            Assert.Equal(64, signature.Length);
            Assert.True(independent.VerifyData(expected, signature, HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation));
            BigInteger order = SigningCurve.GetOrder(independent.ExportParameters(false).Curve.Oid.Value!);
            BigInteger s = new(signature.AsSpan(32), isUnsigned: true, isBigEndian: true);
            Assert.InRange(s, BigInteger.One, order / 2);
            expected[^1] ^= 1;
            Assert.False(independent.VerifyData(expected, signature, HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation));
        }
        finally
        {
            CryptographicOperations.ZeroMemory(privateKey);
        }

        DidDocument document = Document(key);
        signed.Verify(document);
        using LabelSigningKey replacement = LabelSigningKey.Generate(curve);
        Label resigned = replacement.Sign(signed);
        Assert.Equal(signed.GetCanonicalUnsignedBytes(), resigned.GetCanonicalUnsignedBytes());
        resigned.Verify(Document(replacement));
        Assert.Throws<InvalidDataException>(() => resigned.Verify(document));
        Assert.Throws<InvalidDataException>(() => original.Verify(document));
    }

    [Theory]
    [InlineData(LabelSigningCurve.P256, false)]
    [InlineData(LabelSigningCurve.P256, true)]
    [InlineData(LabelSigningCurve.Secp256k1, false)]
    [InlineData(LabelSigningCurve.Secp256k1, true)]
    public void FactoriesSetOnlyExplicitVersionAndNegationDefaults(LabelSigningCurve curve, bool negation)
    {
        using LabelSigningKey key = LabelSigningKey.Generate(curve);
        DateTimeOffset instant = new(2026, 1, 2, 3, 4, 5, TimeSpan.FromHours(1));
        Label label = negation
            ? key.CreateNegation(new Did("did:plc:issuer"), "did:plc:subject", "spam", instant)
            : key.CreateLabel(new Did("did:plc:issuer"), "did:plc:subject", "spam", instant);
        Assert.Equal(1, label.Version);
        Assert.Equal(negation ? true : (bool?)null, label.Negation);
        Assert.Equal(instant, label.CreationTimestamp);
        Assert.Null(label.Cid);
        Assert.Null(label.ExpiresAt);
        label.Verify(Document(key));
        using JsonDocument json = JsonDocument.Parse(JsonSerializer.Serialize(label, AtProtoServer.AtProtoJsonSerializerOptions));
        Assert.Equal(negation, json.RootElement.TryGetProperty("neg", out _));
        JsonSerializer.Deserialize<Label>(json.RootElement, AtProtoServer.AtProtoJsonSerializerOptions)!.Verify(Document(key));
    }

    [Theory]
    [InlineData(LabelSigningCurve.P256)]
    [InlineData(LabelSigningCurve.Secp256k1)]
    public void RecordFactoriesIncludeCidAndExpiryAndHonorUtf8Boundary(LabelSigningCurve curve)
    {
        using LabelSigningKey key = LabelSigningKey.Generate(curve);
        DateTimeOffset instant = new(2026, 1, 2, 3, 4, 5, TimeSpan.Zero);
        Cid cid = new("bafyreigh5fjr6an3qujvfbm6xmso6jyhido2dguixaeqp3jtcqft2gofba");
        string value = new('\u00e9', 64);
        Label label = key.CreateLabel(new Did("did:plc:issuer"),
            "at://did:plc:subject/app.bsky.feed.post/3lngovum7vm2k", value, instant, cid, instant.AddDays(1));
        Assert.Equal(cid, label.Cid);
        Assert.Equal(instant.AddDays(1), label.ExpiresAt);
        label.Verify(Document(key));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            key.CreateLabel(new Did("did:plc:issuer"), "did:plc:subject", value + "a", instant));
        Assert.Throws<InvalidDataException>(() => key.Sign(label with { Value = value + "a" }));
    }

    [Theory]
    [InlineData(LabelSigningCurve.P256)]
    [InlineData(LabelSigningCurve.Secp256k1)]
    public void AllExplicitKeyFormatsRetainIdentity(LabelSigningCurve curve)
    {
        using LabelSigningKey key = LabelSigningKey.Generate(curve);
        PbeParameters encryption = new(PbeEncryptionAlgorithm.Aes256Cbc, HashAlgorithmName.SHA256, 100_000);
        byte[] plain = key.ExportPkcs8PrivateKey();
        byte[] encrypted = key.ExportEncryptedPkcs8PrivateKey("test-only-password", encryption);
        try
        {
            using LabelSigningKey binary = LabelSigningKey.ImportPkcs8PrivateKey(plain);
            using LabelSigningKey pem = LabelSigningKey.ImportFromPem(key.ExportPkcs8PrivateKeyPem());
            using LabelSigningKey protectedBinary = LabelSigningKey.ImportEncryptedPkcs8PrivateKey(encrypted, "test-only-password");
            using LabelSigningKey protectedPem = LabelSigningKey.ImportFromEncryptedPem(
                key.ExportEncryptedPkcs8PrivateKeyPem("test-only-password", encryption), "test-only-password");
            foreach (LabelSigningKey imported in new[] { binary, pem, protectedBinary, protectedPem })
            {
                Assert.Equal(curve, imported.Curve);
                Assert.Equal(key.ExportPublicKeyMultibase(), imported.ExportPublicKeyMultibase());
                imported.Sign(ReadLabel()).Verify(Document(key));
            }

            Assert.Throws<CryptographicException>(() => LabelSigningKey.ImportEncryptedPkcs8PrivateKey(encrypted, "wrong-password"));
            Assert.Throws<CryptographicException>(() => LabelSigningKey.ImportPkcs8PrivateKey([.. plain, 0]));
            Assert.Throws<CryptographicException>(() => LabelSigningKey.ImportPkcs8PrivateKey(plain.AsSpan(0, plain.Length - 1)));
            Assert.Throws<ArgumentException>(() => LabelSigningKey.ImportFromPem(key.ExportPkcs8PrivateKeyPem() + key.ExportPkcs8PrivateKeyPem()));
            Assert.Throws<ArgumentException>(() => LabelSigningKey.ImportFromPem("not a key"));
            Assert.Throws<ArgumentException>(() => LabelSigningKey.ImportFromPem(
                key.ExportPkcs8PrivateKeyPem().Replace("PRIVATE KEY", "EC PRIVATE KEY", StringComparison.Ordinal)));
            Assert.Throws<ArgumentException>(() => LabelSigningKey.ImportFromPem(
                key.ExportEncryptedPkcs8PrivateKeyPem("test-only-password", encryption)));
            Assert.Throws<CryptographicException>(() => LabelSigningKey.ImportFromEncryptedPem(
                key.ExportEncryptedPkcs8PrivateKeyPem("test-only-password", encryption), "wrong-password"));
            Assert.Throws<ArgumentException>(() => key.ExportEncryptedPkcs8PrivateKey("", encryption));
            Assert.Throws<ArgumentException>(() => LabelSigningKey.ImportEncryptedPkcs8PrivateKey(encrypted, ""));
            Assert.Throws<ArgumentNullException>(() => key.ExportEncryptedPkcs8PrivateKey("password", null!));
        }
        finally
        {
            CryptographicOperations.ZeroMemory(plain);
            CryptographicOperations.ZeroMemory(encrypted);
        }
    }

    [Theory]
    [InlineData("1.2.840.10045.3.1.7")]
    [InlineData("1.3.132.0.10")]
    public void NormalizationHandlesBoundariesAndRejectsInvalidScalars(string oid)
    {
        BigInteger order = SigningCurve.GetOrder(oid);
        foreach (BigInteger s in new[] { BigInteger.One, order / 2, (order / 2) + 1, order - 1 })
        {
            byte[] signature = new byte[64];
            signature[31] = 1;
            byte[] scalar = s.ToByteArray(isUnsigned: true, isBigEndian: true);
            scalar.CopyTo(signature, 64 - scalar.Length);
            SigningCurve.NormalizeSignature(signature, oid);
            Assert.Equal(BigInteger.Min(s, order - s), new BigInteger(signature.AsSpan(32), isUnsigned: true, isBigEndian: true));
        }

        Assert.Throws<CryptographicException>(() => SigningCurve.NormalizeSignature(new byte[64], oid));
        Assert.Throws<CryptographicException>(() => SigningCurve.NormalizeSignature(new byte[63], oid));
        byte[] invalid = new byte[64];
        invalid[31] = 1;
        order.ToByteArray(isUnsigned: true, isBigEndian: true).CopyTo(invalid, 32);
        Assert.Throws<CryptographicException>(() => SigningCurve.NormalizeSignature(invalid, oid));
    }

    [Theory]
    [InlineData("missing version")]
    [InlineData("future version")]
    [InlineData("null source")]
    [InlineData("null uri")]
    [InlineData("invalid uri")]
    [InlineData("null value")]
    [InlineData("long value")]
    public void SigningValidatesMutatedLabels(string failure)
    {
        using LabelSigningKey key = LabelSigningKey.Generate(LabelSigningCurve.P256);
        Label label = ReadLabel();
        Label invalid = failure switch
        {
            "missing version" => label with { Version = null },
            "future version" => label with { Version = 2 },
            "null source" => label with { Source = null! },
            "null uri" => label with { Uri = null! },
            "invalid uri" => label with { Uri = "not a uri" },
            "null value" => label with { Value = null! },
            "long value" => label with { Value = new string('a', 129) },
            _ => throw new ArgumentOutOfRangeException(nameof(failure))
        };
        Assert.Throws<InvalidDataException>(() => invalid.GetCanonicalUnsignedBytes());
        Assert.Throws<InvalidDataException>(() => key.Sign(invalid));
    }

    [Fact]
    public void UnsupportedAndPublicOnlyKeysCannotBeImported()
    {
        using ECDsa unsupported = ECDsa.Create(ECCurve.NamedCurves.nistP384);
        byte[] secret = unsupported.ExportPkcs8PrivateKey();
        try
        {
            Assert.Throws<CryptographicException>(() => LabelSigningKey.ImportPkcs8PrivateKey(secret));
            Assert.Throws<CryptographicException>(() => LabelSigningKey.ImportPkcs8PrivateKey(unsupported.ExportSubjectPublicKeyInfo()));
            Assert.Throws<ArgumentException>(() => LabelSigningKey.ImportFromPem(unsupported.ExportSubjectPublicKeyInfoPem()));
        }
        finally
        {
            CryptographicOperations.ZeroMemory(secret);
        }

        Assert.Throws<ArgumentOutOfRangeException>(() => LabelSigningKey.Generate((LabelSigningCurve)99));
        Assert.Throws<CryptographicException>(() => LabelSigningKey.ImportPkcs8PrivateKey([]));
    }

    [Fact]
    public void DisposalGuardsOperationsAndDefaultRepresentationsHaveNoSecrets()
    {
        LabelSigningKey key = LabelSigningKey.Generate(LabelSigningCurve.P256);
        try
        {
            Assert.Equal(typeof(LabelSigningKey).FullName, key.ToString());
            Assert.Single(typeof(LabelSigningKey).GetProperties());
            Assert.Throws<ArgumentNullException>(() => key.Sign(null!));
        }
        finally
        {
            key.Dispose();
        }

        key.Dispose();
        Assert.Throws<ObjectDisposedException>(() => key.Sign(ReadLabel()));
        Assert.Throws<ObjectDisposedException>(() => key.ExportPkcs8PrivateKey());
        Assert.Throws<ObjectDisposedException>(() => key.ExportPkcs8PrivateKeyPem());
        Assert.Throws<ObjectDisposedException>(() => key.ExportPublicKeyMultibase());
        Assert.Throws<ObjectDisposedException>(() => key.ExportEncryptedPkcs8PrivateKey("password", new(PbeEncryptionAlgorithm.Aes256Cbc, HashAlgorithmName.SHA256, 100_000)));
    }

    [Fact]
    public async Task ConcurrentSigningAndDisposalHaveExplicitOutcomes()
    {
        using LabelSigningKey key = LabelSigningKey.Generate(LabelSigningCurve.P256);
        DidDocument document = Document(key);
        TaskCompletionSource start = new(TaskCreationOptions.RunContinuationsAsynchronously);
        Task[] signers = [.. Enumerable.Range(0, 16).Select(_ => Task.Run(async () =>
        {
            await start.Task;
            try
            {
                key.Sign(ReadLabel()).Verify(document);
            }
            catch (ObjectDisposedException)
            {
                Assert.Throws<ObjectDisposedException>(() => key.Sign(ReadLabel()));
            }
        }, TestContext.Current.CancellationToken))];
        Task disposal = Task.Run(async () =>
        {
            await start.Task;
            key.Dispose();
        }, TestContext.Current.CancellationToken);
        start.SetResult();
        await Task.WhenAll([.. signers, disposal]).WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);
        Assert.Throws<ObjectDisposedException>(() => key.ExportPublicKeyMultibase());
    }

    private static DidDocument Document(LabelSigningKey key) => new(
        new Did("did:plc:issuer"), null, null,
        [new VerificationMethod("did:plc:issuer#atproto_label", "Multikey", new Did("did:plc:issuer"))
        {
            PublicKeyMultibase = key.ExportPublicKeyMultibase()
        }], null);
}
