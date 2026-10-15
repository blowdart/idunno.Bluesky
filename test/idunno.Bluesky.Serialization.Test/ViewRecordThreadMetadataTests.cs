// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json;

using idunno.Bluesky.Embed;

namespace idunno.Bluesky.Serialization.Test;

[ExcludeFromCodeCoverage]
public class ViewRecordThreadMetadataTests
{
    [Theory]
    [InlineData("", null, null)]
    [InlineData(",\"opThreadPostIndex\":2,\"opThreadPostCount\":4", 2, 4)]
    [InlineData(",\"opThreadPostIndex\":1", 1, null)]
    [InlineData(",\"opThreadPostCount\":4", null, 4)]
    [InlineData(",\"opThreadPostIndex\":0,\"opThreadPostCount\":-1", 0, -1)]
    public void SyntheticEmbeddedRecordPreservesOptionalThreadMetadata(string metadata, int? index, int? count)
    {
        string json = $$"""
            {
                "$type":"app.bsky.embed.record#view",
                "record":{
                    "$type":"app.bsky.embed.record#viewRecord",
                    "uri":"at://did:plc:test/app.bsky.feed.post/test",
                    "cid":"bafyreiaxqkvwjv5y45xq7huunf6mjvh7tf6ls44imkgjkqi3uthn57vjxu",
                    "author":{"did":"did:plc:test","handle":"test.invalid"},
                    "value":{
                        "$type":"app.bsky.feed.post",
                        "text":"Synthetic embedded record, not a live response.",
                        "createdAt":"2026-01-01T00:00:00Z"
                    },
                    "indexedAt":"2026-01-01T00:00:00Z"
                    {{metadata}}
                }
            }
            """;

        Assert.NotNull(SourceGenerationContext.Default.GetTypeInfo(typeof(ViewRecord)));
        Assert.NotNull(BlueskyServer.BlueskyJsonSerializerOptions.GetTypeInfo(typeof(ViewRecord)));
        EmbeddedView? embedded = JsonSerializer.Deserialize<EmbeddedView>(json, BlueskyServer.BlueskyJsonSerializerOptions);
        ViewRecord record = Assert.IsType<ViewRecord>(Assert.IsType<EmbeddedRecordView>(embedded).Record);

        Assert.Equal(index, record.OpThreadPostIndex);
        Assert.Equal(count, record.OpThreadPostCount);

        string serialized = JsonSerializer.Serialize<EmbeddedView>(embedded, BlueskyServer.BlueskyJsonSerializerOptions);
        EmbeddedView? roundTripped = JsonSerializer.Deserialize<EmbeddedView>(serialized, BlueskyServer.BlueskyJsonSerializerOptions);
        ViewRecord roundTrippedRecord = Assert.IsType<ViewRecord>(Assert.IsType<EmbeddedRecordView>(roundTripped).Record);

        Assert.Equal(index, roundTrippedRecord.OpThreadPostIndex);
        Assert.Equal(count, roundTrippedRecord.OpThreadPostCount);
    }
}
