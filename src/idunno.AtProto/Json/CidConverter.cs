// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json;
using System.Text.Json.Serialization;

namespace idunno.AtProto.Json
{
    /// <summary>
    /// Converts an AT CID to or from JSON.
    /// </summary>
    public sealed class CidConverter : JsonConverter<Cid>
    {
        /// <summary>
        /// Reads and converts JSON to an <see cref="Cid"/>.
        /// </summary>
        /// <param name="reader">The reader.</param>
        /// <param name="typeToConvert">The type to convert.</param>
        /// <param name="options">An object that specifies serialization options to use.</param>
        /// <returns>A <see cref="Cid"/> created from the JSON.</returns>
        /// <exception cref="JsonException">Thrown when the JSON to be converted is not a string token.</exception>
        public override Cid? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType != JsonTokenType.String)
            {
                throw new JsonException();
            }

            Cid? cid;

            try
            {
                cid = new Cid(reader.GetString()!);
            }
            catch (ArgumentNullException e)
            {
                throw new JsonException("Could not convert value to an Cid", e);
            }

            return cid;
        }

        /// <summary>
        /// Writes the specified <see cref="Cid"></see> as JSON.
        /// </summary>
        /// <param name="writer">The writer to write to.</param>
        /// <param name="value">The <see cref="Cid"/> to convert to JSON.</param>
        /// <param name="options">An object that specifies serialization options to use.</param>
        /// <exception cref="ArgumentNullException">Throws when <paramref name="writer"/> or <paramref name="value"/> is null.</exception>
        public override void Write(Utf8JsonWriter writer, Cid value, JsonSerializerOptions options)
        {
            ArgumentNullException.ThrowIfNull(writer);
            ArgumentNullException.ThrowIfNull(value);

            writer.WriteStringValue(value.Value);
        }
    }
}
