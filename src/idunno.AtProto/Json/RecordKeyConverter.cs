// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace idunno.AtProto.Json
{
    /// <summary>
    /// Converts a RecordKey to or from JSON.
    /// </summary>
    [SuppressMessage("Performance", "CA1812", Justification = "Applied by attribute in RecordKey class.")]
    internal sealed class RecordKeyConverter : JsonConverter<RecordKey>
    {
        /// <summary>
        /// Reads and converts JSON to an <see cref="RecordKey"/>
        /// </summary>
        /// <param name="reader">The reader.</param>
        /// <param name="typeToConvert">The type to convert.</param>
        /// <param name="options">An object that specifies serialization options to use.</param>
        /// <returns>A <see cref="RecordKey"/> created from the JSON.</returns>
        /// <exception cref="JsonException">Thrown if the JSON to be converted is not a string token.</exception>
        public override RecordKey? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType != JsonTokenType.String)
            {
                throw new JsonException();
            }

            RecordKey? rKey;

            try
            {
                rKey = new RecordKey(reader.GetString()!);
            }
            catch (ArgumentNullException e)
            {
                throw new JsonException("Value cannot be null or empty.", e);
            }
            catch (ArgumentException e)
            {
                throw new JsonException("Value is not a valid RecordKet.", e);
            }
            catch (NsidFormatException e)
            {
                throw new JsonException("Value is not a valid RecordKet.", e);
            }

            return rKey;
        }

        /// <summary>
        /// Writes the specified <see cref="RecordKey" /> as JSON.
        /// </summary>
        /// <param name="writer">The writer to write to.</param>
        /// <param name="nsid">The <see cref="Cid"/> to convert to JSON.</param>
        /// <param name="options">An object that specifies serialization options to use.</param>
        public override void Write(Utf8JsonWriter writer, RecordKey nsid, JsonSerializerOptions options)
        {
            writer.WriteStringValue(nsid.ToString());
        }
    }
}
