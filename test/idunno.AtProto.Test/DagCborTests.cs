// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Formats.Cbor;
using System.Text.Json;

namespace idunno.AtProto.Test;

public class DagCborTests
{
    [Theory]
    [InlineData(new byte[] { 0x00 }, "0")]
    [InlineData(new byte[] { 0x18, 0x2a }, "42")]
    [InlineData(new byte[] { 0x20 }, "-1")]
    [InlineData(new byte[] { 0x1b, 0x7f, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff }, "9223372036854775807")]
    [InlineData(new byte[] { 0x63, 0x61, 0x62, 0x63 }, "\"abc\"")]
    [InlineData(new byte[] { 0xf4 }, "false")]
    [InlineData(new byte[] { 0xf5 }, "true")]
    [InlineData(new byte[] { 0xf6 }, "null")]
    [InlineData(new byte[] { 0x80 }, "[]")]
    [InlineData(new byte[] { 0xa0 }, "{}")]
    [InlineData(new byte[] { 0x83, 0x01, 0x02, 0x03 }, "[1,2,3]")]
    public void PrimitiveValuesConvertToTheirJsonEquivalent(byte[] value, string expected)
    {
        JsonElement result = DagCbor.ToJsonElement(value);

        Assert.Equal(expected, result.GetRawText());
    }

    [Fact]
    public void ByteStringsConvertToABytesObject()
    {
        byte[] value = [0x44, 0x01, 0x02, 0x03, 0x04];

        JsonElement result = DagCbor.ToJsonElement(value);

        Assert.Equal(JsonValueKind.Object, result.ValueKind);
        Assert.Equal(Convert.ToBase64String([0x01, 0x02, 0x03, 0x04]), result.GetProperty("$bytes").GetString());
    }

    [Theory]
    [InlineData(long.MaxValue)]
    [InlineData(0L)]
    [InlineData(long.MinValue)]
    public void IntegersAtTheBoundsOfTheDataModelAreConverted(long value)
    {
        JsonElement result = DagCbor.ToJsonElement(Encode(writer => writer.WriteInt64(value)));

        Assert.Equal(value, result.GetInt64());
    }

    [Theory]
    // The AT Proto data model's integers are signed and 64 bit, so an unsigned integer larger than long.MaxValue,
    // which CBOR can encode, has no representation in the data model and is rejected.
    [InlineData(long.MaxValue + 1UL)]
    [InlineData(ulong.MaxValue)]
    public void IntegersLargerThanTheDataModelAllowsAreRejected(ulong value)
    {
        byte[] encoded = Encode(writer => writer.WriteUInt64(value));

        Assert.Throws<InvalidDataException>(() => DagCbor.ToJsonElement(encoded));
    }

    [Fact]
    public void ByteStringsConvertToABytesObjectWhichDeserializesToBytes()
    {
        byte[] expected = [0x01, 0x02, 0x03, 0x04, 0x05];

        JsonElement result = DagCbor.ToJsonElement(Encode(writer => writer.WriteByteString(expected)));

        Bytes bytes = new(result.GetProperty("$bytes").GetString()!);

        Assert.Equal(expected, bytes.ToBytes());
    }

    [Fact]
    public void CidLinksConvertToALinkObject()
    {
        Cid cid = Cid.FromDagCbor([0x01, 0x02, 0x03]);

        JsonElement result = DagCbor.ToJsonElement(EncodeLink(cid));

        Assert.Equal(JsonValueKind.Object, result.ValueKind);
        Assert.Equal(cid.Value, result.GetProperty("$link").GetString());
    }

    [Fact]
    public void CidLinksConvertToALinkObjectWhichDeserializesToACidLink()
    {
        Cid cid = Cid.FromDagCbor([0x0a, 0x0b]);

        JsonElement result = DagCbor.ToJsonElement(EncodeLink(cid));

        Assert.Equal(cid, new Cid(result.GetProperty("$link").GetString()!));
    }

    [Fact]
    public void MapsConvertToAnObjectPreservingKeysAndValues()
    {
        byte[] value = Encode(writer =>
        {
            writer.WriteStartMap(2);
            writer.WriteTextString("$type");
            writer.WriteTextString("app.bsky.feed.post");
            writer.WriteTextString("text");
            writer.WriteTextString("Hello");
            writer.WriteEndMap();
        });

        JsonElement result = DagCbor.ToJsonElement(value);

        Assert.Equal("app.bsky.feed.post", result.GetProperty("$type").GetString());
        Assert.Equal("Hello", result.GetProperty("text").GetString());
    }

    [Fact]
    public void NestedValuesConvertToNestedJson()
    {
        byte[] value = Encode(writer =>
        {
            writer.WriteStartMap(1);
            writer.WriteTextString("reply");
            writer.WriteStartMap(1);
            writer.WriteTextString("parent");
            writer.WriteStartMap(1);
            writer.WriteTextString("uri");
            writer.WriteTextString("at://did:plc:abc/app.bsky.feed.post/rkey");
            writer.WriteEndMap();
            writer.WriteEndMap();
            writer.WriteEndMap();
        });

        JsonElement result = DagCbor.ToJsonElement(value);

        Assert.Equal(
            "at://did:plc:abc/app.bsky.feed.post/rkey",
            result.GetProperty("reply").GetProperty("parent").GetProperty("uri").GetString());
    }

