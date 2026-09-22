// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json;
using System.Text.Json.Serialization;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace idunno.Bluesky.Actor;
#pragma warning restore IDE0130 // Namespace does not match folder structure

/// <summary>
/// Converts an <see cref="AllowIncomingChat"/> to and from its JSON representation, mapping any value which is not
/// known to <see cref="AllowIncomingChat.Unknown"/>.
/// </summary>
/// <remarks>
/// <para>The lexicon definition of an actor's incoming chat configuration is an open union, so the set of values is
/// decided by the service, not by this library. A value added upstream would otherwise make every profile carrying
/// it fail to deserialize. Unknown values are surfaced as <see cref="AllowIncomingChat.Unknown"/> so the rest of
/// the profile can still be read.</para>
/// <para><see cref="ProfileAssociatedChat"/> is only ever read from the service, never sent to it, so an unknown
/// value does not need to round trip.</para>
/// </remarks>
internal sealed class AllowIncomingChatConverter : JsonConverter<AllowIncomingChat>
{
    /// <inheritdoc/>
    public override AllowIncomingChat Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.String)
        {
            throw new JsonException($"Expected a string when reading an {nameof(AllowIncomingChat)}, found {reader.TokenType}.");
        }

        return reader.GetString() switch
        {
            "none" => AllowIncomingChat.None,
            "all" => AllowIncomingChat.All,
            "following" => AllowIncomingChat.Following,
            _ => AllowIncomingChat.Unknown
        };
    }

    /// <inheritdoc/>
    public override void Write(Utf8JsonWriter writer, AllowIncomingChat value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);

        switch (value)
        {
            case AllowIncomingChat.None:
                writer.WriteStringValue("none");
                break;

            case AllowIncomingChat.All:
                writer.WriteStringValue("all");
                break;

            case AllowIncomingChat.Following:
                writer.WriteStringValue("following");
                break;

            default:
                throw new JsonException(
                    $"{nameof(AllowIncomingChat)}.{nameof(AllowIncomingChat.Unknown)} cannot be serialized. It only exists to carry a value this library does not recognize.");
        }
    }
}
