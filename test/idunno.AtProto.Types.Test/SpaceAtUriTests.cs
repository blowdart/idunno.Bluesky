// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

#pragma warning disable BSKYSpaces

using System.Text.Json;

using idunno.AtProto.Json;

namespace idunno.AtProto.Types.Test;

// Samples come from the permissioned data proposal,
// https://github.com/bluesky-social/proposals/blob/119f2a0ab7639d00e47491c549e353c80b53f3c9/0016-permissioned-data/README.md
// and the @atproto/syntax tests in https://github.com/bluesky-social/atproto/pull/6010.
[ExcludeFromCodeCoverage]
public class SpaceAtUriTests
{
    private const string Space = "at:space/did:plc:asdf123/com.example.group/default";
    private const string SpaceRecord = $"{Space}/did:plc:user1/com.atproto.feed.post/abc123";

    private readonly JsonSerializerOptions _jsonSerializerOptions = new(JsonSerializerDefaults.Web);

    [Theory]
    [InlineData(Space, "did:plc:asdf123", "com.example.group", "default", null, null, null)]
    [InlineData(SpaceRecord, "did:plc:asdf123", "com.example.group", "default", "did:plc:user1", "com.atproto.feed.post", "abc123")]
    [InlineData("at:space/did:example:forum/com.atmoboards.forum/general", "did:example:forum", "com.atmoboards.forum", "general", null, null, null)]
    [InlineData(
        "at:space/did:example:forum/com.atmoboards.forum/general/did:example:alice/com.atmoboards.thread/3mwcj3b6uak2f",
        "did:example:forum",
        "com.atmoboards.forum",
        "general",
        "did:example:alice",
        "com.atmoboards.thread",
        "3mwcj3b6uak2f")]
    [InlineData("at:space/did:web:example.com/app.example.space/self", "did:web:example.com", "app.example.space", "self", null, null, null)]
    [InlineData(
        "at:space/did:plc:ec72yg6n2sydzjvtovvdlxrk/app.example.space/self/did:plc:ec72yg6n2sydzjvtovvdlxrk/app.bsky.feed.post/3koaf5bu5kq27",
        "did:plc:ec72yg6n2sydzjvtovvdlxrk",
        "app.example.space",
        "self",
        "did:plc:ec72yg6n2sydzjvtovvdlxrk",
        "app.bsky.feed.post",
        "3koaf5bu5kq27")]
    public void ValidSpaceUriParsesIntoComponents(
        string value,
        string spaceDid,
        string spaceType,
        string spaceKey,
        string? authorDid,
        string? collection,
        string? recordKey)
    {
        Assert.True(AtUri.TryParse(value, out AtUri? parsed));

        SpaceAtUri spaceUri = Assert.IsType<SpaceAtUri>(parsed);

        Assert.True(spaceUri.IsSpaceUri);
        Assert.Null(spaceUri.Repo);
        Assert.Equal(new Did(spaceDid), spaceUri.Authority);
        Assert.Equal(new Did(spaceDid), spaceUri.SpaceDid);
        Assert.Equal(new Nsid(spaceType), spaceUri.SpaceType);
        Assert.Equal(spaceKey, spaceUri.SpaceKey);
        Assert.Null(spaceUri.AbsolutePath);
        Assert.Equal(authorDid is not null, spaceUri.IsRecord);
        Assert.Equal(authorDid is null ? null : new Did(authorDid), spaceUri.AuthorDid);
        Assert.Equal(collection is null ? null : new Nsid(collection), spaceUri.Collection);
        Assert.Equal(recordKey is null ? null : new RecordKey(recordKey), spaceUri.RecordKey);
        Assert.Equal(value, spaceUri.ToString());
    }

    [Theory]
    [InlineData("at:space")]
    [InlineData("at:space/")]
    [InlineData("at:space/did:plc:asdf123")]
    [InlineData("at:space/did:plc:asdf123/com.example.group")]
    [InlineData("at:space/did:plc:asdf123/short/default")]
    [InlineData("at:space/did:plc:asdf123/com.example.group/..")]
    [InlineData("at:space/user.bsky.social/com.example.group/default")]
    [InlineData("at:space/did:plc/com.example.group/default")]
    [InlineData("at:space//did:plc:asdf123/com.example.group/default")]
    [InlineData($"{Space}/")]
    [InlineData($"{Space}/user.bsky.social/com.atproto.feed.post/abc123")]
    [InlineData($"{Space}/did:plc:user1")]
    [InlineData($"{Space}/did:plc:user1/com.atproto.feed.post")]
    [InlineData($"{Space}/did:plc:user1/com.atproto.feed.post/abc123/extra")]
    [InlineData($"{Space}/did:plc:user1/short/abc123")]
    [InlineData($"{Space}/did:plc:user1/com.atproto.feed.post/..")]
    [InlineData($"{Space}/did:plc:user1/com.atproto.feed.post/%%%")]
    [InlineData($"{Space}?foo=bar")]
    [InlineData($"{Space}#")]
    [InlineData("at:SPACE/did:plc:asdf123/com.example.group/default")]
    [InlineData("at://did:plc:asdf123/space/com.example.group/default")]
    public void InvalidSpaceUriIsRejected(string value)
    {
        Assert.False(AtUri.TryParse(value, out _));
        Assert.False(SpaceAtUri.TryParse(value, out _));
        Assert.Throws<AtUriFormatException>(() => new AtUri(value));
        Assert.Throws<AtUriFormatException>(() => new SpaceAtUri(value));
    }

