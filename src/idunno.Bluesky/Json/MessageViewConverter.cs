// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json;
using System.Text.Json.Serialization;

using idunno.Bluesky.Chat;
using idunno.Bluesky.Embed;
using idunno.Bluesky.RichText;

namespace idunno.Bluesky.Json;

internal sealed class MessageViewConverter : JsonConverter<MessageView>
{
    public override MessageView? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        MessageViewData? data = (MessageViewData?)JsonSerializer.Deserialize(ref reader, options.GetTypeInfo(typeof(MessageViewData)));
        if (data is null)
        {
            return null;
        }

        return new MessageView(data.Id, data.Revision, data.Text, data.Facets, null, data.Reactions, data.Sender, data.SentAt)
        {
            EmbeddedView = data.EmbeddedView,
            ExtensionData = data.ExtensionData
        };
    }

    public override void Write(Utf8JsonWriter writer, MessageView value, JsonSerializerOptions options)
    {
        MessageViewData data = new(value.Id, value.Revision, value.Text, value.Facets, value.EmbeddedView, value.Reactions, value.Sender, value.SentAt);
        if (value.ExtensionData is not null)
        {
            foreach (KeyValuePair<string, JsonElement> property in value.ExtensionData)
            {
                data.ExtensionData.Add(property.Key, property.Value);
            }
        }

        JsonSerializer.Serialize(writer, data, options.GetTypeInfo(typeof(MessageViewData)));
    }

    internal sealed record MessageViewData(
        [property: JsonRequired] string Id,
        [property: JsonRequired, JsonPropertyName("rev")] string Revision,
        [property: JsonRequired] string Text,
        IReadOnlyCollection<Facet>? Facets,
        [property: JsonPropertyName("embed")] EmbeddedView? EmbeddedView,
        IReadOnlyCollection<ReactionView>? Reactions,
        [property: JsonRequired] MessageViewSender Sender,
        [property: JsonRequired] DateTimeOffset SentAt)
    {
        [JsonExtensionData]
        public Dictionary<string, JsonElement> ExtensionData { get; set; } = [];
    }
}
