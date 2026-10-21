// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Globalization;
using System.Text.Json;

using idunno.AtProto.Labels;
using idunno.AtProto.Labels.Models;

namespace idunno.AtProto.Serialization.Test;

[ExcludeFromCodeCoverage]
public class BossettLabelTests
{
    // Public identity snapshot captured alongside the labels, not resolved live by the tests.
    private const string DidDocumentJson = """
        {"id":"did:plc:i65enriuag7n5fgkopbqtkyk","verificationMethod":[
        {"id":"did:plc:i65enriuag7n5fgkopbqtkyk#atproto_label","type":"Multikey",
        "controller":"did:plc:i65enriuag7n5fgkopbqtkyk","publicKeyMultibase":"zQ3shRnNF9GBivUWEHr15yQbnYMkWeKVcQWCSJffbWHWNpGyZ"}]}
        """;

    [Fact]
    public void CapturedIssuedLabelsAndNegationsHaveValidSignatures()
    {
        DidDocument identity = JsonSerializer.Deserialize<DidDocument>(DidDocumentJson, AtProtoServer.AtProtoJsonSerializerOptions)!;
        QueryLabelsResponse response = JsonSerializer.Deserialize<QueryLabelsResponse>(Response, AtProtoServer.AtProtoJsonSerializerOptions)!;
        LabelSignatureVerifier.Verify(identity, response.Labels);
        foreach (Label label in response.Labels)
        {
            label.Verify(identity);
            (label with { }).Verify(identity);
            JsonSerializer.Deserialize<Label>(
                JsonSerializer.Serialize(label, AtProtoServer.AtProtoJsonSerializerOptions),
                AtProtoServer.AtProtoJsonSerializerOptions)!.Verify(identity);
        }
    }

    [Theory]
    [InlineData("value")]
    [InlineData("uri")]
    [InlineData("source")]
    [InlineData("cid")]
    [InlineData("negation")]
    [InlineData("creation")]
    [InlineData("sameCreation")]
    [InlineData("expiry")]
    [InlineData("signature")]
    [InlineData("unsigned")]
    [InlineData("version")]
    public void ModifiedCopiesDoNotVerifyTheOriginalData(string field)
    {
        DidDocument identity = JsonSerializer.Deserialize<DidDocument>(DidDocumentJson, AtProtoServer.AtProtoJsonSerializerOptions)!;
        Label original = JsonSerializer.Deserialize<QueryLabelsResponse>(Response, AtProtoServer.AtProtoJsonSerializerOptions)!.Labels[0];
        Label modified = field switch
        {
            "value" => original with { Value = "spam" },
            "uri" => original with { Uri = "did:plc:other" },
            "source" => original with { Source = new Did("did:plc:other") },
            "cid" => original with { Cid = new Cid("bafyreigh5fjr6an3qujvfbm6xmso6jyhido2dguixaeqp3jtcqft2gofba") },
            "negation" => original with { IsNegationLabel = true },
            "creation" => original with { CreationTimestamp = original.CreationTimestamp.AddSeconds(1) },
            "sameCreation" => original with { CreationTimestamp = original.CreationTimestamp },
            "expiry" => original with { ExpiresAt = original.CreationTimestamp.AddDays(1) },
            "signature" => original with { Signature = new Bytes(new byte[64]) },
            "unsigned" => original with { Signature = null },
            "version" => original with { Version = 2 },
            _ => throw new ArgumentOutOfRangeException(nameof(field))
        };

        Assert.Throws<InvalidDataException>(() => modified.Verify(identity));
        Assert.Throws<InvalidDataException>(() => LabelSignatureVerifier.Verify(identity, [original, modified]));
        original.Verify(identity);
    }

    [Theory]
    [InlineData("2024-10-17T07:44:57.3860Z")]
    [InlineData("2024-10-17T07:44:57.386+00:00")]
    public void EquivalentTimestampSpellingsDoNotHaveTheSameSignature(string timestamp)
    {
        DidDocument identity = JsonSerializer.Deserialize<DidDocument>(DidDocumentJson, AtProtoServer.AtProtoJsonSerializerOptions)!;
        Label original = JsonSerializer.Deserialize<QueryLabelsResponse>(Response, AtProtoServer.AtProtoJsonSerializerOptions)!.Labels[0];
        Label changed = JsonSerializer.Deserialize<QueryLabelsResponse>(
            Response.Replace("2024-10-17T07:44:57.386Z", timestamp, StringComparison.Ordinal),
            AtProtoServer.AtProtoJsonSerializerOptions)!.Labels[0];

        Assert.Equal(original, changed);
        Assert.Equal(original.GetHashCode(), changed.GetHashCode());
        Assert.Throws<InvalidDataException>(() => changed.Verify(identity));
    }

