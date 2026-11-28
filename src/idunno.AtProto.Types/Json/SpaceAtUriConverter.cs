// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace idunno.AtProto.Json;

/// <summary>
/// Converts a space URI to or from JSON.
/// </summary>
[Experimental("BSKYSpaces", UrlFormat = "https://bluesky.idunno.dev/docs/spaces.html")]
public sealed class SpaceAtUriConverter : JsonConverter<SpaceAtUri>
{
    /// <summary>
    /// Reads and converts JSON to a <see cref="SpaceAtUri"/>.
    /// </summary>
    /// <param name="reader">The reader.</param>
    /// <param name="typeToConvert">The type to convert.</param>
    /// <param name="options">An object that specifies serialization options to use.</param>
    /// <returns>A <see cref="SpaceAtUri"/> created from the JSON.</returns>
    /// <exception cref="JsonException">
    /// Thrown when the JSON to be converted is not a string token, is not a valid AT URI, or is a public AT URI rather than a space URI.
    /// </exception>
    public override SpaceAtUri? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.String)
        {
            throw new JsonException();
        }

        SpaceAtUri? spaceUri;

        try
        {
            spaceUri = new SpaceAtUri(reader.GetString()!);
        }
        catch (ArgumentException e)
        {
            throw new JsonException("Value cannot be null or an empty string.", e);
        }
        catch (AtUriFormatException e)
        {
            throw new JsonException("Invalid space URI format.", e);
        }

        return spaceUri;
    }

    /// <summary>
    /// Writes the specified <see cref="SpaceAtUri"/> as JSON.
    /// </summary>
    /// <param name="writer">The writer to write to.</param>
    /// <param name="value">The <see cref="SpaceAtUri"/> to convert to JSON.</param>
    /// <param name="options">An object that specifies serialization options to use.</param>
    /// <exception cref="ArgumentNullException">Throws when <paramref name="writer"/> or <paramref name="value"/> is <see langword="null"/>.</exception>
    public override void Write(Utf8JsonWriter writer, SpaceAtUri value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentNullException.ThrowIfNull(value);

        writer.WriteStringValue(value.ToString());
    }
}
