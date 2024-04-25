// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json;
using System.Text.Json.Serialization;

namespace idunno.AtProto.Json
{
    /// <summary>
    /// Converts an AT URI to or from JSON.
    /// </summary>
    internal class AtUriJsonConverter : JsonConverter<AtUri>
    {
        /// <summary>
        /// Reads and converts JSON to a DID.
        /// </summary>
        /// <param name="reader">The reader.</param>
        /// <param name="typeToConvert">The type to convert.</param>
        /// <param name="options">An object that specifies serialization options to use.</param>
        /// <returns>An <see cref="AtUri"/> created from the JSON.</returns>
        /// <exception cref="JsonException">Thrown if the JSON to be converted is not a string token.</exception>
        public override AtUri? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            return reader.TokenType != JsonTokenType.String ? throw new JsonException() : new AtUri(reader.GetString()!);
        }

        /// <summary>
        /// Writes the specified <see cref="AtUri"></see> as JSON.
        /// </summary>
        /// <param name="writer">The writer to write to.</param>
        /// <param name="handle">The <see cref="Did"/> to convert to JSON.</param>
        /// <param name="options">An object that specifies serialization options to use.</param>
        public override void Write(Utf8JsonWriter writer, AtUri atUri, JsonSerializerOptions options)
        {
            writer.WriteStringValue(atUri.ToString());
        }
    }
}
