// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json;

using idunno.AtProto;
using idunno.Bluesky.Feed;

namespace idunno.Bluesky.Serialization.Test;

[ExcludeFromCodeCoverage]
public class FeedInteractionSerializationTests
{
    [Fact]
    public void InteractionRoundTripsWithTheLexiconPropertyNames()
    {
        string json = """
        {
            "item": "at://did:plc:hfgp6pj3akhqxntgqwramlbg/app.bsky.feed.post/3lcf6ry7xy22x",
            "event": "app.bsky.feed.defs#interactionQuote",
            "viewContext": "feed",
            "payload": { "position": 3 },
            "feedContext": "ctx-1",
            "reqId": "request-1"
        }
        """;

        Interaction? interaction = JsonSerializer.Deserialize<Interaction>(json, BlueskyServer.BlueskyJsonSerializerOptions);

        Assert.NotNull(interaction);
        Assert.Equal(new AtUri("at://did:plc:hfgp6pj3akhqxntgqwramlbg/app.bsky.feed.post/3lcf6ry7xy22x"), interaction.Item);
        Assert.Equal(InteractionEvent.InteractionQuote, interaction.Event);
        Assert.Equal(InteractionViewContext.Feed, interaction.ViewContext);
        Assert.Equal(3, interaction.Payload!.Value.GetProperty("position").GetInt32());
        Assert.Equal("ctx-1", interaction.FeedContext);
        Assert.Equal("request-1", interaction.RequestId);

        using JsonDocument serialized = JsonDocument.Parse(JsonSerializer.Serialize(interaction, BlueskyServer.BlueskyJsonSerializerOptions));
        Assert.Equal("request-1", serialized.RootElement.GetProperty("reqId").GetString());
        Assert.Equal("app.bsky.feed.defs#interactionQuote", serialized.RootElement.GetProperty("event").GetString());
        Assert.Equal("feed", serialized.RootElement.GetProperty("viewContext").GetString());
    }

    [Fact]
    public void InteractionMapsUnrecognisedEventAndViewContextValuesToUnknown()
    {
        string json = """{ "event": "app.bsky.feed.defs#interactionFuture", "viewContext": "future" }""";

        Interaction? interaction = JsonSerializer.Deserialize<Interaction>(json, BlueskyServer.BlueskyJsonSerializerOptions);

        Assert.NotNull(interaction);
        Assert.Equal(InteractionEvent.Unknown, interaction.Event);
        Assert.Equal(InteractionViewContext.Unknown, interaction.ViewContext);
    }
}
