﻿// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json;
using System.Text.Json.Serialization;

namespace idunno.AtProto.Json
{
    /// <summary>
    /// Converts an AT URI to or from JSON.
    /// </summary>
    public sealed class AtUriConverter : JsonConverter<AtUri>
    {
        /// <summary>
        /// Reads and converts JSON to an <see cref="AtUri"/>.
        /// </summary>
        /// <param name="reader">The reader.</param>
        /// <param name="typeToConvert">The type to convert.</param>
        /// <param name="options">An object that specifies serialization options to use.</param>
        /// <returns>An <see cref="AtUri"/> created from the JSON.</returns>
        /// <exception cref="JsonException">Thrown when the JSON to be converted is not a string token.</exception>
        public override AtUri? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType != JsonTokenType.String)
            {
                throw new JsonException();
            }

            AtUri? atUri;

            try
            {
                atUri = new AtUri(reader.GetString()!);
            }
            catch (ArgumentException e)
            {
                throw new JsonException("Value cannot be null or an empty string.", e);
            }
            catch (AtUriFormatException e)
            {
                throw new JsonException("Invalid AT URI format.", e);
            }
            catch (RecordKeyFormatException e)
            {
                throw new JsonException("Invalid rKey format.", e);
            }

            return atUri;
        }

        /// <summary>
        /// Writes the specified <see cref="AtUri"></see> as JSON.
        /// </summary>
        /// <param name="writer">The writer to write to.</param>
        /// <param name="value">The <see cref="Did"/> to convert to JSON.</param>
        /// <param name="options">An object that specifies serialization options to use.</param>
        /// <exception cref="ArgumentNullException">Throws when <paramref name="writer"/> or <paramref name="value"/> is null.</exception>
        public override void Write(Utf8JsonWriter writer, AtUri value, JsonSerializerOptions options)
        {
            ArgumentNullException.ThrowIfNull(writer);
            ArgumentNullException.ThrowIfNull(value);

            writer.WriteStringValue(value.ToString());
        }
    }
}
