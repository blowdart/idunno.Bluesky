// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json;
using System.Text.Json.Serialization;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace idunno.Bluesky.Feed;
#pragma warning restore IDE0130 // Namespace does not match folder structure

/// <summary>
/// Converts an <see cref="InteractionEvent"/> to and from its lexicon token, mapping unrecognized tokens to <see cref="InteractionEvent.Unknown"/>.
/// </summary>
internal sealed class InteractionEventConverter : JsonConverter<InteractionEvent>
{
    /// <inheritdoc/>
    public override InteractionEvent Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.String)
        {
            throw new JsonException($"Expected a string when reading an {nameof(InteractionEvent)}, found {reader.TokenType}.");
        }

        return reader.GetString() switch
        {
            "app.bsky.feed.defs#requestLess" => InteractionEvent.RequestLess,
            "app.bsky.feed.defs#requestMore" => InteractionEvent.RequestMore,
            "app.bsky.feed.defs#clickthroughItem" => InteractionEvent.ClickthroughItem,
            "app.bsky.feed.defs#clickthroughAuthor" => InteractionEvent.ClickthroughAuthor,
            "app.bsky.feed.defs#clickthroughReposter" => InteractionEvent.ClickthroughReposter,
            "app.bsky.feed.defs#clickthroughEmbed" => InteractionEvent.ClickthroughEmbed,
            "app.bsky.feed.defs#interactionSeen" => InteractionEvent.InteractionSeen,
            "app.bsky.feed.defs#interactionLike" => InteractionEvent.InteractionLike,
            "app.bsky.feed.defs#interactionRepost" => InteractionEvent.InteractionRepost,
            "app.bsky.feed.defs#interactionReply" => InteractionEvent.InteractionReply,
            "app.bsky.feed.defs#interactionQuote" => InteractionEvent.InteractionQuote,
            "app.bsky.feed.defs#interactionShare" => InteractionEvent.InteractionShare,
            "app.bsky.feed.defs#interactionFollow" => InteractionEvent.InteractionFollow,
            "app.bsky.feed.defs#interactionUnspecced" => InteractionEvent.InteractionUnspecced,
            _ => InteractionEvent.Unknown
        };
    }

    /// <inheritdoc/>
    public override void Write(Utf8JsonWriter writer, InteractionEvent value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);

        string token = value switch
        {
            InteractionEvent.RequestLess => "app.bsky.feed.defs#requestLess",
            InteractionEvent.RequestMore => "app.bsky.feed.defs#requestMore",
            InteractionEvent.ClickthroughItem => "app.bsky.feed.defs#clickthroughItem",
            InteractionEvent.ClickthroughAuthor => "app.bsky.feed.defs#clickthroughAuthor",
            InteractionEvent.ClickthroughReposter => "app.bsky.feed.defs#clickthroughReposter",
            InteractionEvent.ClickthroughEmbed => "app.bsky.feed.defs#clickthroughEmbed",
            InteractionEvent.InteractionSeen => "app.bsky.feed.defs#interactionSeen",
            InteractionEvent.InteractionLike => "app.bsky.feed.defs#interactionLike",
            InteractionEvent.InteractionRepost => "app.bsky.feed.defs#interactionRepost",
            InteractionEvent.InteractionReply => "app.bsky.feed.defs#interactionReply",
            InteractionEvent.InteractionQuote => "app.bsky.feed.defs#interactionQuote",
            InteractionEvent.InteractionShare => "app.bsky.feed.defs#interactionShare",
            InteractionEvent.InteractionFollow => "app.bsky.feed.defs#interactionFollow",
            InteractionEvent.InteractionUnspecced => "app.bsky.feed.defs#interactionUnspecced",
            _ => throw new JsonException(
                $"{nameof(InteractionEvent)}.{value} cannot be serialized. {nameof(InteractionEvent.Unknown)} only exists to carry a value this library does not recognize.")
        };

        writer.WriteStringValue(token);
    }
}
