// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json;
using System.Text.Json.Serialization;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace idunno.Bluesky.Feed;
#pragma warning restore IDE0130 // Namespace does not match folder structure

/// <summary>
/// Converts an <see cref="InteractionViewContext"/> to and from its lexicon token, mapping unrecognized tokens to <see cref="InteractionViewContext.Unknown"/>.
/// </summary>
internal sealed class InteractionViewContextConverter : JsonConverter<InteractionViewContext>
{
    /// <inheritdoc/>
    public override InteractionViewContext Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.String)
        {
            throw new JsonException($"Expected a string when reading an {nameof(InteractionViewContext)}, found {reader.TokenType}.");
        }

        return reader.GetString() switch
        {
            "feed" => InteractionViewContext.Feed,
            "thread" => InteractionViewContext.Thread,
            _ => InteractionViewContext.Unknown
        };
    }

    /// <inheritdoc/>
    public override void Write(Utf8JsonWriter writer, InteractionViewContext value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);

        string token = value switch
        {
            InteractionViewContext.Feed => "feed",
            InteractionViewContext.Thread => "thread",
            _ => throw new JsonException(
                $"{nameof(InteractionViewContext)}.{value} cannot be serialized. {nameof(InteractionViewContext.Unknown)} only exists to carry a value this library does not recognize.")
        };

        writer.WriteStringValue(token);
    }
}
