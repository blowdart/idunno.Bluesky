// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json;
using System.Text.Json.Serialization;

namespace idunno.AtProto.Json
{
    /// <summary>
    /// Converts a NSID to or from JSON.
    /// </summary>
    public sealed class NsidConverter : JsonConverter<Nsid>
    {
        /// <summary>
        /// Reads and converts JSON to an <see cref="Nsid"/>
        /// </summary>
        /// <param name="reader">The reader.</param>
        /// <param name="typeToConvert">The type to convert.</param>
        /// <param name="options">An object that specifies serialization options to use.</param>
        /// <returns>A <see cref="RecordKey"/> created from the JSON.</returns>
        /// <exception cref="JsonException">Thrown when the JSON to be converted is not a string token.</exception>
        public override Nsid? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType != JsonTokenType.String)
            {
                throw new JsonException();
            }

            Nsid? nsid;

            try
            {
                nsid = new Nsid(reader.GetString()!);
            }
            catch (ArgumentNullException e)
            {
                throw new JsonException("Value cannot be null or empty.", e);
            }
            catch (ArgumentException e)
            {
                throw new JsonException("Value is not a valid NSID.", e);
            }
            catch (NsidFormatException e)
            {
                throw new JsonException("Value is not a valid NSID.", e);
            }

            return nsid;
        }

        /// <summary>
        /// Writes the specified <see cref="RecordKey" /> as JSON.
        /// </summary>
        /// <param name="writer">The writer to write to.</param>
        /// <param name="nsid">The <see cref="Cid"/> to convert to JSON.</param>
        /// <param name="options">An object that specifies serialization options to use.</param>
        /// <exception cref="ArgumentNullException">Throws when <paramref name="writer"/> or <paramref name="nsid"/> is null.</exception>
        public override void Write(Utf8JsonWriter writer, Nsid nsid, JsonSerializerOptions options)
        {
            ArgumentNullException.ThrowIfNull(writer);
            ArgumentNullException.ThrowIfNull(nsid);

            writer.WriteStringValue(nsid.ToString());
        }
    }
}
