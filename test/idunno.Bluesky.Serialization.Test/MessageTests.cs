// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json;
using System.Text.Json.Nodes;

using idunno.AtProto.Repo;
using idunno.Bluesky.Chat;

namespace idunno.Bluesky.Serialization.Test
{
    [ExcludeFromCodeCoverage]
    public class MessageTests
    {
        [Fact]
        public void MessageInputWithoutEmbeddedRecordSerializesCorrectly()
        {
            MessageInput messageInput = new("text");

            string actual = JsonSerializer.Serialize(messageInput, options: BlueskyServer.BlueskyJsonSerializerOptions);

            Assert.NotNull(actual);

            JsonNode? jsonNode = JsonNode.Parse(actual);

            Assert.NotNull(jsonNode);

            Assert.NotNull(jsonNode["text"]);
            Assert.Equal("text", jsonNode["text"]!.GetValue<string>());
        }

        [Fact]
        public void MessageInputWithEmbeddedRecordSerializesCorrectly()
        {
            StrongReference strongReference = new (
                "at://did:plc:7xkc5gsqnj33qs3fsa2mewzj/app.bsky.feed.post/3lqvtxtvtfc2q",
                "bafyreihdfqqjhjwctdcf6kxnpk47kr6cc7u2jtxntnb54ibhb43bbbfw54");

            MessageInput messageInput = new(
                "text",
                embed : new Embed.EmbeddedRecord(strongReference));

            string actual = JsonSerializer.Serialize(messageInput, options: BlueskyServer.BlueskyJsonSerializerOptions);

            Assert.NotNull(actual);

            JsonNode? messageInputJsonNode = JsonNode.Parse(actual);

            Assert.NotNull(messageInputJsonNode);

            Assert.NotNull(messageInputJsonNode["text"]);
            Assert.Equal("text", messageInputJsonNode["text"]!.GetValue<string>());

            JsonNode? embed = messageInputJsonNode["embed"];
            Assert.NotNull(embed);
            Assert.NotNull(embed["$type"]);
            Assert.Equal("app.bsky.embed.record", embed["$type"]!.GetValue<string>());

            JsonNode? record = embed["record"];
            Assert.NotNull(record);
            Assert.NotNull(record["uri"]);
            Assert.NotNull(record["cid"]);

            Assert.Equal(strongReference.Uri.ToString(), record["uri"]!.GetValue<string>());
            Assert.Equal(strongReference.Cid.ToString(), record["cid"]!.GetValue<string>());
        }
    }
}