    [Fact]
    public void AnIndefiniteLengthArrayIsRejected()
    {
        byte[] value = Encode(writer =>
        {
            writer.WriteStartArray(null);
            writer.WriteInt32(1);
            writer.WriteInt32(2);
            writer.WriteEndArray();
        }, CborConformanceMode.Lax);

        Assert.Throws<InvalidDataException>(() => DagCbor.ToJsonElement(value));
    }

    [Theory]
    [InlineData(new byte[] { 0xf9, 0x3c, 0x00 })]
    [InlineData(new byte[] { 0xfa, 0x3f, 0x80, 0x00, 0x00 })]
    [InlineData(new byte[] { 0xfb, 0x3f, 0xf0, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 })]
    public void FloatingPointValuesAreRejected(byte[] value)
    {
        InvalidDataException exception = Assert.Throws<InvalidDataException>(() => DagCbor.ToJsonElement(value));

        Assert.Contains("floating point", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AnUnsupportedTagIsRejected()
    {
        byte[] value = Encode(writer =>
        {
            writer.WriteTag((CborTag)1);
            writer.WriteInt32(0);
        }, CborConformanceMode.Lax);

        InvalidDataException exception = Assert.Throws<InvalidDataException>(() => DagCbor.ToJsonElement(value));

        Assert.Contains("tag", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AMapKeyWhichIsNotAStringIsRejected()
    {
        byte[] value = Encode(writer =>
        {
            writer.WriteStartMap(1);
            writer.WriteInt32(1);
            writer.WriteInt32(2);
            writer.WriteEndMap();
        }, CborConformanceMode.Lax);

        InvalidDataException exception = Assert.Throws<InvalidDataException>(() => DagCbor.ToJsonElement(value));

        Assert.Contains("not a string", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AnEmptyValueIsRejected()
    {
        InvalidDataException exception = Assert.Throws<InvalidDataException>(() => DagCbor.ToJsonElement(ReadOnlyMemory<byte>.Empty));

        Assert.Contains("no data", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void TrailingDataIsRejected()
    {
        InvalidDataException exception = Assert.Throws<InvalidDataException>(() => DagCbor.ToJsonElement(new byte[] { 0x01, 0x02 }));

        Assert.Contains("trailing data", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void TruncatedDataIsRejected()
    {
        Assert.Throws<InvalidDataException>(() => DagCbor.ToJsonElement(new byte[] { 0x83, 0x01 }));
    }

    [Fact]
    public void ALinkWithoutTheMultibasePrefixIsRejected()
    {
        byte[] value = Encode(writer =>
        {
            writer.WriteTag((CborTag)42);
            writer.WriteByteString([0x01, 0x71]);
        });

        InvalidDataException exception = Assert.Throws<InvalidDataException>(() => DagCbor.ToJsonElement(value));

        Assert.Contains("invalid prefix", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ValuesNestedTooDeeplyAreRejected()
    {
        byte[] value = Encode(writer =>
        {
            for (int i = 0; i <= 200; i++)
            {
                writer.WriteStartArray(1);
            }

            writer.WriteInt32(0);

            for (int i = 0; i <= 200; i++)
            {
                writer.WriteEndArray();
            }
        });

        InvalidDataException exception = Assert.Throws<InvalidDataException>(() => DagCbor.ToJsonElement(value));

        Assert.Contains("levels deep", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void TryToJsonElementReturnsTrueForAValidValue()
    {
        Assert.True(DagCbor.TryToJsonElement(new byte[] { 0x01 }, out JsonElement result));
        Assert.Equal("1", result.GetRawText());
    }

    [Fact]
    public void TryToJsonElementReturnsFalseForAnInvalidValue()
    {
        Assert.False(DagCbor.TryToJsonElement(new byte[] { 0xfb, 0x3f, 0xf0, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 }, out JsonElement result));
        Assert.Equal(default, result);
    }

    [Fact]
    public void ToJsonElementReturnsAnElementWhichOutlivesItsDocument()
    {
        JsonElement result = DagCbor.ToJsonElement(new byte[] { 0x63, 0x61, 0x62, 0x63 });

        GC.Collect();
        GC.WaitForPendingFinalizers();

        Assert.Equal("abc", result.GetString());
    }

    [Fact]
    public void ToJsonDocumentReturnsADisposableDocument()
    {
        using JsonDocument document = DagCbor.ToJsonDocument(new byte[] { 0x63, 0x61, 0x62, 0x63 });

        Assert.Equal("abc", document.RootElement.GetString());
    }

    private static byte[] Encode(Action<CborWriter> write, CborConformanceMode conformanceMode = CborConformanceMode.Canonical)
    {
        CborWriter writer = new(conformanceMode, convertIndefiniteLengthEncodings: false);
        write(writer);

        return writer.Encode();
    }

    private static byte[] EncodeLink(Cid cid) => Encode(writer =>
    {
        writer.WriteTag((CborTag)42);
        writer.WriteByteString([0x00, .. cid.ToBytes()]);
    });
}
