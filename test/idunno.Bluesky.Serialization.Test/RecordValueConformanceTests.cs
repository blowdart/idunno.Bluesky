// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json;

using idunno.Bluesky.Embed;
using idunno.Bluesky.Feed;
using idunno.Bluesky.Record;

namespace idunno.Bluesky.Serialization.Test;

public class RecordValueConformanceTests
{
    private const string Author = """
        { "did": "did:plc:hfgp6pj3akhqxntgqwramlbg", "handle": "blowdart.me" }
        """;

    private static string PostViewJson(string record, string extra = "") => $$"""
        {
            "uri": "at://did:plc:hfgp6pj3akhqxntgqwramlbg/app.bsky.feed.post/3l66cdbste424",
            "cid": "bafyreibtb55kfqsny3qk4c2puoqom4cu5zkpnp5xdwu5dud264b4efocyq",
            "author": {{Author}},
            "record": {{record}},
            "indexedAt": "2024-10-10T16:43:37.262Z"{{extra}}
        }
        """;

    private static string ViewRecordJson(string value, string extra = "") => $$"""
        {
            "uri": "at://did:plc:hfgp6pj3akhqxntgqwramlbg/app.bsky.feed.post/3l66cdbste424",
            "cid": "bafyreibtb55kfqsny3qk4c2puoqom4cu5zkpnp5xdwu5dud264b4efocyq",
            "author": {{Author}},
            "value": {{value}},
            "indexedAt": "2024-10-10T16:43:37.262Z"{{extra}}
        }
        """;

    private const string UnknownRecord = """
        { "$type": "com.example.unknown", "createdAt": "2024-10-10T16:43:37.262Z", "answer": 42 }
        """;

    private const string PostRecord = """
        { "$type": "app.bsky.feed.post", "createdAt": "2024-10-10T16:43:37.262Z", "text": "Hello" }
        """;

    [Fact]
    public void ViewRecordWithAnUnknownRecordTypePreservesTheRecordAndItsType()
    {
        ViewRecord? viewRecord = JsonSerializer.Deserialize<ViewRecord>(ViewRecordJson(UnknownRecord), BlueskyJsonSerializerOptions.Options);

        Assert.NotNull(viewRecord);
        UnknownBlueskyRecord unknown = Assert.IsType<UnknownBlueskyRecord>(viewRecord.Value);
        Assert.Equal("com.example.unknown", unknown.RecordType);
        Assert.NotNull(unknown.ExtensionData);
        Assert.Equal(42, unknown.ExtensionData["answer"].GetInt32());

        string serialized = JsonSerializer.Serialize(viewRecord, BlueskyJsonSerializerOptions.Options);
        using JsonDocument document = JsonDocument.Parse(serialized);
        JsonElement value = document.RootElement.GetProperty("value");
        Assert.Equal("com.example.unknown", value.GetProperty("$type").GetString());
        Assert.Equal(42, value.GetProperty("answer").GetInt32());
        Assert.Equal(1, value.EnumerateObject().Count(p => p.Name == "$type"));
    }

    [Fact]
    public void ViewRecordWithAKnownRecordTypeDispatchesToTheTypedRecord()
    {
        ViewRecord? viewRecord = JsonSerializer.Deserialize<ViewRecord>(ViewRecordJson(PostRecord), BlueskyJsonSerializerOptions.Options);

        Assert.NotNull(viewRecord);
        Post post = Assert.IsType<Post>(viewRecord.Value);
        Assert.Equal("Hello", post.Text);
    }

    [Fact]
    public void ViewRecordWithoutARecordTypeDeserializesAsABaseRecord()
    {
        ViewRecord? viewRecord = JsonSerializer.Deserialize<ViewRecord>(
            ViewRecordJson("""{ "createdAt": "2024-10-10T16:43:37.262Z", "answer": 42 }"""),
            BlueskyJsonSerializerOptions.Options);

        Assert.NotNull(viewRecord);
        Assert.IsType<BlueskyRecord>(viewRecord.Value);
    }

    [Fact]
    public void ViewRecordDiscardsNullLabelsAndEmbeds()
    {
        ViewRecord? viewRecord = JsonSerializer.Deserialize<ViewRecord>(
            ViewRecordJson(PostRecord, """, "labels": [ null ], "embeds": [ null ]"""),
            BlueskyJsonSerializerOptions.Options);

        Assert.NotNull(viewRecord);
        Assert.Empty(viewRecord.Labels!);
        Assert.Empty(viewRecord.Embeds!);
    }

    [Theory]
    [InlineData(UnknownRecord)]
    [InlineData("""{ "createdAt": "2024-10-10T16:43:37.262Z", "answer": 42 }""")]
    public void PostViewWithARecordThatIsNotAPostDoesNotThrow(string record)
    {
        PostView? postView = JsonSerializer.Deserialize<PostView>(PostViewJson(record), BlueskyJsonSerializerOptions.Options);

        Assert.NotNull(postView);
        Assert.IsType<BlueskyRecord>(postView.RecordValue);
        Assert.Equal(42, postView.RecordValue.ExtensionData!["answer"].GetInt32());
        Assert.False(postView.TryGetPost(out Post? post));
        Assert.Null(post);
        Assert.Throws<InvalidOperationException>(() => postView.Record);
    }

    [Fact]
    public void PostViewWithAPostRecordExposesThePost()
    {
        PostView? postView = JsonSerializer.Deserialize<PostView>(PostViewJson(PostRecord), BlueskyJsonSerializerOptions.Options);

        Assert.NotNull(postView);
        Assert.True(postView.TryGetPost(out Post? post));
        Assert.Equal("Hello", post.Text);
        Assert.Same(post, postView.Record);
    }

    [Fact]
    public void PostViewReadsViewCountAndDiscardsNullLabels()
    {
        PostView? postView = JsonSerializer.Deserialize<PostView>(
            PostViewJson(PostRecord, """, "viewCount": 1234, "labels": [ null ]"""),
            BlueskyJsonSerializerOptions.Options);

        Assert.NotNull(postView);
        Assert.Equal(1234, postView.ViewCount);
        Assert.Empty(postView.Labels);
    }
}
