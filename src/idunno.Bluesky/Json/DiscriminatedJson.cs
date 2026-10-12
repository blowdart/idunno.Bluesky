// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Buffers;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace idunno.Bluesky.Json;

internal static class DiscriminatedJson
{
    internal static object? Deserialize(JsonElement element, JsonTypeInfo typeInfo)
    {
        ArrayBufferWriter<byte> buffer = new();
        using (Utf8JsonWriter writer = new(buffer))
        {
            writer.WriteStartObject();
            foreach (JsonProperty property in element.EnumerateObject().Where(property => !property.NameEquals("$type")))
            {
                property.WriteTo(writer);
            }
            writer.WriteEndObject();
        }

        return JsonSerializer.Deserialize(buffer.WrittenSpan, typeInfo);
    }

    internal static void Serialize(Utf8JsonWriter writer, string discriminator, object value, JsonTypeInfo typeInfo)
    {
        byte[] json = JsonSerializer.SerializeToUtf8Bytes(value, typeInfo);
        using JsonDocument document = JsonDocument.Parse(json);

        writer.WriteStartObject();
        writer.WriteString("$type", discriminator);
        foreach (JsonProperty property in document.RootElement.EnumerateObject().Where(property => !property.NameEquals("$type")))
        {
            property.WriteTo(writer);
        }
        writer.WriteEndObject();
    }
}
