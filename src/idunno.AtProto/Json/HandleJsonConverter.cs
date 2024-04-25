// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json;
using System.Text.Json.Serialization;

namespace idunno.AtProto.Json
{
    /// <summary>
    /// Converts a Handle to or from JSON.
    /// </summary>
    internal class HandleJsonConverter : JsonConverter<Handle>
    {
        /// <summary>
        /// Reads and converts JSON to a Handle
        /// </summary>
        /// <param name="reader">The reader.</param>
        /// <param name="typeToConvert">The type to convert.</param>
        /// <param name="options">An object that specifies serialization options to use.</param>
        /// <returns>A <see cref="Handle"/> created from the JSON.</returns>
        /// <exception cref="JsonException">Thrown if the JSON to be converted is not a string token.</exception>
        public override Handle? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            return reader.TokenType != JsonTokenType.String ? throw new JsonException() : new Handle(reader.GetString()!);
        }

        /// <summary>
        /// Writes the specified <see cref="Handle"/> as JSON.
        /// </summary>
        /// <param name="writer">The writer to write to.</param>
        /// <param name="handle">The <see cref="Handle"/> to convert to JSON.</param>
        /// <param name="options">An object that specifies serialization options to use.</param>
        public override void Write(Utf8JsonWriter writer, Handle handle, JsonSerializerOptions options)
        {
            writer.WriteStringValue(handle.Value);
        }
    }
}
