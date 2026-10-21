// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json;

using idunno.AtProto.Labels;

namespace idunno.AtProto.Serialization.Test;

[ExcludeFromCodeCoverage]
public class LabelSigningSerializationTests
{
    // Test-only public keys and signatures generated with native Linux OpenSSL 3.0.13.
    // Payload: version 1, issuer did:plc:issuer, subject did:plc:subject, value spam,
    // cts 2026-01-02T03:04:05Z, and neg omitted or true. No private keys are retained.
    [Theory]
    [InlineData(LabelSigningCurve.P256, false, "zDnaeYTZkgZv6v7CWGQgS6AUnQiBxRbiGb9uUMY66jfwVM4k8", "cPp4S+0qtAD9go2ihfgq4VSSGhgzCNxb5aj961kmwJVRiYGVs4aRG8762vKaA56F6lHnx8yKJpVN+p+6a3iYPQ==")]
    [InlineData(LabelSigningCurve.P256, true, "zDnaeYTZkgZv6v7CWGQgS6AUnQiBxRbiGb9uUMY66jfwVM4k8", "kydF8a//kcUxMX5n1JDEKvzJch2TALKT5nM7Hwhu7mMXYVQIduYLdEvrOHtHkwJ1RwlIoHPdsK/6fkRH4WOL8g==")]
    [InlineData(LabelSigningCurve.Secp256k1, false, "zQ3shioJhwcP5kyFuo2iXZKPUNg9Zb1D8LMGXgMpRsRrEocL6", "ks6bF8Z3uKcbSiBkgaEGOwbptayDb0kCRisOmkyAyFZvdWFYx/aYWW0W8uqqkNCHi6otsb/BZTP4QKmTCv8rZg==")]
    [InlineData(LabelSigningCurve.Secp256k1, true, "zQ3shioJhwcP5kyFuo2iXZKPUNg9Zb1D8LMGXgMpRsRrEocL6", "yLM3cAH1t0nRipbTLWgKqI6tZ3tiPV8B8x0wUKmlDINfoRJiOoe7c6F8Vny7u/rvUME0QCfyKPX/ZU8l9Bjlhg==")]
    public void LinuxOpenSslSignaturesSurviveSerializationAndResigning(LabelSigningCurve curve, bool negation, string publicKey, string signature)
    {
        string negationField = negation ? ",\"neg\":true" : string.Empty;
        string json = """
            {"ver":1,"src":"did:plc:issuer","uri":"did:plc:subject","val":"spam","cts":"2026-01-02T03:04:05Z"
            """ + negationField + ",\"sig\":{\"$bytes\":\"" + signature + "\"}}";
        DidDocument originalDocument = Document(publicKey);
        foreach (JsonSerializerOptions options in new[] { AtProtoServer.AtProtoJsonSerializerOptions, new(JsonSerializerDefaults.Web) })
        {
            Label label = JsonSerializer.Deserialize<Label>(json, options)!;
            label.Verify(originalDocument);
            Label roundTripped = JsonSerializer.Deserialize<Label>(JsonSerializer.Serialize(label, options), options)!;
            Assert.Equal(label.GetCanonicalUnsignedBytes(), roundTripped.GetCanonicalUnsignedBytes());
            roundTripped.Verify(originalDocument);
            Assert.Throws<InvalidDataException>(() => (label with { Value = "not-spam" }).Verify(originalDocument));
            using LabelSigningKey key = LabelSigningKey.Generate(curve);
            Label resigned = key.Sign(roundTripped);
            Assert.Equal(label.GetCanonicalUnsignedBytes(), resigned.GetCanonicalUnsignedBytes());
            resigned.Verify(Document(key.ExportPublicKeyMultibase()));
            label.Verify(originalDocument);
        }
    }

    private static DidDocument Document(string publicKey) => new(
        new Did("did:plc:issuer"), null, null,
        [new VerificationMethod("did:plc:issuer#atproto_label", "Multikey", new Did("did:plc:issuer"))
        {
            PublicKeyMultibase = publicKey
        }], null);
}
