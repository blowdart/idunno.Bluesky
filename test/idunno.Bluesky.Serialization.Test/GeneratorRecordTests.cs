// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json;

using idunno.AtProto;
using idunno.AtProto.Repo;
using idunno.Bluesky.Feed;
using idunno.Bluesky.Feed.Gates;
using idunno.Bluesky.Record;

namespace idunno.Bluesky.Serialization.Test;

[ExcludeFromCodeCoverage]
public class GeneratorRecordTests
{
    public static TheoryData<string> BlueskyRecordDiscriminatorDocuments =>
    [
        """{"$type":"app.bsky.feed.post","text":"Test post","createdAt":"2025-04-21T10:49:31.969Z"}""",
        """{"$type":"app.bsky.feed.generator","did":"did:plc:ewvi7nxzyoun6zhxrhs64oiz","displayName":"Test Generator","createdAt":"2025-04-21T10:49:31.969Z"}""",
        """{"$type":"app.bsky.graph.follow","subject":"did:plc:ewvi7nxzyoun6zhxrhs64oiz","createdAt":"2025-04-21T10:49:31.969Z"}""",
        """{"$type":"app.bsky.feed.repost","subject":{"uri":"at://did:plc:ewvi7nxzyoun6zhxrhs64oiz/app.bsky.feed.post/3lndpysuow22f","cid":"bafyreihszwqbe7nxh2mv4o2wk365yanh3p3zimbunfrjdtq7gcmmzohx6i"},"createdAt":"2025-04-21T10:49:31.969Z"}""",
        """{"$type":"app.bsky.feed.threadgate","post":"at://did:plc:ewvi7nxzyoun6zhxrhs64oiz/app.bsky.feed.post/3lndpysuow22f","createdAt":"2025-04-21T10:49:31.969Z"}""",
        """{"$type":"app.bsky.feed.postgate","post":"at://did:plc:ewvi7nxzyoun6zhxrhs64oiz/app.bsky.feed.post/3lndpysuow22f","createdAt":"2025-04-21T10:49:31.969Z"}""",
        """{"$type":"app.bsky.feed.like","subject":{"uri":"at://did:plc:ewvi7nxzyoun6zhxrhs64oiz/app.bsky.feed.post/3lndpysuow22f","cid":"bafyreihszwqbe7nxh2mv4o2wk365yanh3p3zimbunfrjdtq7gcmmzohx6i"},"createdAt":"2025-04-21T10:49:31.969Z"}""",
        """{"$type":"app.bsky.graph.block","subject":"did:plc:ewvi7nxzyoun6zhxrhs64oiz","createdAt":"2025-04-21T10:49:31.969Z"}""",
        """{"$type":"app.bsky.actor.profile"}""",
        """{"$type":"app.bsky.graph.starterpack","name":"Test starter pack","list":"at://did:plc:ewvi7nxzyoun6zhxrhs64oiz/app.bsky.graph.list/test","createdAt":"2025-04-21T10:49:31.969Z"}""",
        """{"$type":"app.bsky.labeler.service","policies":{"labelValues":[]},"createdAt":"2025-04-21T10:49:31.969Z"}""",
        """{"$type":"app.bsky.graph.verification","handle":"example.com","subject":"did:plc:ewvi7nxzyoun6zhxrhs64oiz","displayName":"Example","createdAt":"2025-04-21T10:49:31.969Z"}""",
        """{"$type":"app.bsky.graph.list","name":"Test list","purpose":"app.bsky.graph.defs#curatelist","createdAt":"2025-04-21T10:49:31.969Z"}""",
        """{"$type":"app.bsky.graph.listitem","list":"at://did:plc:ewvi7nxzyoun6zhxrhs64oiz/app.bsky.graph.list/test","subject":"did:plc:ewvi7nxzyoun6zhxrhs64oiz"}""",
        """{"$type":"app.bsky.notification.declaration"}""",
        """{"$type":"app.bsky.actor.status","status":"app.bsky.actor.status#live","createdAt":"2025-04-21T10:49:31.969Z"}""",
        """{"$type":"app.bsky.graph.referencelistoptout","subject":"at://did:plc:ewvi7nxzyoun6zhxrhs64oiz/app.bsky.graph.list/test","createdAt":"2025-04-21T10:49:31.969Z"}""",
        """{"$type":"app.bsky.graph.listblock","subject":"at://did:plc:ewvi7nxzyoun6zhxrhs64oiz/app.bsky.graph.list/test","createdAt":"2025-04-21T10:49:31.969Z"}""",
        """{"$type":"app.bsky.actor.contentVisibilityDeclaration","hideFromAlgorithmicRecommendations":false}"""
    ];

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

