// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json;
using System.Text.Json.Serialization;

using idunno.Bluesky.RichText;

namespace idunno.Bluesky.Json;

internal sealed class FacetFeatureConverter : JsonConverter<FacetFeature>
{
    public override FacetFeature? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using JsonDocument document = JsonDocument.ParseValue(ref reader);
        JsonElement element = document.RootElement;

        if (element.ValueKind != JsonValueKind.Object ||
            !element.TryGetProperty("$type", out JsonElement discriminator) ||
            discriminator.ValueKind != JsonValueKind.String)
        {
            throw new JsonException("A facet feature must be an object with a string $type discriminator.");
        }

        Type? type = discriminator.GetString() switch
        {
            "app.bsky.richtext.facet#mention" => typeof(MentionFacetFeature),
            "app.bsky.richtext.facet#link" => typeof(LinkFacetFeature),
            "app.bsky.richtext.facet#tag" => typeof(TagFacetFeature),
            _ => null
        };

        if (type is null)
        {
            return new FacetFeature
            {
                ExtensionData = element.EnumerateObject().ToDictionary(property => property.Name, property => property.Value.Clone())
            };
        }

        return (FacetFeature?)DiscriminatedJson.Deserialize(element, options.GetTypeInfo(type));
    }

    public override void Write(Utf8JsonWriter writer, FacetFeature value, JsonSerializerOptions options)
    {
        string? discriminator = value switch
        {
            MentionFacetFeature => "app.bsky.richtext.facet#mention",
            LinkFacetFeature => "app.bsky.richtext.facet#link",
            TagFacetFeature => "app.bsky.richtext.facet#tag",
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
                throw new JsonException("An unknown facet feature must retain its string $type discriminator.");
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
