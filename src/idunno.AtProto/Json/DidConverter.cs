// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace idunno.AtProto.Json
{
    /// <summary>
    /// Converts a DID to or from JSON.
    /// </summary>
    [SuppressMessage("Performance", "CA1812", Justification = "Applied by attribute in Did class.")]
    internal sealed class DidConverter : JsonConverter<Did>
    {
        /// <summary>
        /// Reads and converts JSON to a <see cref="Did"/>>.
        /// </summary>
        /// <param name="reader">The reader.</param>
        /// <param name="typeToConvert">The type to convert.</param>
        /// <param name="options">An object that specifies serialization options to use.</param>
        /// <returns>A <see cref="Did"/> created from the JSON.</returns>
        /// <exception cref="JsonException">Thrown if the JSON to be converted is not a string token.</exception>
        public override Did? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType != JsonTokenType.String)
            {
                throw new JsonException();
            }

            Did? did;

            try
            {
                did = new Did(reader.GetString()!);
            }
            catch (ArgumentNullException e)
            {
                throw new JsonException("Value cannot be null or empty.", e);
            }
            catch (ArgumentException e)
            {
                throw new JsonException("Value is not a valid DID.", e);
            }

            return reader.TokenType != JsonTokenType.String ? throw new JsonException() : did;
        }

        /// <summary>
        /// Writes the specified <see cref="Did"></see> as JSON.
        /// </summary>
        /// <param name="writer">The writer to write to.</param>
        /// <param name="did">The <see cref="Did"/> to convert to JSON.</param>
        /// <param name="options">An object that specifies serialization options to use.</param>
        public override void Write(Utf8JsonWriter writer, Did did, JsonSerializerOptions options)
        {
            writer.WriteStringValue(did.Value);
        }
    }
}
