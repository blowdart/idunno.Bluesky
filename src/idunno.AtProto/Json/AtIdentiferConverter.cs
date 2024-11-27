// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace idunno.AtProto.Json
{
    /// <summary>
    /// Converts an AT Identifier to or from JSON.
    /// </summary>
    [SuppressMessage("Performance", "CA1812", Justification = "Applied by attribute in AtIdentifier class.")]
    internal sealed class AtIdentifierConverter : JsonConverter<AtIdentifier>
    {
        /// <summary>
        /// Reads and converts JSON to an <see cref="AtIdentifier"/>/
        /// </summary>
        /// <param name="reader">The reader.</param>
        /// <param name="typeToConvert">The type to convert.</param>
        /// <param name="options">An object that specifies serialization options to use.</param>
        /// <returns>A <see cref="AtIdentifier"/> created from the JSON.</returns>
        /// <exception cref="JsonException">Thrown if the JSON to be converted is not a string token.</exception>
        public override AtIdentifier? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType != JsonTokenType.String)
            {
                throw new JsonException();
            }

            string? content = reader.GetString() ?? throw new JsonException();
            bool result = AtIdentifier.TryParse(content, out AtIdentifier? atIdentifier);

            if (!result)
            {
                throw new JsonException($"{content} cannot be parsed as an AtIdentifier");
            }

            return atIdentifier;
        }

        /// <summary>
        /// Writes the specified <see cref="AtIdentifier"></see> as JSON.
        /// </summary>
        /// <param name="writer">The writer to write to.</param>
        /// <param name="atIdentifier">The <see cref="Cid"/> to convert to JSON.</param>
        /// <param name="options">An object that specifies serialization options to use.</param>
        public override void Write(Utf8JsonWriter writer, AtIdentifier atIdentifier, JsonSerializerOptions options)
        {
            writer.WriteStringValue(atIdentifier.ToString());
        }
    }
}
