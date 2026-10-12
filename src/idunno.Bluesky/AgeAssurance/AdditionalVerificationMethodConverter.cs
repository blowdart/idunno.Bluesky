// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json;
using System.Text.Json.Serialization;

namespace idunno.Bluesky.AgeAssurance;

internal sealed class AdditionalVerificationMethodConverter : JsonConverter<AdditionalVerificationMethod>
{
    public override AdditionalVerificationMethod Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType is not JsonTokenType.String)
        {
            throw new JsonException($"Expected a string when reading an {nameof(AdditionalVerificationMethod)}, found {reader.TokenType}.");
        }

        return reader.GetString() switch
        {
            "device" => AdditionalVerificationMethod.Device,
            _ => AdditionalVerificationMethod.Unknown
        };
    }

    public override void Write(Utf8JsonWriter writer, AdditionalVerificationMethod value, JsonSerializerOptions options)
    {
        if (value is not AdditionalVerificationMethod.Device)
        {
            throw new JsonException($"The {nameof(AdditionalVerificationMethod)} value '{value}' is not supported.");
        }

        writer.WriteStringValue("device");
    }
}
