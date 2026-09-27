// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json;

namespace idunno.AtProto.Types.Test;

public class BytesTests
{

    [Theory]
    [InlineData("v102dZsFvFw1oPjolcRtFl4QZ7oE/bY=", new byte[] { 191, 93, 54, 117, 155, 5, 188, 92, 53, 160, 248, 232, 149, 196, 109, 22, 94, 16, 103, 186, 4, 253, 182 })]
    [InlineData("XAiqHcuHBuUyh8OVx/XbZg==", new byte[] { 92, 8, 170, 29, 203, 135, 6, 229, 50, 135, 195, 149, 199, 245, 219, 102 })]
    [InlineData("avMVteiVYAoSIUw96F0xcSGraSuwgg==", new byte[] { 106, 243, 21, 181, 232, 149, 96, 10, 18, 33, 76, 61, 232, 93, 49, 113, 33, 171, 105, 43, 176, 130 })]
    [InlineData("X+5LK3RDNoRf", new byte[] { 95, 238, 75, 43, 116, 67, 54, 132, 95 })]
    [InlineData("xu23SWJ82k7pYIE7dy0J6E1h4+xwCVyVZZbXe8o=", new byte[] { 198, 237, 183, 73, 98, 124, 218, 78, 233, 96, 129, 59, 119, 45, 9, 232, 77, 97, 227, 236, 112, 9, 92, 149, 101, 150, 215, 123, 202 })]
    [InlineData("ATwQbV6F7XYB9552pxn7WYKg", new byte[] { 1, 60, 16, 109, 94, 133, 237, 118, 1, 247, 158, 118, 167, 25, 251, 89, 130, 160 })]
    [InlineData("6475ooowHNxnZjrQq8CJgQbVu22LfT+7oTWX", new byte[] { 235, 142, 249, 162, 138, 48, 28, 220, 103, 102, 58, 208, 171, 192, 137, 129, 6, 213, 187, 109, 139, 125, 63, 187, 161, 53, 151 })]
    [InlineData("PFrG/CvGSQ8p", new byte[] { 60, 90, 198, 252, 43, 198, 73, 15, 41 })]
    [InlineData("ZLWunGhvxGBLYP8IbeevgcE8eSE=", new byte[] { 100, 181, 174, 156, 104, 111, 196, 96, 75, 96, 255, 8, 109, 231, 175, 129, 193, 60, 121, 33 })]
    [InlineData("fzjPSKEKJjzX4w==", new byte[] { 127, 56, 207, 72, 161, 10, 38, 60, 215, 227 })]
    public void ValidBase64StringShouldConstruct(string base64String, byte[] expectedBytes)
    {
        var bytes = new Bytes(base64String);

        Assert.Equal(expectedBytes, bytes.Value);
    }

    [Theory]
    [InlineData("v102dZsFvFw1oPjolcRtFl4QZ7oE/bY=", new byte[] { 191, 93, 54, 117, 155, 5, 188, 92, 53, 160, 248, 232, 149, 196, 109, 22, 94, 16, 103, 186, 4, 253, 182 })]
    [InlineData("XAiqHcuHBuUyh8OVx/XbZg==", new byte[] { 92, 8, 170, 29, 203, 135, 6, 229, 50, 135, 195, 149, 199, 245, 219, 102 })]
    [InlineData("avMVteiVYAoSIUw96F0xcSGraSuwgg==", new byte[] { 106, 243, 21, 181, 232, 149, 96, 10, 18, 33, 76, 61, 232, 93, 49, 113, 33, 171, 105, 43, 176, 130 })]
    [InlineData("X+5LK3RDNoRf", new byte[] { 95, 238, 75, 43, 116, 67, 54, 132, 95 })]
    [InlineData("xu23SWJ82k7pYIE7dy0J6E1h4+xwCVyVZZbXe8o=", new byte[] { 198, 237, 183, 73, 98, 124, 218, 78, 233, 96, 129, 59, 119, 45, 9, 232, 77, 97, 227, 236, 112, 9, 92, 149, 101, 150, 215, 123, 202 })]
    [InlineData("ATwQbV6F7XYB9552pxn7WYKg", new byte[] { 1, 60, 16, 109, 94, 133, 237, 118, 1, 247, 158, 118, 167, 25, 251, 89, 130, 160 })]
    [InlineData("6475ooowHNxnZjrQq8CJgQbVu22LfT+7oTWX", new byte[] { 235, 142, 249, 162, 138, 48, 28, 220, 103, 102, 58, 208, 171, 192, 137, 129, 6, 213, 187, 109, 139, 125, 63, 187, 161, 53, 151 })]
    [InlineData("PFrG/CvGSQ8p", new byte[] { 60, 90, 198, 252, 43, 198, 73, 15, 41 })]
    [InlineData("ZLWunGhvxGBLYP8IbeevgcE8eSE=", new byte[] { 100, 181, 174, 156, 104, 111, 196, 96, 75, 96, 255, 8, 109, 231, 175, 129, 193, 60, 121, 33 })]
    [InlineData("fzjPSKEKJjzX4w==", new byte[] { 127, 56, 207, 72, 161, 10, 38, 60, 215, 227 })]
    public void ValueBytesShouldHaveCorrectStringValue(string expectedBase64String, byte[] byteArray)
    {
        var bytes = new Bytes(byteArray);

        Assert.Equal(expectedBase64String, bytes.ToString());
    }