    // Legacy examples are from the alpha SDK syntax and PDS migration tests at
    // https://github.com/bluesky-social/atproto/tree/1f510f8e36a973f6fd055da349b586918805ae66/packages
    [Theory]
    [InlineData("at://did:plc:asdf123/space/com.example.group/default", Space)]
    [InlineData(
        "at://did:plc:asdf123/space/com.example.group/default/did:plc:user1/com.atproto.feed.post/abc123",
        SpaceRecord)]
    [InlineData(
        "at://did:plc:authority/space/com.example.group/default/did:plc:writer/com.example.post/3kbcq3p7ad401",
        "at:space/did:plc:authority/com.example.group/default/did:plc:writer/com.example.post/3kbcq3p7ad401")]
    [InlineData(
        "at://did:web:localhost%3A2583/space/com.example.group/space",
        "at:space/did:web:localhost%3A2583/com.example.group/space")]
    [InlineData(Space, Space)]
    [InlineData(SpaceRecord, SpaceRecord)]
    public void RecordValueSpaceUriNormalizesLegacyAndCurrentForms(string input, string expected)
    {
        Assert.True(SpaceAtUri.TryParseRecordValue(input, out SpaceAtUri? result));
        Assert.Equal(expected, result.ToString());
        Assert.Equal(new SpaceAtUri(expected), result);
        Assert.Equal(new SpaceAtUri(expected).GetHashCode(), result.GetHashCode());

        if (input.StartsWith("at://", StringComparison.Ordinal))
        {
            Assert.False(SpaceAtUri.TryParse(input, out _));
            Assert.False(AtUri.TryParse(input, out _));
        }
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("at://user.bsky.social/space/com.example.group/default")]
    [InlineData("at://did:plc:asdf123/space/com.example.group")]
    [InlineData("at://did:plc:asdf123/space/com.example.group/default/")]
    [InlineData("at://did:plc:asdf123/space/com.example.group/default/did:plc:user1")]
    [InlineData("at://did:plc:asdf123/space/com.example.group/default/user.bsky.social/com.example.post/abc")]
    [InlineData("at://did:plc:asdf123/space/com.example.group/..")]
    [InlineData("at://did:plc:asdf123/space/com.example.group/default?x=1")]
    [InlineData("at://did:plc:asdf123/SPACE/com.example.group/default")]
    [InlineData("at://did:plc:asdf123/com.example.post/space")]
    [InlineData("at://did:plc:asdf123/space/com.example.group/default/did:plc:user1/com.example.post/key/extra")]
    public void RecordValueSpaceUriRejectsInvalidOrPublicValues(string? value)
    {
        Assert.False(SpaceAtUri.TryParseRecordValue(value, out SpaceAtUri? result));
        Assert.Null(result);
    }

