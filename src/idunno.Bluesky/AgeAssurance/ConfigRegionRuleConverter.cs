// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json;
using System.Text.Json.Serialization;

namespace idunno.Bluesky.AgeAssurance;

internal sealed class ConfigRegionRuleConverter : JsonConverter<ConfigRegionRule>
{
    private static readonly Dictionary<string, Type> s_types = new()
    {
        ["app.bsky.ageassurance.defs#configRegionRuleDefault"] = typeof(ConfigRegionRuleDefault),
        ["app.bsky.ageassurance.defs#configRegionRuleIfDeclaredOverAge"] = typeof(ConfigRegionRuleIfDeclaredOverAge),
        ["app.bsky.ageassurance.defs#configRegionRuleIfDeclaredUnderAge"] = typeof(ConfigRegionRuleIfDeclaredUnderAge),
        ["app.bsky.ageassurance.defs#configRegionRuleIfAssuredOverAge"] = typeof(ConfigRegionRuleIfAssuredOverAge),
        ["app.bsky.ageassurance.defs#configRegionRuleIfAssuredUnderAge"] = typeof(ConfigRegionRuleIfAssuredUnderAge),
        ["app.bsky.ageassurance.defs#configRegionRuleIfAccountNewerThan"] = typeof(ConfigRegionRuleIfAccountNewerThan),
        ["app.bsky.ageassurance.defs#configRegionRuleIfAccountOlderThan"] = typeof(ConfigRegionRuleIfAccountOlderThan)
    };

    private static readonly Dictionary<Type, string> s_discriminators = s_types.ToDictionary(entry => entry.Value, entry => entry.Key);

    public override bool CanConvert(Type typeToConvert) => typeToConvert == typeof(ConfigRegionRule);

    public override ConfigRegionRule? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using JsonDocument document = JsonDocument.ParseValue(ref reader);
        JsonElement element = document.RootElement;
        if (element.ValueKind != JsonValueKind.Object)
        {
            throw new JsonException("Expected an age-assurance rule object.");
        }

        if (element.TryGetProperty("$type", out JsonElement discriminator) &&
            discriminator.ValueKind == JsonValueKind.String &&
            s_types.TryGetValue(discriminator.GetString()!, out Type? type))
        {
            return (ConfigRegionRule?)JsonSerializer.Deserialize(element, options.GetTypeInfo(type));
        }

        return new ConfigRegionRule
        {
            ExtensionData = element.EnumerateObject().ToDictionary(property => property.Name, property => property.Value.Clone())
        };
    }

    public override void Write(Utf8JsonWriter writer, ConfigRegionRule value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        if (s_discriminators.TryGetValue(value.GetType(), out string? discriminator))
        {
            writer.WriteString("$type", discriminator);
            JsonElement element = JsonSerializer.SerializeToElement(value, options.GetTypeInfo(value.GetType()));
            foreach (JsonProperty property in element.EnumerateObject().Where(property => !property.NameEquals("$type")))
            {
                property.WriteTo(writer);
            }
        }
        else if (value.ExtensionData is not null)
        {
            foreach (KeyValuePair<string, JsonElement> property in value.ExtensionData)
            {
                writer.WritePropertyName(property.Key);
                property.Value.WriteTo(writer);
            }
        }

        writer.WriteEndObject();
    }
}