    [Fact]
    public void OmittedNegationIsNotTheSameSignedDataAsExplicitFalse()
    {
        DidDocument identity = JsonSerializer.Deserialize<DidDocument>(DidDocumentJson, AtProtoServer.AtProtoJsonSerializerOptions)!;
        Label original = JsonSerializer.Deserialize<QueryLabelsResponse>(Response, AtProtoServer.AtProtoJsonSerializerOptions)!.Labels[0];
        Label changed = original with { IsNegationLabel = false };

        Assert.Equal(original, changed);
        Assert.Equal(original.GetHashCode(), changed.GetHashCode());
        Assert.Throws<InvalidDataException>(() => changed.Verify(identity));
    }

    [Fact]
    public void UnknownPropertiesAreNotSigned()
    {
        DidDocument identity = JsonSerializer.Deserialize<DidDocument>(DidDocumentJson, AtProtoServer.AtProtoJsonSerializerOptions)!;
        QueryLabelsResponse response = JsonSerializer.Deserialize<QueryLabelsResponse>(
            Response.Replace("\"ver\":1", "\"ver\":1,\"$type\":\"com.atproto.label.defs#label\",\"future\":true", StringComparison.Ordinal),
            AtProtoServer.AtProtoJsonSerializerOptions)!;

        LabelSignatureVerifier.Verify(identity, response.Labels);
    }

    [Fact]
    public void CollectionVerificationRejectsMissingArgumentsAndNullEntries()
    {
        DidDocument identity = JsonSerializer.Deserialize<DidDocument>(DidDocumentJson, AtProtoServer.AtProtoJsonSerializerOptions)!;
        Assert.Throws<ArgumentNullException>(() => LabelSignatureVerifier.Verify(null!, []));
        Assert.Throws<ArgumentNullException>(() => LabelSignatureVerifier.Verify(identity, null!));
        Assert.Throws<InvalidDataException>(() => LabelSignatureVerifier.Verify(identity, [null!]));
        LabelSignatureVerifier.Verify(identity, []);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void VerificationSurvivesBothSerializerPaths(bool sourceGenerated)
    {
        JsonSerializerOptions options = sourceGenerated ? AtProtoServer.AtProtoJsonSerializerOptions : new(JsonSerializerDefaults.Web);
        DidDocument identity = JsonSerializer.Deserialize<DidDocument>(DidDocumentJson, AtProtoServer.AtProtoJsonSerializerOptions)!;
        using JsonDocument response = JsonDocument.Parse(Response);
        Label label = JsonSerializer.Deserialize<Label>(response.RootElement.GetProperty("labels")[0], options)!;

        label.Verify(identity);
        JsonSerializer.Deserialize<Label>(JsonSerializer.Serialize(label, options), options)!.Verify(identity);
    }

    [Theory]
    [InlineData("missing creation")]
    [InlineData("null creation")]
    [InlineData("invalid creation")]
    [InlineData("missing source")]
    [InlineData("null value")]
    [InlineData("long value")]
    [InlineData("duplicate value")]
    [InlineData("invalid cid")]
    [InlineData("invalid negation")]
    [InlineData("null negation")]
    [InlineData("invalid signature")]
    public void MalformedLabelsAreRejectedDuringDeserialization(string failure)
    {
        using JsonDocument response = JsonDocument.Parse(Response);
        string original = response.RootElement.GetProperty("labels")[0].GetRawText();
        string malformed = failure switch
        {
            "missing creation" => original.Replace("\"cts\":\"2024-10-17T07:44:57.386Z\",", string.Empty, StringComparison.Ordinal),
            "null creation" => original.Replace("\"2024-10-17T07:44:57.386Z\"", "null", StringComparison.Ordinal),
            "invalid creation" => original.Replace("2024-10-17T07:44:57.386Z", "not a timestamp", StringComparison.Ordinal),
            "missing source" => original.Replace("\"src\":\"did:plc:i65enriuag7n5fgkopbqtkyk\",", string.Empty, StringComparison.Ordinal),
            "null value" => original.Replace("\"newaccount\"", "null", StringComparison.Ordinal),
            "long value" => original.Replace("newaccount", new string('a', 129), StringComparison.Ordinal),
            "duplicate value" => original.Replace("\"val\":\"newaccount\"", "\"val\":\"newaccount\",\"val\":\"spam\"", StringComparison.Ordinal),
            "invalid cid" => original.Replace("\"ver\":1", "\"ver\":1,\"cid\":\"not a cid\"", StringComparison.Ordinal),
            "invalid negation" => original.Replace("\"ver\":1", "\"ver\":1,\"neg\":\"false\"", StringComparison.Ordinal),
            "null negation" => original.Replace("\"ver\":1", "\"ver\":1,\"neg\":null", StringComparison.Ordinal),
            "invalid signature" => original.Replace("\"$bytes\":\"", "\"$bytes\":\"!", StringComparison.Ordinal),
            _ => throw new ArgumentOutOfRangeException(nameof(failure))
        };

        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<Label>(malformed, AtProtoServer.AtProtoJsonSerializerOptions));
    }

