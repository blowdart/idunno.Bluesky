// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.ComponentModel.DataAnnotations;
using System.Text.Json;

using idunno.AtProto;
using idunno.AtProto.Repo;

using idunno.Bluesky.Actor;

namespace idunno.Bluesky.Serialization.Test;

[ExcludeFromCodeCoverage]
public class ProfileTests
{
    [Theory]
    [InlineData("displayName", 64, 640)]
    [InlineData("description", 256, 2560)]
    [InlineData("pronouns", 20, 200)]
    public void ProfileParsingAcceptsTextAtBothLimits(string property, int graphemeLimit, int byteLimit)
    {
        string[] values = [new string('a', graphemeLimit), "ab" + new string('\u0301', (byteLimit - 2) / 2)];

        Assert.Equal(graphemeLimit, values[0].GetGraphemeLength());
        Assert.Equal(byteLimit, values[1].GetUtf8Length());
        foreach (string text in values)
        {
            using JsonDocument document = JsonDocument.Parse($$"""{"{{property}}":"{{text}}"}""");
            Assert.True(Profile.TryParse(document, out Profile? profile));
            Assert.Empty(profile!.Validate(new ValidationContext(profile)));
        }
    }

    [Fact]
    public void ProfileBlobValidationIsOptInOutsideTheParseMethods()
    {
        Profile source = new(avatar: new Blob(
            new CidLink("bafkreib6vy272khka47dedwort35c4zxl52j4ddijh2e7m23laaebotytq"), "image/gif", 1_000_001));
        string json = JsonSerializer.Serialize(source, BlueskyJsonSerializerOptions.Default);
        using JsonDocument document = JsonDocument.Parse(json);

        Assert.NotNull(JsonSerializer.Deserialize<Profile>(json, BlueskyJsonSerializerOptions.Default));
        Assert.False(Profile.TryParse(document, out _));
        Profile tolerant = Profile.Parse(document, validate: false);
        Assert.Equal(source.Avatar, tolerant.Avatar);
        Assert.Contains(tolerant.Validate(new ValidationContext(tolerant)), result => result.MemberNames.Contains("Avatar.Size"));
        Assert.Contains(tolerant.Validate(new ValidationContext(tolerant)), result => result.MemberNames.Contains("Avatar.MimeType"));
    }

