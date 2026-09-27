// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

using idunno.Bluesky;
using idunno.Bluesky.Feed;

namespace idunno.Bluesky.Serialization.Test;

[ExcludeFromCodeCoverage]
public class JetstreamRecordTests
{
    [Fact]
    public void LikeDeserializesFromJetstreamRecordWithSourceGeneratedOptions()
    {
        using JsonDocument document = JsonDocument.Parse(
            """
            {
              "$type": "app.bsky.feed.like",
              "createdAt": "2026-09-27T08:00:00Z",
              "subject": {
                "uri": "at://did:plc:g6ylltenitt4tp27bpwalh7b/app.bsky.feed.post/3abc",
                "cid": "bafyreif5vwyd7sgxydtaomspxdbuniyz5gjk2sublkl2xuegchmlzfjydq"
              }
            }
            """);

        JsonSerializerOptions options = BlueskyJsonSerializerOptions.Options;
        JsonTypeInfo<Like> typeInfo = (JsonTypeInfo<Like>)options.GetTypeInfo(typeof(Like));
        Like? like = JsonSerializer.Deserialize(document.RootElement, typeInfo);

        Assert.NotNull(like);
        Assert.Equal("at://did:plc:g6ylltenitt4tp27bpwalh7b/app.bsky.feed.post/3abc", like.Subject.Uri.ToString());
        Assert.Equal("bafyreif5vwyd7sgxydtaomspxdbuniyz5gjk2sublkl2xuegchmlzfjydq", like.Subject.Cid.ToString());
        Assert.Equal(DateTimeOffset.Parse("2026-09-27T08:00:00Z"), like.CreatedAt);
    }
}
