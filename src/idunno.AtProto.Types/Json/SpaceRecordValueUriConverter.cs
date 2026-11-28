// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace idunno.AtProto.Json;

/// <summary>
/// Converts AT URIs in alpha-era record values, accepting legacy space URIs on read.
/// </summary>
/// <remarks>
/// <para>Add this converter to options used only to deserialize record values. Do not use those options for response
/// envelopes, endpoint parameters, credentials, MAC headers, archives, or untrusted new record input.</para>
/// <para>Legacy space URIs are normalized to the current form. Reserializing a record containing one changes its
/// content and therefore its CID; retain the original record value when its original CID matters.</para>
/// </remarks>
[Experimental("BSKYSpaces", UrlFormat = "https://bluesky.idunno.dev/docs/spaces.html")]
public sealed class SpaceRecordValueUriConverter : JsonConverterFactory
{
    /// <summary>
    /// Determines whether the converter supports the specified type.
    /// </summary>
    /// <param name="typeToConvert">The type to convert.</param>
    /// <returns><see langword="true"/> for <see cref="AtUri"/> or <see cref="SpaceAtUri"/>; otherwise, <see langword="false"/>.</returns>
    public override bool CanConvert(Type typeToConvert) => typeToConvert == typeof(AtUri) || typeToConvert == typeof(SpaceAtUri);

    /// <summary>
    /// Creates a converter for the specified AT URI type.
    /// </summary>
    /// <param name="typeToConvert">The type to convert.</param>
    /// <param name="options">The serializer options.</param>
    /// <returns>A converter for the specified AT URI type.</returns>
    /// <exception cref="ArgumentException"><paramref name="typeToConvert"/> is not a supported AT URI type.</exception>
    public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options)
    {
        if (typeToConvert == typeof(AtUri))
        {
            return new RecordAtUriConverter();
        }

        if (typeToConvert == typeof(SpaceAtUri))
        {
            return new RecordSpaceAtUriConverter();
        }

        throw new ArgumentException("The type must be AtUri or SpaceAtUri.", nameof(typeToConvert));
    }

    /// <summary>
    /// Reads AT URIs in record values without changing the default AT URI converter.
    /// </summary>
    private sealed class RecordAtUriConverter : JsonConverter<AtUri>
    {
        /// <inheritdoc/>
        public override AtUri Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType != JsonTokenType.String)
            {
                throw new JsonException("An AT URI must be a string.");
            }

            string? value = reader.GetString();
            if (SpaceAtUri.TryParseRecordValue(value, out SpaceAtUri? spaceUri))
            {
                return spaceUri;
            }

            if (value is not null && AtUri.TryParse(value, out AtUri? atUri))
            {
                return atUri;
            }

            throw new JsonException("Invalid AT URI format.");
        }

        /// <inheritdoc/>
        public override void Write(Utf8JsonWriter writer, AtUri value, JsonSerializerOptions options) =>
            writer.WriteStringValue(value.ToString());
    }

    /// <summary>
    /// Reads space URIs in record values without changing the default space URI converter.
    /// </summary>
    private sealed class RecordSpaceAtUriConverter : JsonConverter<SpaceAtUri>
    {
        /// <inheritdoc/>
        public override SpaceAtUri Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType != JsonTokenType.String || !SpaceAtUri.TryParseRecordValue(reader.GetString(), out SpaceAtUri? value))
            {
                throw new JsonException("Invalid space URI format.");
            }

            return value;
        }

        /// <inheritdoc/>
        public override void Write(Utf8JsonWriter writer, SpaceAtUri value, JsonSerializerOptions options) =>
            writer.WriteStringValue(value.ToString());
    }
}
