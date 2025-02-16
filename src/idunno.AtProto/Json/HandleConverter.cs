// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace idunno.AtProto.Json
{
    /// <summary>
    /// Converts a Handle to or from JSON.
    /// </summary>
    [SuppressMessage("Performance", "CA1812", Justification = "Applied by attribute in Handle class.")]
    internal sealed class HandleConverter : JsonConverter<Handle>
    {
        /// <summary>
        /// Reads and converts JSON to a <see cref="Handle"/>.
        /// </summary>
        /// <param name="reader">The reader.</param>
        /// <param name="typeToConvert">The type to convert.</param>
        /// <param name="options">An object that specifies serialization options to use.</param>
        /// <returns>A <see cref="Handle"/> created from the JSON.</returns>
        /// <exception cref="JsonException">Thrown when the JSON to be converted is not a string token.</exception>
        public override Handle? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType != JsonTokenType.String)
            {
                throw new JsonException();
            }

            Handle? handle;

            try
            {
                handle = new Handle(reader.GetString()!);
            }
            catch (ArgumentNullException e)
            {
                throw new JsonException("Value cannot be null or empty.", e);
            }
            catch (ArgumentException e)
            {
                throw new JsonException("Value is not a valid Handle.", e);
            }

            return handle;
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
