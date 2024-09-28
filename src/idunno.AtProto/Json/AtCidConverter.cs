// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json;
using System.Text.Json.Serialization;

namespace idunno.AtProto.Json
{
    /// <summary>
    /// Converts an AT CID to or from JSON.
    /// </summary>
    internal class AtCidConverter : JsonConverter<AtCid>
    {
        /// <summary>
        /// Reads and converts JSON to a NSID.
        /// </summary>
        /// <param name="reader">The reader.</param>
        /// <param name="typeToConvert">The type to convert.</param>
        /// <param name="options">An object that specifies serialization options to use.</param>
        /// <returns>A <see cref="AtCid"/> created from the JSON.</returns>
        /// <exception cref="JsonException">Thrown if the JSON to be converted is not a string token.</exception>
        public override AtCid? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType != JsonTokenType.String)
            {
                throw new JsonException();
            }

            AtCid? atCid;

            try
            {
                atCid = new AtCid(reader.GetString()!);
            }
            catch (ArgumentNullException e)
            {
                throw new JsonException("Could not value to an AtCid", e);
            }

            return atCid;
        }

        /// <summary>
        /// Writes the specified <see cref="AtCid"></see> as JSON.
        /// </summary>
        /// <param name="writer">The writer to write to.</param>
        /// <param name="handle">The <see cref="AtCid"/> to convert to JSON.</param>
        /// <param name="options">An object that specifies serialization options to use.</param>
        public override void Write(Utf8JsonWriter writer, AtCid did, JsonSerializerOptions options)
        {
            writer.WriteStringValue(did.Value);
        }
    }
}
