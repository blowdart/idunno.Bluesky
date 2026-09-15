// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json;

namespace idunno.AtProto.Types.Test;

[ExcludeFromCodeCoverage]
public class ConverterContractTests
{
    private static readonly JsonSerializerOptions s_jsonSerializerOptions = new(JsonSerializerDefaults.Web);

    // Anything a server can put on the wire, whether it is well formed or not. A converter turning one of these into
    // anything other than a JsonException would escape past the callers which are written to catch deserialization
    // failures, so this is the invariant every converter has to hold.
    public static TheoryData<string> MalformedValues =>
    [
        "",
        " ",
        "\t",
        "x",
        "at://",
        "at://x",
        "AT://did:plc:identifier",
        "at://did:plc:identifier//",
        "at://did:plc:identifier/a",
        "at://did:plc:identifier/-a.b.c/rkey",
        "at://did:plc:identifier/a.b.c-/rkey",
        "at://did:plc:identifier/a..b/rkey",
        "at://did:plc:identifier/999.999.999/rkey",
        "at://did:plc:identifier/a.b.c/",
        "at://did:plc:identifier/a.b.c/.",
        "at://did:plc:identifier/a.b.c/..",
        "at://did:plc:identifier/a.b.c/rkey/extra",
        "at://did:plc:identifier/a.b.c/\u00e9",
        "did:",
        "did::",
        "did:plc:",
        "not-a-did",
        "-leading.hyphen",
        "trailing.hyphen-",
        ".",
        "..",
        "a/b",
        "a%b",
        "!!!",
        "\u00e9",
    ];

    [Theory]
    [MemberData(nameof(MalformedValues))]
    public void NoConverterLetsAnythingOtherThanAJsonExceptionEscapeForAMalformedValue(string value)
    {
        AssertOnlyJsonExceptionEscapes<AtUriSample>(value);
        AssertOnlyJsonExceptionEscapes<AtIdentifierSample>(value);
        AssertOnlyJsonExceptionEscapes<DidSample>(value);
        AssertOnlyJsonExceptionEscapes<HandleSample>(value);
        AssertOnlyJsonExceptionEscapes<NsidSample>(value);
        AssertOnlyJsonExceptionEscapes<RecordKeySample>(value);
        AssertOnlyJsonExceptionEscapes<TimestampIdentifierSample>(value);
    }

    [Fact]
    public void AnAtUriWhoseRecordKeyIsInvalidIsReportedAsAJsonException()
    {
        // AtUri reports a bad record key as an AtUriFormatException rather than a RecordKeyFormatException, so this
        // covers the converter still translating it after that change.
        Assert.Throws<JsonException>(
            () => JsonSerializer.Deserialize<AtUriSample>(
                "{\"value\":\"at://did:plc:identifier/test.idunno.lexiconType/.\"}",
                s_jsonSerializerOptions));
    }

    [Fact]
    public void AnAtUriWhoseCollectionIsInvalidIsReportedAsAJsonException()
    {
        Assert.Throws<JsonException>(
            () => JsonSerializer.Deserialize<AtUriSample>(
                "{\"value\":\"at://did:plc:identifier/-test.idunno.lexiconType/rkey\"}",
                s_jsonSerializerOptions));
    }

    [Fact]
    public void BytesAreReadWhenTheBytesPropertyNameArrivesEscaped()
    {
        // A sender is free to escape any character in a property name, so the name has to be compared against its
        // decoded form rather than the literal bytes on the wire.
        BytesSample? actual = JsonSerializer.Deserialize<BytesSample>(
            "{\"value\":{\"\\u0024bytes\":\"dHJ1ZQ==\"}}",
            s_jsonSerializerOptions);

        Assert.NotNull(actual);
        Assert.NotNull(actual.Value);
        Assert.Equal("dHJ1ZQ==", actual.Value.Base64EncodedValue);
    }