    [Theory]
    [InlineData("at://did:plc:asdf123/space/com.example.group/default", Space)]
    [InlineData(
        "at://did:plc:asdf123/space/com.example.group/default/did:plc:user1/com.atproto.feed.post/abc123",
        SpaceRecord)]
    public void RecordValueConverterAcceptsLegacyUrisWithoutChangingStrictJson(string input, string expected)
    {
        string json = $$"""{"uri":"{{input}}"}""";
        JsonSerializerOptions options = new(JsonSerializerDefaults.Web);
        options.Converters.Add(new SpaceRecordValueUriConverter());

        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<AtUriHolder>(json, _jsonSerializerOptions));
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<SpaceAtUriSample>(json, _jsonSerializerOptions));

        AtUriHolder atUri = Assert.IsType<AtUriHolder>(JsonSerializer.Deserialize<AtUriHolder>(json, options));
        SpaceAtUriSample spaceUri = Assert.IsType<SpaceAtUriSample>(JsonSerializer.Deserialize<SpaceAtUriSample>(json, options));
        Assert.IsType<SpaceAtUri>(atUri.Uri);
        Assert.Equal(expected, atUri.Uri.ToString());
        Assert.Equal(expected, spaceUri.Uri.ToString());
        Assert.Equal($$"""{"uri":"{{expected}}"}""", JsonSerializer.Serialize(atUri, options));
        Assert.Equal($$"""{"uri":"{{expected}}"}""", JsonSerializer.Serialize(spaceUri, options));
    }

    [Fact]
    public void RecordValueConverterPreservesPublicUrisAndRejectsMalformedValues()
    {
        JsonSerializerOptions options = new(JsonSerializerDefaults.Web);
        options.Converters.Add(new SpaceRecordValueUriConverter());
        const string publicUri = "at://did:plc:asdf123/com.atproto.feed.post/abc123";

        AtUriHolder parsed = Assert.IsType<AtUriHolder>(
            JsonSerializer.Deserialize<AtUriHolder>($$"""{"uri":"{{publicUri}}"}""", options));
        Assert.Equal(typeof(AtUri), parsed.Uri.GetType());
        Assert.Equal(publicUri, parsed.Uri.ToString());
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<SpaceAtUriSample>($$"""{"uri":"{{publicUri}}"}""", options));
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<AtUriHolder>("""{"uri":"at://user.bsky.social/space/a.b.c/key"}""", options));
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<AtUriHolder>("""{"uri":42}""", options));
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<SpaceAtUriSample>("""{"uri":42}""", options));
    }

    [Theory]
    [InlineData(Space)]
    [InlineData(SpaceRecord)]
    public void ConstructorReturnsBaseTypeThatEqualsParsedSpaceUri(string value)
    {
        AtUri constructed = new(value);
        SpaceAtUri parsed = SpaceAtUri.Parse(value, null);

        Assert.Equal(typeof(AtUri), constructed.GetType());
        Assert.True(constructed.IsSpaceUri);
        Assert.Null(constructed.Repo);

        Assert.True(constructed.Equals(parsed));
        Assert.True(parsed.Equals(constructed));
        Assert.True(constructed == parsed);
        Assert.False(constructed != parsed);
        Assert.Equal(constructed.GetHashCode(), parsed.GetHashCode());

        Assert.True(constructed.TryGetSpaceUri(out SpaceAtUri? converted));
        Assert.Equal(parsed, converted);
        Assert.Equal(parsed.SpaceKey, converted.SpaceKey);
        Assert.Equal(parsed.AuthorDid, converted.AuthorDid);
    }

    [Theory]
    [InlineData(Space)]
    [InlineData(SpaceRecord)]
    public void FactoriesReturnSpaceAtUri(string value)
    {
        AtUri implicitlyConverted = value;

        Assert.IsType<SpaceAtUri>(AtUri.FromString(value));
        Assert.IsType<SpaceAtUri>(AtUri.Parse(value, null));
        Assert.IsType<SpaceAtUri>(implicitlyConverted);
        Assert.True(AtUri.TryParse(value, null, out AtUri? parsed));
        Assert.IsType<SpaceAtUri>(parsed);
    }

    [Fact]
    public void PublicAtUriIsNotASpaceUri()
    {
        const string value = "at://did:plc:asdf123/com.example.group/default";

        AtUri parsed = AtUri.Parse(value, null);

        Assert.Equal(typeof(AtUri), parsed.GetType());
        Assert.False(parsed.IsSpaceUri);
        Assert.Equal(new Did("did:plc:asdf123"), parsed.Repo);
        Assert.False(parsed.TryGetSpaceUri(out SpaceAtUri? spaceUri));
        Assert.Null(spaceUri);

        Assert.False(SpaceAtUri.TryParse(value, out _));
        Assert.Throws<FormatException>(() => SpaceAtUri.Parse(value, null));
        Assert.Throws<AtUriFormatException>(() => new SpaceAtUri(value));
    }

    [Fact]
    public void SpaceUriDoesNotEqualPublicAtUriWithTheSameComponents()
    {
        AtUri publicUri = new("at://did:plc:asdf123/com.atproto.feed.post/abc123");
        SpaceAtUri spaceUri = new(SpaceRecord);

        Assert.Equal(publicUri.Authority, spaceUri.Authority);
        Assert.Equal(publicUri.Collection, spaceUri.Collection);

        Assert.NotEqual(publicUri, spaceUri);
        Assert.NotEqual<AtUri>(spaceUri, publicUri);
    }

    [Theory]
    [InlineData(Space, $"{Space}/did:plc:user1/com.atproto.feed.post/abc123")]
    [InlineData(Space, "at:space/did:plc:asdf123/com.example.group/other")]
    [InlineData(Space, "at:space/did:plc:asdf124/com.example.group/default")]
    [InlineData(Space, "at:space/did:plc:asdf123/com.example.other/default")]
    [InlineData(SpaceRecord, $"{Space}/did:plc:user2/com.atproto.feed.post/abc123")]
    [InlineData(SpaceRecord, $"{Space}/did:plc:user1/com.atproto.feed.post/abc124")]
    public void SpaceUrisWithDifferentComponentsAreNotEqual(string first, string second)
    {
        Assert.NotEqual(new AtUri(first), new AtUri(second));
        Assert.NotEqual(SpaceAtUri.Parse(first, null), SpaceAtUri.Parse(second, null));
    }

    [Fact]
    public void SpaceConstructorMatchesParsedSpace()
    {
        SpaceAtUri constructed = new(new Did("did:plc:asdf123"), new Nsid("com.example.group"), "default");

        Assert.False(constructed.IsRecord);
        Assert.Equal(Space, constructed.ToString());
        Assert.Equal(new AtUri(Space), constructed);
    }

    [Fact]
    public void SpaceRecordConstructorMatchesParsedSpaceRecord()
    {
        SpaceAtUri constructed = new(
            spaceDid: new Did("did:plc:asdf123"),
            spaceType: new Nsid("com.example.group"),
            spaceKey: "default",
            authorDid: new Did("did:plc:user1"),
            collection: new Nsid("com.atproto.feed.post"),
            recordKey: new RecordKey("abc123"));

        Assert.True(constructed.IsRecord);
        Assert.Equal(SpaceRecord, constructed.ToString());
        Assert.Equal(new AtUri(SpaceRecord), constructed);
    }

    [Theory]
    [InlineData("..")]
    [InlineData(".")]
    [InlineData("")]
    [InlineData("has space")]
    [InlineData("has/slash")]
    public void SpaceConstructorRejectsInvalidSpaceKey(string spaceKey)
    {
        ArgumentException exception = Assert.Throws<ArgumentException>(
            () => new SpaceAtUri(new Did("did:plc:asdf123"), new Nsid("com.example.group"), spaceKey));

        Assert.Equal("spaceKey", exception.ParamName);
    }

    [Theory]
    [InlineData(Space)]
    [InlineData(SpaceRecord)]
    public void SpaceUriRoundTripsThroughAtUriJson(string value)
    {
        string json = JsonSerializer.Serialize(new AtUriHolder(new AtUri(value)), _jsonSerializerOptions);

        Assert.Equal($"{{\"uri\":\"{value}\"}}", json);

        AtUriHolder? deserialized = JsonSerializer.Deserialize<AtUriHolder>(json, _jsonSerializerOptions);

        Assert.NotNull(deserialized);
        SpaceAtUri spaceUri = Assert.IsType<SpaceAtUri>(deserialized.Uri);
        Assert.Equal(value, spaceUri.ToString());
    }

    [Theory]
    [InlineData(Space)]
    [InlineData(SpaceRecord)]
    public void SpaceUriRoundTripsThroughSpaceAtUriJson(string value)
    {
        string json = JsonSerializer.Serialize(new SpaceAtUriSample(SpaceAtUri.Parse(value, null)), _jsonSerializerOptions);

        Assert.Equal($"{{\"uri\":\"{value}\"}}", json);

        SpaceAtUriSample? deserialized = JsonSerializer.Deserialize<SpaceAtUriSample>(json, _jsonSerializerOptions);

        Assert.NotNull(deserialized);
        Assert.Equal(value, deserialized.Uri.ToString());
    }

    [Theory]
    [InlineData("{\"uri\":\"at://did:plc:asdf123/com.atproto.feed.post/abc123\"}")]
    [InlineData("{\"uri\":\"at:space/did:plc:asdf123/com.example.group\"}")]
    [InlineData("{\"uri\":\"\"}")]
    [InlineData("{\"uri\":0}")]
    public void SpaceAtUriJsonRejectsValuesThatAreNotSpaceUris(string json) =>
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<SpaceAtUriSample>(json, _jsonSerializerOptions));

    [Fact]
    public void SpaceUriTooLongIsRejected()
    {
        string value = $"{Space}/did:plc:user1/com.atproto.feed.post/{new string('a', 8192)}";

        Assert.False(AtUri.TryParse(value, out _));
    }

    private sealed record AtUriHolder(AtUri Uri);

    private sealed record SpaceAtUriSample(SpaceAtUri Uri);
}
