// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Numerics;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;

using idunno.AtProto.Firehose;
using idunno.AtProto.Labels;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;

using static idunno.AtProto.Integration.Test.FirehoseTestData;

namespace idunno.AtProto.Integration.Test;

[ExcludeFromCodeCoverage]
public class LabelSignatureVerificationTests
{
    public static TheoryData<string, string, bool?, bool> SignedLabelShapes
    {
        get
        {
            TheoryData<string, string, bool?, bool> data = [];
            foreach (string curve in new[] { "1.2.840.10045.3.1.7", "1.3.132.0.10" })
            {
                foreach (string timestamp in new[]
                {
                    "2026-01-02T03:04:05Z",
                    "2026-01-02T03:04:05.1200Z",
                    "2026-01-02T04:04:05.12+01:00",
                    "2026-01-02T03:04:05.12345678901234567Z",
                    "2026-01-02T04:04:05.12345678901234567+01:00"
                })
                {
                    foreach (bool? negation in new bool?[] { null, false, true })
                    {
                        data.Add(curve, timestamp, negation, false);
                        data.Add(curve, timestamp, negation, true);
                    }
                }
            }

            return data;
        }
    }

    [Theory]
    [MemberData(nameof(SignedLabelShapes))]
    public void SigningPreservesDecodedSignedRepresentations(string curve, string timestamp, bool? negation, bool optionalFields)
    {
        using ECDsa externalKey = ECDsa.Create(ECCurve.CreateFromValue(curve));
        byte[] privateKey = externalKey.ExportPkcs8PrivateKey();
        try
        {
            using LabelSigningKey key = LabelSigningKey.ImportPkcs8PrivateKey(privateKey);
            Dictionary<string, object?> data = LabelPayload();
            data.Remove("sig");
            data["cts"] = timestamp;
            data.Remove("neg");
            if (negation.HasValue)
            {
                data["neg"] = negation.Value;
            }

            if (!optionalFields)
            {
                data.Remove("cid");
                data.Remove("exp");
            }
            else
            {
                data["cid"] = $"z{SimpleBase.Base58.Bitcoin.Encode(CidOf(RecordBytes()).ToBytes())}";
                data["exp"] = "2027-01-01T01:00:00.12345678901234567+01:00";
            }

            byte[] expected = Encode(data);
            data["$type"] = "com.atproto.label.defs#label";
            data["future"] = true;
            Label json = ReadJson(data);
            Label cbor = LabelEventDecoder.DecodeLabel(FirehoseCbor.ReadFields(Encode(data), LabelEventDecoder.LabelFields));
            foreach (Label original in new[] { json, cbor })
            {
                Label signed = key.Sign(original);
                Assert.Null(original.Signature);
                Assert.Equal(expected, signed.GetCanonicalUnsignedBytes());
                Assert.True(externalKey.VerifyData(expected, signed.Signature!.ToBytes(), HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation));
                signed.Verify(DidDocumentFor(OtherDid, "atproto_label", externalKey));
                Label roundTripped = JsonSerializer.Deserialize<Label>(
                    JsonSerializer.Serialize(signed, AtProtoServer.AtProtoJsonSerializerOptions), AtProtoServer.AtProtoJsonSerializerOptions)!;
                Assert.Equal(expected, roundTripped.GetCanonicalUnsignedBytes());
                roundTripped.Verify(DidDocumentFor(OtherDid, "atproto_label", externalKey));
            }
        }
        finally
        {
            CryptographicOperations.ZeroMemory(privateKey);
        }
    }

