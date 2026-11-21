// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json;
using System.Text.Json.Serialization;

using idunno.Bluesky.Record;

namespace idunno.Bluesky.Json;

/// <summary>
/// Reads and writes a property whose lexicon type is an <c>unknown</c> record, preserving records with an unrecognized <c>$type</c>.
/// </summary>
/// <remarks>
/// <para>Records with a recognized <c>$type</c> are dispatched through the polymorphic <see cref="BlueskyRecord"/> contract.
/// <see cref="BlueskyRecord"/> falls back to its own contract for an unrecognized discriminator, which discards the <c>$type</c>.
/// When that happens this converter returns an <see cref="UnknownBlueskyRecord"/> holding the original JSON instead.</para>
/// <para>A record without a <c>$type</c> is read as a base <see cref="BlueskyRecord"/>.</para>
/// <para>Each record read through this converter starts a nested deserialization, which allocates several kilobytes.
/// Apply it only where records of other types are expected, not to properties on hot feed paths that almost always hold posts.</para>
/// </remarks>
internal sealed class BlueskyRecordValueConverter : JsonConverter<BlueskyRecord>
{
    /// <summary>
    /// Reads a record, returning an <see cref="UnknownBlueskyRecord"/> when its <c>$type</c> is not recognized.
    /// </summary>
    /// <param name="reader">The reader positioned at the record.</param>
    /// <param name="typeToConvert">The type to convert.</param>
    /// <param name="options">The serializer options in use.</param>
    /// <returns>The record that was read.</returns>
    /// <exception cref="JsonException">Thrown when the JSON is not an object.</exception>
    public override BlueskyRecord? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartObject)
        {
            throw new JsonException("A record must be a JSON object.");
        }
        bool hasDiscriminator = TryPeekDiscriminator(reader, out Utf8JsonReader discriminatorReader);

        BlueskyRecord? record = (BlueskyRecord?)JsonSerializer.Deserialize(ref reader, options.GetTypeInfo(typeof(BlueskyRecord)));

        // Every recognized discriminator maps to a derived type, so an exact BlueskyRecord means the
        // polymorphic contract fell back to the base type and the record type would otherwise be lost.
        if (hasDiscriminator && record is not null && record.GetType() == typeof(BlueskyRecord) && discriminatorReader.TokenType == JsonTokenType.String)
        {
            return new UnknownBlueskyRecord(discriminatorReader.GetString()!, record.ExtensionData);
        }

        return record;
    }

    /// <summary>
    /// Scans the top-level properties of the object at <paramref name="reader"/> for a <c>$type</c> property without advancing the caller's reader.
    /// </summary>
    /// <param name="reader">A copy of the reader, positioned at the start of the object.</param>
    /// <param name="discriminatorReader">When this method returns <see langword="true"/>, a reader positioned at the <c>$type</c> value.</param>
    /// <returns><see langword="true"/> if the object has a <c>$type</c> property; otherwise <see langword="false"/>.</returns>
    /// <remarks>
    /// <para>The serializer buffers the complete value before invoking a converter, so skipping property values cannot run out of data.
    /// Peeking avoids buffering the record into a <see cref="JsonDocument"/>, and returning a reader defers allocating the
    /// <c>$type</c> string until a record turns out to be unrecognized, which matters on hot feed paths.</para>
    /// </remarks>
    private static bool TryPeekDiscriminator(Utf8JsonReader reader, out Utf8JsonReader discriminatorReader)
    {
        while (reader.Read() && reader.TokenType == JsonTokenType.PropertyName)
        {
            bool isDiscriminator = reader.ValueTextEquals("$type"u8);

            reader.Read();

            if (isDiscriminator)
            {
                discriminatorReader = reader;
                return true;
            }

            reader.Skip();
        }

        discriminatorReader = default;
        return false;
    }

    /// <summary>
    /// Writes a record, writing an <see cref="UnknownBlueskyRecord"/> back as the JSON it was read from.
    /// </summary>
    /// <param name="writer">The writer to write to.</param>
    /// <param name="value">The record to write.</param>
    /// <param name="options">The serializer options in use.</param>
    public override void Write(Utf8JsonWriter writer, BlueskyRecord value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentNullException.ThrowIfNull(value);

        if (value is UnknownBlueskyRecord unknownRecord)
        {
            writer.WriteStartObject();
            foreach (KeyValuePair<string, JsonElement> property in unknownRecord.ExtensionData ?? new Dictionary<string, JsonElement>())
            {
                writer.WritePropertyName(property.Key);
                property.Value.WriteTo(writer);
            }
            writer.WriteEndObject();
            return;
        }

        JsonSerializer.Serialize(writer, value, options.GetTypeInfo(typeof(BlueskyRecord)));
    }
}
