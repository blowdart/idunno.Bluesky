// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json;
using System.Text.Json.Serialization;

namespace idunno.Bluesky.Feed;

/// <summary>
/// Converts a <see cref="GeneratorContentMode"/> to and from its JSON representation.
/// </summary>
internal sealed class GeneratorContentModeConverter : JsonConverter<GeneratorContentMode>
{
    /// <inheritdoc/>
    public override GeneratorContentMode Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.String)
        {
            throw new JsonException($"Expected a string when reading a {nameof(GeneratorContentMode)}, found {reader.TokenType}.");
        }

        return reader.GetString() switch
        {
            "app.bsky.feed.defs#contentModeUnspecified" => GeneratorContentMode.Unspecified,
            "app.bsky.feed.defs#contentModeVideo" => GeneratorContentMode.Video,
            _ => GeneratorContentMode.Unknown
        };
    }

    /// <inheritdoc/>
    public override void Write(Utf8JsonWriter writer, GeneratorContentMode value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);

        string contentMode = value switch
        {
            GeneratorContentMode.Unspecified => "app.bsky.feed.defs#contentModeUnspecified",
            GeneratorContentMode.Video => "app.bsky.feed.defs#contentModeVideo",
            _ => throw new JsonException(
                $"{nameof(GeneratorContentMode)}.{nameof(GeneratorContentMode.Unknown)} cannot be serialized. It only exists to carry a value this library does not recognize.")
        };

        writer.WriteStringValue(contentMode);
    }
}
