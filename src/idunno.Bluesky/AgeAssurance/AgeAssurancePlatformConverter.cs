// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json;
using System.Text.Json.Serialization;

namespace idunno.Bluesky.AgeAssurance;

internal sealed class AgeAssurancePlatformConverter : JsonConverter<AgeAssurancePlatform>
{
    public override AgeAssurancePlatform Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType is not JsonTokenType.String)
        {
            throw new JsonException($"Expected a string when reading an {nameof(AgeAssurancePlatform)}, found {reader.TokenType}.");
        }

        return reader.GetString() switch
        {
            "web" => AgeAssurancePlatform.Web,
            "ios" => AgeAssurancePlatform.Ios,
            "android" => AgeAssurancePlatform.Android,
            _ => AgeAssurancePlatform.Unknown
        };
    }

    public override void Write(Utf8JsonWriter writer, AgeAssurancePlatform value, JsonSerializerOptions options)
    {
        string? serialized = value switch
        {
            AgeAssurancePlatform.Web => "web",
            AgeAssurancePlatform.Ios => "ios",
            AgeAssurancePlatform.Android => "android",
            _ => null
        };

        if (serialized is null)
        {
            throw new JsonException($"The {nameof(AgeAssurancePlatform)} value '{value}' is not supported.");
        }

        writer.WriteStringValue(serialized);
    }
}