    [Theory]
    [InlineData("displayName", 64, 640, false, "DisplayName")]
    [InlineData("displayName", 64, 640, true, "DisplayName")]
    [InlineData("description", 256, 2560, false, "Description")]
    [InlineData("description", 256, 2560, true, "Description")]
    [InlineData("pronouns", 20, 200, false, "Pronouns")]
    [InlineData("pronouns", 20, 200, true, "Pronouns")]
    public void TolerantProfileParsingRetainsOverLimitText(string property, int graphemeLimit, int byteLimit, bool testBytes, string expectedMember)
    {
        string text = testBytes ? "a" + new string('\u0301', byteLimit / 2) : new string('a', graphemeLimit + 1);
        string json = $$$"""{"$type":"app.bsky.actor.profile","{{{property}}}":"{{{text}}}","unknown":{"value":42}}""";
        Profile profile;
        using (JsonDocument document = JsonDocument.Parse(json))
        {
            Assert.False(Profile.TryParse(document, out Profile? rejected));
            Assert.Null(rejected);
            Assert.False(Profile.TryParse(document.RootElement, out _));
            Assert.Throws<JsonException>(() => Profile.Parse(document));
            Assert.Throws<JsonException>(() => Profile.Parse(document.RootElement));
            Assert.True(Profile.TryParse(document, validate: false, out Profile? parsed));
            Assert.NotNull(parsed);
            Assert.True(Profile.TryParse(document.RootElement, validate: false, out _));
            profile = Profile.Parse(document.RootElement, validate: false);
            Assert.NotNull(Profile.Parse(document, validate: false));

            Exception? exception = Xunit.Record.Exception(() => JsonSerializer.Deserialize<Profile>(json, BlueskyJsonSerializerOptions.Default));
            Assert.IsType<ArgumentOutOfRangeException>(exception);
        }

        Assert.Equal(42, profile.ExtensionData["unknown"].GetProperty("value").GetInt32());
        Assert.DoesNotContain("$type", profile.ExtensionData.Keys);
        Assert.Equal(text, expectedMember switch
        {
            nameof(Profile.DisplayName) => profile.DisplayName,
            nameof(Profile.Description) => profile.Description,
            nameof(Profile.Pronouns) => profile.Pronouns,
            _ => throw new ArgumentOutOfRangeException(nameof(expectedMember))
        });
        Assert.Contains(profile.Validate(new ValidationContext(profile)), result => result.MemberNames.Contains(expectedMember));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Profile { DisplayName = new string('x', 65) });
        Assert.Throws<ArgumentOutOfRangeException>(() => profile.Description = new string('x', 257));
        Assert.Throws<ArgumentOutOfRangeException>(() => profile.Pronouns = new string('x', 21));
        profile.DisplayName = "valid";
        profile.Description = "valid";
        profile.Pronouns = "valid";
        Assert.Empty(profile.Validate(new ValidationContext(profile)));
    }

    [Theory]
    [InlineData(10, false, true)]
    [InlineData(11, false, false)]
    [InlineData(1, true, false)]
    public void TolerantProfileParsingRetainsInvalidLinks(int count, bool containsNull, bool valid)
    {
        const string reference = """
            {"uri":"at://did:plc:ec72yg6n2sydzjvtovvdlxrk/app.bsky.actor.link/example","cid":"bafyreiaahtgpdozb4wjdymb3tvifuwdmxjtumhjxl25lykvsoq3v3l4une"}
            """;
        string links = string.Join(",", Enumerable.Repeat(containsNull ? "null" : reference, count));
        using JsonDocument document = JsonDocument.Parse($$"""{"$type":"app.bsky.actor.profile","links":[{{links}}]}""");

        Assert.Equal(valid, Profile.TryParse(document, out _));
        Profile profile = Profile.Parse(document, validate: false);

        Assert.Equal(count, profile.Links!.Count);
        Assert.Equal(valid, !profile.Validate(new ValidationContext(profile)).Any());
        if (!valid)
        {
            Assert.Contains(profile.Validate(new ValidationContext(profile)),
                result => result.MemberNames.Any(member => member.StartsWith(nameof(Profile.Links), StringComparison.Ordinal)));
            Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<Profile>(document.RootElement, BlueskyJsonSerializerOptions.Default));
        }
    }

    [Theory]
    [InlineData("null")]
    [InlineData("[]")]
    [InlineData("42")]
    [InlineData("""{"displayName":42}""")]
    [InlineData("""{"createdAt":"not a date"}""")]
    [InlineData("""{"links":{}}""")]
    [InlineData("""{"avatar":{"ref":{"$link":"not a cid"}}}""")]
    public void ProfileParsingRejectsMalformedShapesInBothModes(string json)
    {
        using JsonDocument document = JsonDocument.Parse(json);

        Assert.False(Profile.TryParse(document, out _));
        Assert.False(Profile.TryParse(document, validate: false, out Profile? result));
        Assert.Null(result);
        Assert.Throws<JsonException>(() => Profile.Parse(document, validate: false));
    }

    [Fact]
    public void ProfileParsingHandlesNullAndUndefinedInputs()
    {
        Assert.False(Profile.TryParse((JsonDocument?)null, out _));
        Assert.False(Profile.TryParse((JsonDocument?)null, validate: false, out _));
        Assert.False(Profile.TryParse(default(JsonElement), out _));
        Assert.False(Profile.TryParse(default(JsonElement), validate: false, out _));
        Assert.Throws<ArgumentNullException>(() => Profile.Parse((JsonDocument)null!));
        Assert.Throws<ArgumentNullException>(() => Profile.Parse((JsonDocument)null!, validate: false));
        Assert.Throws<JsonException>(() => Profile.Parse(default(JsonElement)));
        Assert.Throws<ArgumentNullException>(() => new Profile().Validate(null!));
    }

    [Fact]
    public void ProfileParsingPreservesKnownFieldsInBothModes()
    {
        Blob image = new(new CidLink("bafkreib6vy272khka47dedwort35c4zxl52j4ddijh2e7m23laaebotytq"), "image/jpeg", 1000);
        StrongReference reference = new(
            new AtUri("at://did:plc:ec72yg6n2sydzjvtovvdlxrk/app.bsky.feed.post/3ldegderd6s2e"),
            new Cid("bafyreiaahtgpdozb4wjdymb3tvifuwdmxjtumhjxl25lykvsoq3v3l4une"));
        Profile source = new("name", "description", "pronouns", new Uri("https://example.com"), image, image)
        {
            CreatedAt = new DateTimeOffset(2026, 10, 1, 0, 0, 0, TimeSpan.Zero),
            JoinedViaStarterPack = reference,
            PinnedPost = reference,
            Links = [reference]
        };
        source.Bot = true;
        using JsonDocument document = JsonDocument.Parse(JsonSerializer.Serialize(source, BlueskyJsonSerializerOptions.Default));

        Profile strict = Profile.Parse(document);
        Profile tolerant = Profile.Parse(document, validate: false);

        Assert.Equal(
            JsonSerializer.Serialize(strict, BlueskyJsonSerializerOptions.Default),
            JsonSerializer.Serialize(tolerant, BlueskyJsonSerializerOptions.Default));
        Assert.Equal(source.CreatedAt, tolerant.CreatedAt);
        Assert.Equal(source.Website, tolerant.Website);
        Assert.Equal(image, tolerant.Avatar);
        Assert.Equal(image, tolerant.Banner);
        Assert.Equal(reference, tolerant.JoinedViaStarterPack);
        Assert.Equal(reference, tolerant.PinnedPost);
        Assert.Equal(reference, Assert.Single(tolerant.Links!));
        Assert.True(tolerant.Bot);
        Assert.Empty(tolerant.Validate(new ValidationContext(tolerant)));
        using JsonDocument empty = JsonDocument.Parse("{}");
        Assert.Empty(Profile.Parse(empty).Validate(new ValidationContext(strict)));
        Assert.Empty(Profile.Parse(empty, validate: false).Validate(new ValidationContext(tolerant)));
    }

    [Fact]
    public void ProfileRecordDeserializesCorrectlyWithSourceGeneratedJsonContext()
    {
        string json = """
            {
                "$type": "app.bsky.actor.profile",
                "avatar": {
                    "$type": "blob",
                    "ref": {
                        "$link": "bafkreib6vy272khka47dedwort35c4zxl52j4ddijh2e7m23laaebotytq"
                    },
                    "mimeType": "image/jpeg",
                    "size": 357802
                },
                "banner": {
                    "$type": "blob",
                    "ref": {
                        "$link": "bafkreibn6toizt6ma2woo2etv2ttqtitnmutwbtqq6zzlnojjxu5zfql7m"
                    },
                    "mimeType": "image/jpeg",
                    "size": 246524
                },
                "labels": {
                    "$type": "com.atproto.label.defs#selfLabels",
                    "values": []
                },
                "pinnedPost": {
                    "cid": "bafyreiaahtgpdozb4wjdymb3tvifuwdmxjtumhjxl25lykvsoq3v3l4une",
                    "uri": "at://did:plc:ec72yg6n2sydzjvtovvdlxrk/app.bsky.feed.post/3ldegderd6s2e"
                },
                "description": "Updated on 12/22/24 04:13:44",
                "displayName": "Test Bot"
            }
            """;

        Profile? actual = JsonSerializer.Deserialize<Profile>(json, BlueskyServer.BlueskyJsonSerializerOptions);

        Assert.NotNull(actual);
    }
}