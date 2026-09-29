// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json;

using idunno.AtProto;
using idunno.Bluesky.Feed;
using idunno.Bluesky.Feed.Gates;
using idunno.Bluesky.Record;

namespace idunno.Bluesky.Serialization.Test;

[ExcludeFromCodeCoverage]
public class GeneratorRecordTests
{
    [Fact]
    public void LiveGeneratorRecordDeserializesAndSerializesWithSourceGeneratedJsonContext()
    {
        // Captured from at://did:plc:3guzzweuqraryl3rdkimjamk/app.bsky.feed.generator/for-you via the public API.
        string json = """
            {
                "$type": "app.bsky.feed.generator",
                "acceptsInteractions": true,
                "avatar": {
                    "$type": "blob",
                    "ref": {
                        "$link": "bafkreiff5zhxmc7fbx3a5snadgq2zrd6eoisoq4ewroh7wnjp6sd5eejsq"
                    },
                    "mimeType": "image/png",
                    "size": 160575
                },
                "createdAt": "2026-05-06T01:45:56.996858+00:00",
                "description": "A personalized algorithmic feed based on your likes.\n\nIt finds people who liked the same posts as you, and shows you what else they've liked recently.\n\nhttps://foryou.club\n",
                "did": "did:web:foryou.club",
                "displayName": "For You"
            }
            """;

        Generator? actual = JsonSerializer.Deserialize<Generator>(json, BlueskyServer.BlueskyJsonSerializerOptions);

        Assert.NotNull(actual);
        Assert.Equal("did:web:foryou.club", actual.Did.Value);
        Assert.Equal("For You", actual.DisplayName);
        Assert.Contains("personalized algorithmic feed", actual.Description, StringComparison.Ordinal);
        Assert.Equal("image/png", actual.Avatar?.MimeType);
        Assert.Equal(160575, actual.Avatar?.Size);
        Assert.True(actual.AcceptsInteractions);
        Assert.Equal(DateTimeOffset.Parse("2026-05-06T01:45:56.996858+00:00"), actual.CreatedAt);

        string serialized = JsonSerializer.Serialize<BlueskyRecord>(actual, BlueskyServer.BlueskyJsonSerializerOptions);
        BlueskyRecord? roundTripped = JsonSerializer.Deserialize<BlueskyRecord>(serialized, BlueskyServer.BlueskyJsonSerializerOptions);

        Assert.IsType<Generator>(roundTripped);
    }

    [Theory]
    [InlineData("app.bsky.feed.defs#contentModeUnspecified", GeneratorContentMode.Unspecified)]
    [InlineData("app.bsky.feed.defs#contentModeVideo", GeneratorContentMode.Video)]
    [InlineData("app.bsky.feed.defs#contentModeFuture", GeneratorContentMode.Unknown)]
    public void GeneratorContentModeDeserializesKnownAndUnknownValues(string contentMode, GeneratorContentMode expected)
    {
        string json = $$"""
            {
                "$type": "app.bsky.feed.generator",
                "did": "did:plc:ewvi7nxzyoun6zhxrhs64oiz",
                "displayName": "Test Generator",
                "contentMode": "{{contentMode}}",
                "createdAt": "2025-04-21T10:49:31.969Z"
            }
            """;

        Generator? actual = JsonSerializer.Deserialize<Generator>(json, BlueskyServer.BlueskyJsonSerializerOptions);

        Assert.NotNull(actual);
        Assert.Equal(expected, actual.ContentMode);
    }

    [Theory]
    [InlineData(RecordType.Generator, typeof(Generator))]
    [InlineData(RecordType.ThreadGate, typeof(ThreadGate))]
    [InlineData(RecordType.PostGate, typeof(PostGate))]
    public void BlueskyRecordDeserializesKnownRecordsToTheirConcreteType(string discriminator, Type expectedType)
    {
        string properties = discriminator switch
        {
            RecordType.Generator => """
                "did": "did:plc:ewvi7nxzyoun6zhxrhs64oiz",
                "displayName": "Test Generator",
                "createdAt": "2025-04-21T10:49:31.969Z"
                """,
            RecordType.ThreadGate or RecordType.PostGate => """
                "post": "at://did:plc:ewvi7nxzyoun6zhxrhs64oiz/app.bsky.feed.post/3lndpysuow22f",
                "createdAt": "2025-04-21T10:49:31.969Z"
                """,
            _ => throw new ArgumentOutOfRangeException(nameof(discriminator))
        };
        string json = $$"""
            {
                "$type": "{{discriminator}}",
                {{properties}}
            }
            """;

        BlueskyRecord? actual = JsonSerializer.Deserialize<BlueskyRecord>(json, BlueskyServer.BlueskyJsonSerializerOptions);

        Assert.NotNull(actual);
        Assert.IsType(expectedType, actual);
    }

    [Fact]
    public void ThreadGateSerializesItsDiscriminatorAsItsConcreteAndBaseType()
    {
        ThreadGate gate = new(new AtUri("at://did:plc:ewvi7nxzyoun6zhxrhs64oiz/app.bsky.feed.post/3lndpysuow22f"));

        string concreteJson = JsonSerializer.Serialize(gate, BlueskyServer.BlueskyJsonSerializerOptions);
        string baseJson = JsonSerializer.Serialize<BlueskyRecord>(gate, BlueskyServer.BlueskyJsonSerializerOptions);

        Assert.Contains("\"$type\":\"app.bsky.feed.threadgate\"", concreteJson, StringComparison.Ordinal);
        Assert.Contains("\"$type\":\"app.bsky.feed.threadgate\"", baseJson, StringComparison.Ordinal);
    }

    [Fact]
    public void PostGateSerializesItsDiscriminatorAsItsConcreteAndBaseType()
    {
        PostGate gate = new(new AtUri("at://did:plc:ewvi7nxzyoun6zhxrhs64oiz/app.bsky.feed.post/3lndpysuow22f"), null);

        string concreteJson = JsonSerializer.Serialize(gate, BlueskyServer.BlueskyJsonSerializerOptions);
        string baseJson = JsonSerializer.Serialize<BlueskyRecord>(gate, BlueskyServer.BlueskyJsonSerializerOptions);

        Assert.Contains("\"$type\":\"app.bsky.feed.postgate\"", concreteJson, StringComparison.Ordinal);
        Assert.Contains("\"$type\":\"app.bsky.feed.postgate\"", baseJson, StringComparison.Ordinal);
    }

}