    [Theory]
    [InlineData("invalid_base64_string")]
    // A length one more than a multiple of four cannot be produced by base64, padded or unpadded.
    [InlineData("v102dZsFvFw1oPjolcRtFl4QZ7oE/bYaZ")]
    [InlineData("=")]
    [InlineData("v102dZsFvFw1oPjolcRtFl4QZ7oE/bY===")]
    // Partial padding is neither the padded nor the unpadded encoding, so it is not completed into something which
    // decodes. "TQ=" needs "==" to be valid, and dropping the padding entirely would make it "TQ".
    [InlineData("TQ=")]
    [InlineData("v102dZsFvFw1oPjolcRtFl4QZ7oE/bYa=")]
    public void InvalidBase64StringShouldThrow(string invalidBase64String)
    {
        Assert.Throws<FormatException>(() => new Bytes(invalidBase64String));
    }

    [Fact]
    public void BothEncodingsOfTheSameValueAreEqual()
    {
        Assert.Equal(new Bytes("TQ=="), new Bytes("TQ"));
    }

    [Theory]
    // Jetstream, and other Go implementations, encode $bytes with base64.RawStdEncoding, which omits the padding,
    // and the AT Protocol data model makes the padding optional, so unpadded values must be accepted.
    [InlineData("v102dZsFvFw1oPjolcRtFl4QZ7oE/bY", new byte[] { 191, 93, 54, 117, 155, 5, 188, 92, 53, 160, 248, 232, 149, 196, 109, 22, 94, 16, 103, 186, 4, 253, 182 })]
    [InlineData("XAiqHcuHBuUyh8OVx/XbZg", new byte[] { 92, 8, 170, 29, 203, 135, 6, 229, 50, 135, 195, 149, 199, 245, 219, 102 })]
    [InlineData("fzjPSKEKJjzX4w", new byte[] { 127, 56, 207, 72, 161, 10, 38, 60, 215, 227 })]
    [InlineData("X+5LK3RDNoRf", new byte[] { 95, 238, 75, 43, 116, 67, 54, 132, 95 })]
    public void UnpaddedBase64StringShouldConstruct(string base64String, byte[] expectedBytes)
    {
        var bytes = new Bytes(base64String);

        Assert.Equal(expectedBytes, bytes.Value);
    }

    [Theory]
    // Convert.FromBase64String() ignores white space, so the padding has to be worked out from the significant
    // characters rather than the length of the string.
    [InlineData("XAiqHcuHBuUy h8OVx/XbZg")]
    [InlineData("XAiqHcuHBuUy\nh8OVx/XbZg")]
    [InlineData("XAiqHcuHBuUy h8OVx/XbZg==")]
    [InlineData(" XAiqHcuHBuUyh8OVx/XbZg ")]
    public void Base64StringContainingWhiteSpaceShouldConstruct(string base64String)
    {
        var bytes = new Bytes(base64String);

        Assert.Equal<byte[]>([92, 8, 170, 29, 203, 135, 6, 229, 50, 135, 195, 149, 199, 245, 219, 102], bytes.ToBytes());
    }

