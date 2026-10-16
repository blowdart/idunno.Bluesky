// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json;
using System.Text.Json.Serialization;

using idunno.Bluesky.Chat;

namespace idunno.Bluesky.Json;

internal sealed class MessageViewBaseConverter : JsonConverter<MessageViewBase>
{
    public override MessageViewBase? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using JsonDocument document = JsonDocument.ParseValue(ref reader);
        JsonElement element = document.RootElement;
        if (element.ValueKind != JsonValueKind.Object ||
            !element.TryGetProperty("$type", out JsonElement discriminator) ||
            discriminator.ValueKind != JsonValueKind.String)
        {
            throw new JsonException("A message view must have a string $type discriminator.");
        }

        Type? type = discriminator.GetString() switch
        {
            "chat.bsky.convo.defs#messageView" => typeof(MessageView),
            "chat.bsky.convo.defs#deletedMessageView" => typeof(DeletedMessageView),
            "chat.bsky.convo.defs#systemMessageView" => typeof(SystemMessageView),
            "chat.bsky.convo.defs#messageBeforeUserJoinedGroupView" => typeof(MessageBeforeUserJoinedGroupView),
            _ => null
        };

        if (type is null)
        {
            return new MessageViewBase
            {
                ExtensionData = element.EnumerateObject().ToDictionary(property => property.Name, property => property.Value.Clone())
            };
        }

        return (MessageViewBase?)DiscriminatedJson.Deserialize(element, options.GetTypeInfo(type));
    }

    public override void Write(Utf8JsonWriter writer, MessageViewBase value, JsonSerializerOptions options)
    {
        string? discriminator = value switch
        {
            MessageView => "chat.bsky.convo.defs#messageView",
            DeletedMessageView => "chat.bsky.convo.defs#deletedMessageView",
            SystemMessageView => "chat.bsky.convo.defs#systemMessageView",
            MessageBeforeUserJoinedGroupView => "chat.bsky.convo.defs#messageBeforeUserJoinedGroupView",
            _ => null
        };

        if (discriminator is not null)
        {
            DiscriminatedJson.Serialize(writer, discriminator, value, options.GetTypeInfo(value.GetType()));
        }
        else
        {
            if (value.ExtensionData is null ||
                !value.ExtensionData.TryGetValue("$type", out JsonElement type) ||
                type.ValueKind != JsonValueKind.String)
            {
                throw new JsonException("An unknown message view must retain its string $type discriminator.");
            }

            writer.WriteStartObject();
            foreach (KeyValuePair<string, JsonElement> property in value.ExtensionData)
            {
                writer.WritePropertyName(property.Key);
                property.Value.WriteTo(writer);
            }
            writer.WriteEndObject();
        }
    }
}
