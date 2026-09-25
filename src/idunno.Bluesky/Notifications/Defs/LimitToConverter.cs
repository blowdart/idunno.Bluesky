// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json;
using System.Text.Json.Serialization;

namespace idunno.Bluesky.Notifications.PreferenceTypes;

/// <summary>
/// Converts a <see cref="LimitTo"/> to and from its JSON representation, mapping any value which is not known to
/// <see cref="LimitTo.Unknown"/>.
/// </summary>
/// <remarks>
/// <para>The lexicon definition of a filterable notification preference is an open list of known values, so the
/// set of values is decided by the service, not by this library. A value added upstream would otherwise make every
/// preferences response carrying it fail to deserialize. Unknown values are surfaced as
/// <see cref="LimitTo.Unknown"/> so the rest of the response can still be read.</para>
/// </remarks>
internal sealed class LimitToConverter : JsonConverter<LimitTo>
{
    /// <inheritdoc/>
    public override LimitTo Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.String)
        {
            throw new JsonException($"Expected a string when reading a {nameof(LimitTo)}, found {reader.TokenType}.");
        }

        return reader.GetString() switch
        {
            "all" => LimitTo.All,
            "follows" => LimitTo.Follows,
            _ => LimitTo.Unknown
        };
    }

    /// <inheritdoc/>
    public override void Write(Utf8JsonWriter writer, LimitTo value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);

        switch (value)
        {
            case LimitTo.All:
                writer.WriteStringValue("all");
                break;

            case LimitTo.Follows:
                writer.WriteStringValue("follows");
                break;

            default:
                throw new JsonException(
                    $"{nameof(LimitTo)}.{nameof(LimitTo.Unknown)} cannot be serialized. It only exists to carry a value this library does not recognize.");
        }
    }
}