    [Theory]
    [InlineData("{\"value\":{\"bytes\":\"dHJ1ZQ==\"}}")]
    [InlineData("{\"value\":{\"$byte\":\"dHJ1ZQ==\"}}")]
    [InlineData("{\"value\":{\"$bytess\":\"dHJ1ZQ==\"}}")]
    [InlineData("{\"value\":{\"$BYTES\":\"dHJ1ZQ==\"}}")]
    public void BytesCarryingSomethingOtherThanTheBytesPropertyNameIsRejected(string json)
    {
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<BytesSample>(json, s_jsonSerializerOptions));
    }

    [Fact]
    public void BytesRoundTripThroughTheirOwnOutput()
    {
        const string json = "{\"value\":{\"$bytes\":\"dHJ1ZQ==\"}}";

        BytesSample? deserialized = JsonSerializer.Deserialize<BytesSample>(json, s_jsonSerializerOptions);

        Assert.Equal(json, JsonSerializer.Serialize(deserialized, s_jsonSerializerOptions));
    }

    [Theory]
    [InlineData("at://did:plc:z72i7hdynmk6r22z27h6tvur/app.bsky.feed.post/3jui7kd54zh2y")]
    [InlineData("at://did:plc:z72i7hdynmk6r22z27h6tvur/app.bsky.feed.post")]
    [InlineData("at://did:plc:z72i7hdynmk6r22z27h6tvur")]
    [InlineData("at://user.bsky.social/app.bsky.feed.post/self")]
    public void AnAtUriRoundTripsThroughItsOwnOutput(string value) => AssertRoundTrips<AtUriSample>(value);

    [Theory]
    [InlineData("did:plc:z72i7hdynmk6r22z27h6tvur")]
    [InlineData("did:web:example.com")]
    public void ADidRoundTripsThroughItsOwnOutput(string value)
    {
        AssertRoundTrips<DidSample>(value);
        AssertRoundTrips<AtIdentifierSample>(value);
    }

    [Theory]
    [InlineData("app.bsky.feed.post")]
    [InlineData("com.example.fooBar")]
    public void AnNsidRoundTripsThroughItsOwnOutput(string value) => AssertRoundTrips<NsidSample>(value);

    [Theory]
    [InlineData("3jui7kd54zh2y")]
    [InlineData("self")]
    [InlineData("literal:self")]
    public void ARecordKeyRoundTripsThroughItsOwnOutput(string value) => AssertRoundTrips<RecordKeySample>(value);

    [Theory]
    [InlineData("bafyreidfayvfuwqa7qlnopdjiqrxzs6blmoeu4rujcjtnci5beludirz2a")]
    [InlineData("QmY7Yh4UquoXHLPFo2XbhXkhBvFoPwmQUSa92pxnxjQuPU")]
    public void ACidRoundTripsThroughItsOwnOutput(string value) => AssertRoundTrips<CidSample>(value);

    private static void AssertOnlyJsonExceptionEscapes<T>(string value)
    {
        string json = JsonSerializer.Serialize(new Dictionary<string, string> { ["value"] = value }, s_jsonSerializerOptions);

        try
        {
            _ = JsonSerializer.Deserialize<T>(json, s_jsonSerializerOptions);
        }
        catch (JsonException)
        {
            // The only exception a converter is allowed to raise for a value it cannot read.
        }
        catch (Exception ex)
        {
            Assert.Fail($"Deserializing {json} as {typeof(T).Name} threw {ex.GetType().Name}: {ex.Message}");
        }
    }

    private static void AssertRoundTrips<T>(string value)
    {
        string json = JsonSerializer.Serialize(new Dictionary<string, string> { ["value"] = value }, s_jsonSerializerOptions);

        Assert.Equal(json, JsonSerializer.Serialize(JsonSerializer.Deserialize<T>(json, s_jsonSerializerOptions), s_jsonSerializerOptions));
    }

    private sealed class AtUriSample
    {
        public AtUri? Value { get; set; }
    }

    private sealed class AtIdentifierSample
    {
        public AtIdentifier? Value { get; set; }
    }

    private sealed class DidSample
    {
        public Did? Value { get; set; }
    }

    private sealed class HandleSample
    {
        public Handle? Value { get; set; }
    }

    private sealed class NsidSample
    {
        public Nsid? Value { get; set; }
    }

    private sealed class RecordKeySample
    {
        public RecordKey? Value { get; set; }
    }

    private sealed class TimestampIdentifierSample
    {
        public TimestampIdentifier? Value { get; set; }
    }

    private sealed class CidSample
    {
        public Cid? Value { get; set; }
    }

    private sealed class BytesSample
    {
        public Bytes? Value { get; set; }
    }
}
