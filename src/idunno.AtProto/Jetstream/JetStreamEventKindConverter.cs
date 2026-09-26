// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json;
using System.Text.Json.Serialization;

namespace idunno.AtProto.Jetstream;

/// <summary>
/// Converts a jetstream event kind to and from its JSON representation, mapping any kind which is not known
/// to <see cref="JetStreamEventKind.Unknown"/>.
/// </summary>
/// <remarks>
/// <para>The set of event kinds a jetstream emits is decided by the server, not by this library, so a kind which is
/// added upstream would otherwise make every message carrying it fail to deserialize. Unknown kinds are surfaced as
/// <see cref="JetStreamEventKind.Unknown"/> so the rest of the event can still be read.</para>
/// </remarks>
internal sealed class JetStreamEventKindConverter : JsonConverter<JetStreamEventKind>
{
    public override JetStreamEventKind Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.String)
        {
            throw new JsonException($"Expected a string when reading a {nameof(JetStreamEventKind)}, found {reader.TokenType}.");
        }

        string? value = reader.GetString();

        return value switch
        {
            "account" => JetStreamEventKind.Account,
            "commit" => JetStreamEventKind.Commit,
            "identity" => JetStreamEventKind.Identity,
            "sync" => JetStreamEventKind.Sync,
            _ => JetStreamEventKind.Unknown
        };
    }

    public override void Write(Utf8JsonWriter writer, JetStreamEventKind value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);

        switch (value)
        {
            case JetStreamEventKind.Account:
                writer.WriteStringValue("account");
                break;

            case JetStreamEventKind.Commit:
                writer.WriteStringValue("commit");
                break;

            case JetStreamEventKind.Identity:
                writer.WriteStringValue("identity");
                break;

            case JetStreamEventKind.Sync:
                writer.WriteStringValue("sync");
                break;

            default:
                writer.WriteStringValue("unknown");
                break;
        }
    }
}