        string concreteSerialized = JsonSerializer.Serialize(actual, BlueskyServer.BlueskyJsonSerializerOptions);
        Assert.Contains("\"$type\":\"app.bsky.feed.generator\"", concreteSerialized, StringComparison.Ordinal);

        string serialized = JsonSerializer.Serialize<BlueskyRecord>(actual, BlueskyServer.BlueskyJsonSerializerOptions);
        BlueskyRecord? roundTripped = JsonSerializer.Deserialize<BlueskyRecord>(serialized, BlueskyServer.BlueskyJsonSerializerOptions);

        Assert.IsType<Generator>(roundTripped);
    }

    [Theory]
    [MemberData(nameof(BlueskyRecordDiscriminatorDocuments))]
    public void BlueskyRecordSerializesEveryConcreteLexiconDiscriminator(string json)
    {
        using JsonDocument sourceDocument = JsonDocument.Parse(json);
        string expectedDiscriminator = sourceDocument.RootElement.GetProperty("$type").GetString()!;

        BlueskyRecord? record = JsonSerializer.Deserialize<BlueskyRecord>(json, BlueskyServer.BlueskyJsonSerializerOptions);

        Assert.NotNull(record);
        Assert.NotEqual(typeof(BlueskyRecord), record.GetType());

        string serialized = JsonSerializer.Serialize(record, record.GetType(), BlueskyServer.BlueskyJsonSerializerOptions);

        using JsonDocument serializedDocument = JsonDocument.Parse(serialized);
        Assert.Equal(expectedDiscriminator, serializedDocument.RootElement.GetProperty("$type").GetString());
    }

    [Fact]
    public void GeneratorRepositoryRecordDeserializesWithSourceGeneratedJsonContext()
    {
        string json = """
            {
                "uri": "at://did:plc:ewvi7nxzyoun6zhxrhs64oiz/app.bsky.feed.generator/test",
                "cid": "bafyreihszwqbe7nxh2mv4o2wk365yanh3p3zimbunfrjdtq7gcmmzohx6i",
                "value": {
                    "$type": "app.bsky.feed.generator",
                    "did": "did:plc:ewvi7nxzyoun6zhxrhs64oiz",
                    "displayName": "Test Generator",
                    "createdAt": "2025-04-21T10:49:31.969Z"
                }
            }
            """;

        AtProtoRepositoryRecord<Generator>? actual =
            JsonSerializer.Deserialize<AtProtoRepositoryRecord<Generator>>(json, BlueskyServer.BlueskyJsonSerializerOptions);

        Assert.NotNull(actual);
        Assert.Equal("at://did:plc:ewvi7nxzyoun6zhxrhs64oiz/app.bsky.feed.generator/test", actual.Uri.ToString());
        Assert.Equal("Test Generator", actual.Value.DisplayName);
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
    [InlineData(GeneratorContentMode.Unspecified, "app.bsky.feed.defs#contentModeUnspecified")]
    [InlineData(GeneratorContentMode.Video, "app.bsky.feed.defs#contentModeVideo")]
    public void KnownGeneratorContentModesRoundTrip(GeneratorContentMode contentMode, string expected)
    {
        string json = JsonSerializer.Serialize(contentMode, BlueskyServer.BlueskyJsonSerializerOptions);

        Assert.Equal($"\"{expected}\"", json);
        Assert.Equal(contentMode, JsonSerializer.Deserialize<GeneratorContentMode>(json, BlueskyServer.BlueskyJsonSerializerOptions));
    }

    [Fact]
    public void SerializingUnknownGeneratorContentModeThrows()
    {
        Assert.Throws<JsonException>(() => JsonSerializer.Serialize(GeneratorContentMode.Unknown, BlueskyServer.BlueskyJsonSerializerOptions));
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
