// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json;
using System.Text.Json.Serialization;

namespace idunno.AtProto.Json;

/// <summary>
/// Converts a Bytes instance to or from JSON.
/// </summary>
public sealed class ByteConverter : JsonConverter<Bytes>
{
    private static readonly string s_propertyName = JsonEncodedText.Encode("$bytes").ToString();

    /// <summary>
    /// Reads and converts JSON to an <see cref="Bytes"/>.
    /// </summary>
    /// <param name="reader">The reader.</param>
    /// <param name="typeToConvert">The type to convert.</param>
    /// <param name="options">An object that specifies serialization options to use.</param>
    /// <returns>A <see cref="Bytes"/> instance created from the JSON.</returns>
    /// <exception cref="JsonException">Thrown when the JSON to be converted is not the correct shape for a serialized <see cref="Bytes"/>.</exception>
    public override Bytes? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartObject)
        {
            throw new JsonException();
        }
        reader.Read();

        if (reader.TokenType != JsonTokenType.PropertyName || reader.GetString() != s_propertyName)
        {
            throw new JsonException();
        }
        reader.Read();

        if (reader.TokenType != JsonTokenType.String)
        {
            throw new JsonException();
        }

        Bytes bytes;
        try
        {
            bytes = new Bytes(reader.GetString()!);
        }
        catch (ArgumentException e)
        {
            throw new JsonException("Value cannot be null or an empty string.", e);
        }
        catch (FormatException e)
        {
            throw new JsonException("Invalid Base64 format.", e);
        }
        reader.Read();

        if (reader.TokenType != JsonTokenType.EndObject)
        {
            throw new JsonException();
        }

        return bytes;
    }

    /// <summary>
    /// Writes the specified <see cref="Bytes"></see> as JSON.
    /// </summary>
    /// <param name="writer">The writer to write to.</param>
    /// <param name="value">The <see cref="Bytes"/> to convert to JSON.</param>
    /// <param name="options">An object that specifies serialization options to use.</param>
    /// <exception cref="ArgumentNullException">Throws when <paramref name="writer"/> or <paramref name="value"/> is <see langword="null"/>.</exception>
    public override void Write(Utf8JsonWriter writer, Bytes value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentNullException.ThrowIfNull(value);

        writer.WriteStartObject();
        writer.WritePropertyName(s_propertyName);
        writer.WriteStringValue(value.Base64EncodedValue);
        writer.WriteEndObject();
    }
}
