// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json;
using System.Text.Json.Serialization;

namespace idunno.Bluesky.Notifications;

/// <summary>
/// Converts a <see cref="NotificationAllowedFrom"/> to and from its JSON representation, mapping any value which
/// is not known to <see cref="NotificationAllowedFrom.Unknown"/>.
/// </summary>
/// <remarks>
/// <para>The lexicon definition of an activity subscription preference is an open list of known values, so the set
/// of values is decided by the service, not by this library. A value added upstream would otherwise make every
/// declaration carrying it fail to deserialize. Unknown values are surfaced as
/// <see cref="NotificationAllowedFrom.Unknown"/> so the rest of the declaration can still be read.</para>
/// </remarks>
internal sealed class NotificationAllowedFromConverter : JsonConverter<NotificationAllowedFrom>
{
    /// <inheritdoc/>
    public override NotificationAllowedFrom Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.String)
        {
            throw new JsonException($"Expected a string when reading a {nameof(NotificationAllowedFrom)}, found {reader.TokenType}.");
        }

        return reader.GetString() switch
        {
            "none" => NotificationAllowedFrom.None,
            "followers" => NotificationAllowedFrom.Followers,
            "mutuals" => NotificationAllowedFrom.Mutuals,
            _ => NotificationAllowedFrom.Unknown
        };
    }

    /// <inheritdoc/>
    public override void Write(Utf8JsonWriter writer, NotificationAllowedFrom value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);

        switch (value)
        {
            case NotificationAllowedFrom.None:
                writer.WriteStringValue("none");
                break;

            case NotificationAllowedFrom.Followers:
                writer.WriteStringValue("followers");
                break;

            case NotificationAllowedFrom.Mutuals:
                writer.WriteStringValue("mutuals");
                break;

            default:
                throw new JsonException(
                    $"{nameof(NotificationAllowedFrom)}.{nameof(NotificationAllowedFrom.Unknown)} cannot be serialized. It only exists to carry a value this library does not recognize.");
        }
    }
}
