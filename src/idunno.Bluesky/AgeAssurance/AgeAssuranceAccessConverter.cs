// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json;
using System.Text.Json.Serialization;

namespace idunno.Bluesky.AgeAssurance;

internal sealed class AgeAssuranceAccessConverter : JsonConverter<AgeAssuranceAccess>
{
    public override AgeAssuranceAccess Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType is not JsonTokenType.String)
        {
            throw new JsonException($"Expected a string when reading an {nameof(AgeAssuranceAccess)}, found {reader.TokenType}.");
        }

        return reader.GetString() switch
        {
            "unknown" => AgeAssuranceAccess.Unknown,
            "none" => AgeAssuranceAccess.None,
            "safe" => AgeAssuranceAccess.Safe,
            "full" => AgeAssuranceAccess.Full,
            _ => AgeAssuranceAccess.Unknown
        };
    }

    public override void Write(Utf8JsonWriter writer, AgeAssuranceAccess value, JsonSerializerOptions options)
    {
        string? serialized = value switch
        {
            AgeAssuranceAccess.Unknown => "unknown",
            AgeAssuranceAccess.None => "none",
            AgeAssuranceAccess.Safe => "safe",
            AgeAssuranceAccess.Full => "full",
            _ => null
        };

        if (serialized is null)
        {
            throw new JsonException($"The {nameof(AgeAssuranceAccess)} value '{value}' is not supported.");
        }

        writer.WriteStringValue(serialized);
    }
}