    [Theory]
    [MemberData(nameof(SignedLabelShapes))]
    public void JsonAndCborLabelsUseTheSameSignedRepresentation(string curve, string timestamp, bool? negation, bool optionalFields)
    {
        using ECDsa key = ECDsa.Create(ECCurve.CreateFromValue(curve));
        DidDocument document = DidDocumentFor(OtherDid, "atproto_label", key);
        Dictionary<string, object?> data = LabelPayload();
        data.Remove("sig");
        data["cts"] = timestamp;
        data.Remove("neg");
        if (negation.HasValue)
        {
            data["neg"] = negation.Value;
        }

        if (!optionalFields)
        {
            data.Remove("cid");
            data.Remove("exp");
        }
        else
        {
            data["cid"] = $"z{SimpleBase.Base58.Bitcoin.Encode(CidOf(RecordBytes()).ToBytes())}";
            data["exp"] = "2027-01-01T01:00:00.12345678901234567+01:00";
        }

        byte[] unsigned = Encode(data);
        data["sig"] = SignLabel(key, unsigned);
        // These properties are not part of the signature.
        data["$type"] = "com.atproto.label.defs#label";
        data["future"] = true;

        Label jsonLabel = ReadJson(data);
        Label cborLabel = LabelEventDecoder.DecodeLabel(FirehoseCbor.ReadFields(Encode(data), LabelEventDecoder.LabelFields));
        TimeSpan expectedOffset = timestamp.EndsWith("+01:00", StringComparison.Ordinal) ? TimeSpan.FromHours(1) : TimeSpan.Zero;
        Assert.Equal(expectedOffset, jsonLabel.CreationTimestamp.Offset);
        Assert.Equal(expectedOffset, cborLabel.CreationTimestamp.Offset);
        if (optionalFields)
        {
            Assert.Equal(TimeSpan.FromHours(1), jsonLabel.ExpiresAt!.Value.Offset);
            Assert.Equal(TimeSpan.FromHours(1), cborLabel.ExpiresAt!.Value.Offset);
            Assert.Equal(1234567, jsonLabel.ExpiresAt.Value.Ticks % TimeSpan.TicksPerSecond);
            Assert.Equal(1234567, cborLabel.ExpiresAt.Value.Ticks % TimeSpan.TicksPerSecond);
        }

        Assert.Equal(unsigned, LabelSignatureVerifier.GetUnsignedLabel(jsonLabel));
        Assert.Equal(unsigned, LabelSignatureVerifier.GetUnsignedLabel(cborLabel));
        LabelSignatureVerifier.Verify(document, [jsonLabel, cborLabel]);
        (jsonLabel with { }).Verify(document);
        if (optionalFields)
        {
            Assert.Throws<InvalidDataException>(() => (jsonLabel with { Cid = jsonLabel.Cid }).Verify(document));
            Assert.Throws<InvalidDataException>(() => (jsonLabel with { ExpiresAt = jsonLabel.ExpiresAt }).Verify(document));
        }

        JsonSerializer.Deserialize<Label>(
            JsonSerializer.Serialize(jsonLabel, AtProtoServer.AtProtoJsonSerializerOptions),
            AtProtoServer.AtProtoJsonSerializerOptions)!.Verify(document);
    }