    // Captured from https://ozone.bossett.social/xrpc/com.atproto.label.queryLabels?uriPatterns=*&limit=3
    // on 2026-10-20 after resolving profile-labels.bossett.social through its DID document.
    private const string Response = """
        {"cursor":"17714415","labels":[
        {"ver":1,"src":"did:plc:i65enriuag7n5fgkopbqtkyk","uri":"did:plc:3x3k2roxac265fz2j7thyrer","val":"newaccount","cts":"2024-10-17T07:44:57.386Z","sig":{"$bytes":"FPT9hLa4s5y/WiBI7tVSnRpcAArr7+AKjW8wqu/IfOZG04m+gw3WZMQtd28u2B5lU/EbWDWGjuGlWuQpbua+iw"}},
        {"ver":1,"src":"did:plc:i65enriuag7n5fgkopbqtkyk","uri":"did:plc:k63xzuhk5fkkuq26ywz6hqio","val":"newaccount","neg":true,"cts":"2025-04-11T13:41:56.234Z","sig":{"$bytes":"ohhQRnuSLVMAq4MrnFM4CR904pdgcan5pCtwAvRQfWshXkw2wwqr+BpBnSAvyRgsI9jOH/15ByhpNAlQW4egQA"}},
        {"ver":1,"src":"did:plc:i65enriuag7n5fgkopbqtkyk","uri":"did:plc:tg7hzpb2xxoojkvlpxxxaxvq","val":"newaccount","neg":true,"cts":"2025-08-04T02:01:56.521Z","sig":{"$bytes":"/Ln/p9O9jhbimKvgvNWzRBkaLR3fovxkZg4qb60sjosP37c1hdrXuRxVNPeV49incFiOBKy3DsHrtSB6sHKDUA"}}]}
        """;

    [Theory]
    [InlineData(0, "did:plc:3x3k2roxac265fz2j7thyrer", false, "2024-10-17T07:44:57.386Z",
        "FPT9hLa4s5y/WiBI7tVSnRpcAArr7+AKjW8wqu/IfOZG04m+gw3WZMQtd28u2B5lU/EbWDWGjuGlWuQpbua+iw")]
    [InlineData(1, "did:plc:k63xzuhk5fkkuq26ywz6hqio", true, "2025-04-11T13:41:56.234Z",
        "ohhQRnuSLVMAq4MrnFM4CR904pdgcan5pCtwAvRQfWshXkw2wwqr+BpBnSAvyRgsI9jOH/15ByhpNAlQW4egQA")]
    [InlineData(2, "did:plc:tg7hzpb2xxoojkvlpxxxaxvq", true, "2025-08-04T02:01:56.521Z",
        "/Ln/p9O9jhbimKvgvNWzRBkaLR3fovxkZg4qb60sjosP37c1hdrXuRxVNPeV49incFiOBKy3DsHrtSB6sHKDUA")]
    public void CapturedLabelsDeserializeWithSourceGeneratedOptions(
        int index, string subject, bool negation, string timestamp, string signature)
    {
        QueryLabelsResponse response = JsonSerializer.Deserialize<QueryLabelsResponse>(Response, AtProtoServer.AtProtoJsonSerializerOptions)!;

        Assert.Equal("17714415", response.Cursor);
        Assert.Equal(3, response.Labels.Count);
        Label label = response.Labels[index];
        Assert.Equal(1, label.Version);
        Assert.Equal(new Did("did:plc:i65enriuag7n5fgkopbqtkyk"), label.Source);
        Assert.Equal(subject, label.Uri);
        Assert.Equal("newaccount", label.Value);
        Assert.Equal(negation, label.IsNegationLabel);
        Assert.Equal(DateTimeOffset.Parse(timestamp, CultureInfo.InvariantCulture), label.CreationTimestamp);
        Assert.Null(label.Cid);
        Assert.Null(label.ExpiresAt);
        Assert.NotNull(label.Signature);
        byte[] expectedSignature = Convert.FromBase64String(signature + "==");
        Assert.Equal(64, label.Signature.ToBytes().Length);
        Assert.Equal(expectedSignature, label.Signature.ToBytes());

        string serialized = JsonSerializer.Serialize(label, AtProtoServer.AtProtoJsonSerializerOptions);
        using JsonDocument document = JsonDocument.Parse(serialized);
        string encodedSignature = document.RootElement.GetProperty("sig").GetProperty("$bytes").GetString()!;
        Assert.Equal(expectedSignature, Convert.FromBase64String(encodedSignature.PadRight(88, '=')));
        Assert.Equal(label, JsonSerializer.Deserialize<Label>(serialized, AtProtoServer.AtProtoJsonSerializerOptions));
    }
}
