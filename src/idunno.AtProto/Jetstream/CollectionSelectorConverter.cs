// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json;
using System.Text.Json.Serialization;

namespace idunno.AtProto.Jetstream;

/// <summary>
/// Converts a <see cref="CollectionSelector"/> to or from JSON.
/// </summary>
public sealed class CollectionSelectorConverter : JsonConverter<CollectionSelector>
{
    /// <summary>
    /// Reads and converts JSON to a <see cref="CollectionSelector"/>.
    /// </summary>
    /// <param name="reader">The reader.</param>
    /// <param name="typeToConvert">The type to convert.</param>
    /// <param name="options">An object that specifies serialization options to use.</param>
    /// <returns>A <see cref="CollectionSelector"/> created from the JSON.</returns>
    /// <exception cref="JsonException">Thrown when the JSON to be converted is not a string token, or is not a valid collection filter.</exception>
    public override CollectionSelector? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.String)
        {
            throw new JsonException();
        }

        try
        {
            return new CollectionSelector(reader.GetString()!);
        }
        catch (ArgumentException e)
        {
            throw new JsonException("Value cannot be null or empty.", e);
        }
        catch (NsidFormatException e)
        {
            throw new JsonException("Value is not a valid collection or collection wildcard.", e);
        }
    }

    /// <summary>
    /// Writes the specified <see cref="CollectionSelector"/> as JSON.
    /// </summary>
    /// <param name="writer">The writer to write to.</param>
    /// <param name="value">The <see cref="CollectionSelector"/> to convert to JSON.</param>
    /// <param name="options">An object that specifies serialization options to use.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="writer"/> or <paramref name="value"/> is <see langword="null"/>.</exception>
    public override void Write(Utf8JsonWriter writer, CollectionSelector value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentNullException.ThrowIfNull(value);

        writer.WriteStringValue(value.ToString());
    }
}