    [Theory]
    [InlineData("cid")]
    [InlineData("exp")]
    [InlineData("neg")]
    public void AddingExplicitNullCannotVerifyAsAnOmittedSignedField(string field)
    {
        using ECDsa key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        DidDocument document = DidDocumentFor(OtherDid, "atproto_label", key);
        Dictionary<string, object?> data = LabelPayload();
        data.Remove("sig");
        data.Remove(field);
        data["sig"] = SignLabel(key, Encode(data));
        ReadJson(data).Verify(document);
        LabelEventDecoder.DecodeLabel(FirehoseCbor.ReadFields(Encode(data), LabelEventDecoder.LabelFields)).Verify(document);

        JsonObject json = ToJson(data);
        json[field] = null;
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<Label>(json.ToJsonString(), AtProtoServer.AtProtoJsonSerializerOptions));
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<Label>(json.ToJsonString(), new JsonSerializerOptions(JsonSerializerDefaults.Web)));
        data[field] = null;
        Assert.Throws<InvalidDataException>(() => LabelEventDecoder.DecodeLabel(FirehoseCbor.ReadFields(Encode(data), LabelEventDecoder.LabelFields)));
    }

    [Theory]
    [InlineData("1.2.840.10045.3.1.7")]
    [InlineData("1.3.132.0.10")]
    public void HighSAndNonCompactSignaturesAreRejected(string curve)
    {
        using ECDsa key = ECDsa.Create(ECCurve.CreateFromValue(curve));
        DidDocument document = DidDocumentFor(OtherDid, "atproto_label", key);
        Label label = ReadJson(LabelPayload(key: key));
        label.Verify(document);
        byte[] signature = label.Signature!.ToBytes();
        BigInteger s = new(signature.AsSpan(32), isUnsigned: true, isBigEndian: true);
        byte[] highS = (CurveOrder(key) - s).ToByteArray(isUnsigned: true, isBigEndian: true);
        highS.CopyTo(signature, 64 - highS.Length);

        Assert.Throws<InvalidDataException>(() => (label with { Signature = new Bytes(signature) }).Verify(document));
        Assert.Throws<InvalidDataException>(() => (label with { Signature = new Bytes(new byte[63]) }).Verify(document));
        Assert.Throws<InvalidDataException>(() => (label with { Signature = new Bytes(new byte[65]) }).Verify(document));
        Assert.Throws<InvalidDataException>(() => (label with { Signature = new Bytes(new byte[64]) }).Verify(document));
    }

    [Theory]
    [InlineData("wrong issuer")]
    [InlineData("repository key")]
    [InlineData("wrong key")]
    public void VerificationRequiresTheIssuersLabelKey(string failure)
    {
        using ECDsa key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        using ECDsa otherKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        Label label = ReadJson(LabelPayload(key: key));
        DidDocument document = failure switch
        {
            "wrong issuer" => DidDocumentFor(TestDid, "atproto_label", key),
            "repository key" => DidDocumentFor(OtherDid, "atproto", key),
            "wrong key" => DidDocumentFor(OtherDid, "atproto_label", otherKey),
            _ => throw new ArgumentOutOfRangeException(nameof(failure))
        };

        Assert.Throws<InvalidDataException>(() => label.Verify(document));
        Assert.Throws<InvalidDataException>(() => LabelSignatureVerifier.Verify(document, [label]));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task QueriedLabelsCanResolveTheirIssuerAndVerify(bool resolutionFails)
    {
        using ECDsa key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        DidDocument document = DidDocumentFor(OtherDid, "atproto_label", key);
        JsonObject labelJson = ToJson(LabelPayload(key: key));
        int resolutions = 0;
        using TestServer server = TestServerBuilder.CreateServer(TestServerBuilder.DefaultUri, async context =>
        {
            context.Response.ContentType = "application/json";
            if (context.Request.Path == "/xrpc/com.atproto.label.queryLabels")
            {
                await context.Response.WriteAsync(
                    new JsonObject { ["labels"] = new JsonArray(labelJson.DeepClone()) }.ToJsonString(),
                    TestContext.Current.CancellationToken);
                return;
            }

            Assert.Equal($"/{OtherDid}", context.Request.Path);
            resolutions++;
            if (resolutionFails)
            {
                context.Response.StatusCode = StatusCodes.Status404NotFound;
                await context.Response.WriteAsync("""{"error":"NotFound"}""", TestContext.Current.CancellationToken);
            }
            else
            {
                await context.Response.WriteAsync(
                    JsonSerializer.Serialize(document, AtProtoServer.AtProtoJsonSerializerOptions),
                    TestContext.Current.CancellationToken);
            }
        });
        using HttpClient client = server.CreateClient();
        AtProtoHttpResult<PagedReadOnlyCollection<Label>> result = await AtProtoServer.QueryLabels(
            ["*"], null, null, null, TestServerBuilder.DefaultUri, null, client,
            cancellationToken: TestContext.Current.CancellationToken);
        Assert.True(result.Succeeded);
        Label label = Assert.Single(result.Result);

        if (resolutionFails)
        {
            await Assert.ThrowsAsync<InvalidDataException>(() => label.VerifyAsync(client, TestContext.Current.CancellationToken));
        }
        else
        {
            await label.VerifyAsync(client, TestContext.Current.CancellationToken);
            label.Verify(document);
            LabelSignatureVerifier.Verify(document, result.Result);
        }

        Assert.Equal(1, resolutions);
        using CancellationTokenSource cancellation = new();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => label.VerifyAsync(client, cancellation.Token));
        Assert.Equal(1, resolutions);
    }

    private static Label ReadJson(Dictionary<string, object?> data) =>
        JsonSerializer.Deserialize<Label>(ToJson(data).ToJsonString(), AtProtoServer.AtProtoJsonSerializerOptions)!;

    private static JsonObject ToJson(Dictionary<string, object?> data)
    {
        JsonObject result = [];
        foreach ((string name, object? value) in data)
        {
            result[name] = value switch
            {
                string text => JsonValue.Create(text),
                long number => JsonValue.Create(number),
                bool boolean => JsonValue.Create(boolean),
                byte[] bytes => new JsonObject { ["$bytes"] = Convert.ToBase64String(bytes).TrimEnd('=') },
                _ => throw new ArgumentException("Unsupported test field.", nameof(data))
            };
        }

        return result;
    }
}