    [Theory]
    [InlineData("v102dZsFvFw1oPjolcRtFl4QZ7oE/bY=", "v102dZsFvFw1oPjolcRtFl4QZ7oE/bY")]
    [InlineData("XAiqHcuHBuUyh8OVx/XbZg==", "XAiqHcuHBuUyh8OVx/XbZg")]
    [InlineData("fzjPSKEKJjzX4w==", "fzjPSKEKJjzX4w")]
    public void PaddedAndUnpaddedBase64StringsShouldBeEqual(string padded, string unpadded)
    {
        var paddedBytes = new Bytes(padded);
        var unpaddedBytes = new Bytes(unpadded);

        Assert.Equal(paddedBytes, unpaddedBytes);
        Assert.Equal(paddedBytes.GetHashCode(), unpaddedBytes.GetHashCode());

        // Whether the value was padded or not, it normalizes to the padded form.
        Assert.Equal(padded, unpaddedBytes.ToString());
    }

    [Theory]
    [InlineData("""{"$bytes":"v102dZsFvFw1oPjolcRtFl4QZ7oE/bY"}""")]
    [InlineData("""{"$bytes":"v102dZsFvFw1oPjolcRtFl4QZ7oE/bY="}""")]
    public void PaddedAndUnpaddedBase64StringsShouldDeserializeIdentically(string json)
    {
        var expected = new Bytes(new byte[] { 191, 93, 54, 117, 155, 5, 188, 92, 53, 160, 248, 232, 149, 196, 109, 22, 94, 16, 103, 186, 4, 253, 182 });

        Assert.Equal(expected, JsonSerializer.Deserialize<Bytes>(json));
    }

    [Fact]
    public void EmptyByteArrayShouldThrowArgumentOutOfRangeException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Bytes([]));
    }

    [Fact]
    public void NullByteArrayShouldThrowArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new Bytes((byte[])null!));
    }

    [Fact]
    public void NullBase64StringShouldThrowArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new Bytes((string)null!));
    }

    [Fact]
    public void EmptyBase64StringShouldThrowArgumentException()
    {
        Assert.Throws<ArgumentException>(() => new Bytes(string.Empty));
    }

    [Fact]
    public void ShouldSerializeCorrectly()
    {
        var expectedJson = """{"$bytes":"v102dZsFvFw1oPjolcRtFl4QZ7oE/bY="}""";
        var bytes = new Bytes("v102dZsFvFw1oPjolcRtFl4QZ7oE/bY=");

        var actualJson = JsonSerializer.Serialize(bytes);
        Assert.Equal(expectedJson, actualJson);
    }

    [Fact]
    public void ShouldDeserializeCorrectly()
    {
        var json = """{"$bytes":"v102dZsFvFw1oPjolcRtFl4QZ7oE/bY="}""";
        var expectedBytes = new Bytes("v102dZsFvFw1oPjolcRtFl4QZ7oE/bY=");
        var actualBytes = JsonSerializer.Deserialize<Bytes>(json);
        Assert.Equal(expectedBytes, actualBytes);
    }

    [Fact]
    public void InvalidBase64StringShouldThrowDuringDeserialization()
    {
        var json = """{"$bytes":"invalid_base64_string"}""";
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<Bytes>(json));
    }

    [Fact]
    public void InvalidPropertyNameShouldThrowDuringDeserialization()
    {
        var json = """{"invalid_property":"v102dZsFvFw1oPjolcRtFl4QZ7oE/bY="}""";
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<Bytes>(json));
    }

    [Fact]
    public void EmptyValueShouldThrowDuringDeserialization()
    {
        var json = """{"$bytes":""}""";
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<Bytes>(json));
    }

    [Fact]
    public void NullValueShouldThrowDuringDeserialization()
    {
        var json = """{"$bytes":null}""";
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<Bytes>(json));
    }

    [Fact]
    public void IncorrectValueTypeShouldThrowDuringDeserialization()
    {
        var json = """{"$bytes":123}""";
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<Bytes>(json));
    }
}