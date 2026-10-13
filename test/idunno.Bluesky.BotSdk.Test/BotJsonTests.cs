// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json;

using idunno.AtProto.Jetstream;
using idunno.AtProto.Jetstream.Archive;
using idunno.Bluesky;

using Xunit;

namespace idunno.Bluesky.BotSdk.Test;

/// <summary>Checks generated JSON metadata against the persisted formats used before Native AOT.</summary>
public sealed class BotJsonTests
{
    /// <summary>Reads whitespace text and empty facet tags with the upgraded SDK's AOT metadata.</summary>
    [Theory]
    [InlineData(" ")]
    [InlineData("\t")]
    [InlineData("\n")]
    public void ReadsPreviouslyRejectedRecordValues(string text)
    {
        string json = JsonSerializer.Serialize(new
        {
            text,
            createdAt = TestRecords.Start,
            facets = new[]
            {
                new
                {
                    index = new { byteStart = 0, byteEnd = 1 },
                    features = new Dictionary<string, string>[]
                    {
                        new() { ["$type"] = "app.bsky.richtext.facet#tag", ["tag"] = "" }
                    }
                }
            }
        });

        Post post = JsonSerializer.Deserialize(json, BotJson.Post)!;
        Assert.Equal(text, post.Text);
        JsonElement roundtrip = JsonSerializer.SerializeToElement(post, BotJson.Post);
        Assert.Equal("", roundtrip.GetProperty("facets")[0].GetProperty("features")[0].GetProperty("tag").GetString());
    }

    /// <summary>Preserves request bounds, filters, enum wire values and the original database property names.</summary>
    [Fact]
    public void RequestMetadataPreservesStoredFormat()
    {
        SnapshotRequest request = new()
        {
            AfterSeq = 10,
            BeforeSeq = 100,
            Kinds = [JetStreamEventKind.Commit],
            Collections = [new("app.bsky.feed.post")],
            Dids = [new("did:plc:ec72yg6n2sydzjvtovvdlxrk")]
        };
        string original = JsonSerializer.Serialize(request);
        string generated = JsonSerializer.Serialize(request, BotJson.SnapshotRequest);
        Assert.True(JsonElement.DeepEquals(JsonDocument.Parse(original).RootElement, JsonDocument.Parse(generated).RootElement));
        SnapshotRequest restored = JsonSerializer.Deserialize(original, BotJson.SnapshotRequest)!;
        Assert.Equal(request.AfterSeq, restored.AfterSeq);
        Assert.Equal(request.BeforeSeq, restored.BeforeSeq);
        Assert.Equal(request.Kinds, restored.Kinds);
        Assert.Equal(request.Collections, restored.Collections);
        Assert.Equal(request.Dids, restored.Dids);
    }

    /// <summary>Round-trips every checkpoint field without changing the stored JSON format.</summary>
    [Fact]
    public void CheckpointMetadataPreservesAllFields()
    {
        SnapshotCheckpoint checkpoint = new()
        {
            SealedTipSeq = 100,
            PlanAfterSeq = 10,
            ReplayAfterSeq = 8,
            LiveAfterSeq = 12,
            RequestFingerprint = "fingerprint",
            SegmentName = "segment",
            SegmentChecksum = "checksum",
            NextBlockIndex = 4,
            NextByteOffset = 2048
        };
        string original = JsonSerializer.Serialize(checkpoint);
        string generated = JsonSerializer.Serialize(checkpoint, BotJson.SnapshotCheckpoint);
        Assert.True(JsonElement.DeepEquals(JsonDocument.Parse(original).RootElement, JsonDocument.Parse(generated).RootElement));
        Assert.Equal(checkpoint, JsonSerializer.Deserialize(original, BotJson.SnapshotCheckpoint));
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize("{}", BotJson.SnapshotCheckpoint));
    }

    /// <summary>Preserves the refresh-token envelope used by existing encrypted credentials.</summary>
    [Fact]
    public void CredentialMetadataPreservesStoredFormat()
    {
        const string original = """{"Service":"https://bsky.social/","RefreshToken":"test-refresh-token"}""";
        SavedCredentials credentials = JsonSerializer.Deserialize(original, BotJsonContext.Default.SavedCredentials)!;
        Assert.Equal("https://bsky.social/", credentials.Service);
        Assert.Equal("test-refresh-token", credentials.RefreshToken);
        Assert.Equal(original, JsonSerializer.Serialize(credentials, BotJsonContext.Default.SavedCredentials));
        Assert.Equal("[REDACTED]", credentials.ToString());
    }

    /// <summary>Keeps the public health response's camel-case properties, null dependencies and worker dictionary.</summary>
    [Fact]
    public void HealthMetadataPreservesProbeShape()
    {
        DateTimeOffset deadline = DateTimeOffset.Parse("2026-10-08T00:00:00Z", System.Globalization.CultureInfo.InvariantCulture);
        LivenessResponse response = new("healthy", new() { ["reader"] = new("waiting", deadline, null) });
        JsonElement json = JsonSerializer.SerializeToElement(response, BotJsonContext.Default.LivenessResponse);
        Assert.Equal("healthy", json.GetProperty("status").GetString());
        JsonElement reader = json.GetProperty("workers").GetProperty("reader");
        Assert.Equal("waiting", reader.GetProperty("phase").GetString());
        Assert.Equal(deadline, reader.GetProperty("deadline").GetDateTimeOffset());
        Assert.Equal(JsonValueKind.Null, reader.GetProperty("dependency").ValueKind);
        Dictionary<string, string?> dependencies = new() { ["reader"] = null, ["sender"] = "database" };
        Assert.Equal("""{"reader":null,"sender":"database"}""",
            JsonSerializer.Serialize(dependencies, BotJsonContext.Default.DictionaryStringString));
    }

    /// <summary>Uses the SDK's generated post metadata without changing complete record comparison.</summary>
    [Fact]
    public void PostMetadataPreservesSdkRecord()
    {
        Post reply = new("Sample reply", createdAt: DateTimeOffset.UtcNow);
        string original = JsonSerializer.Serialize(reply, BlueskyJsonSerializerOptions.Options);
        Assert.Equal(original, JsonSerializer.Serialize(reply, BotJson.Post));
        Assert.True(ReplyRecords.SameRecord(reply, JsonSerializer.Deserialize(original, BotJson.Post)!));
    }
}
