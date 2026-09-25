// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json;
using System.Text.Json.Serialization;

namespace idunno.AtProto.Jetstream;

/// <summary>
/// Converts a jetstream commit operation to and from its JSON representation, mapping any operation which is not
/// known to <see cref="JetstreamCommitOperation.Unknown"/>.
/// </summary>
/// <remarks>
/// <para>The set of operations a jetstream emits is decided by the server, not by this library, so an operation which
/// is added upstream would otherwise make every commit carrying it fail to deserialize.</para>
/// </remarks>
internal sealed class JetstreamCommitOperationConverter : JsonConverter<JetstreamCommitOperation>
{
    public override JetstreamCommitOperation Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.String)
        {
            throw new JsonException($"Expected a string when reading a {nameof(JetstreamCommitOperation)}, found {reader.TokenType}.");
        }

        string? value = reader.GetString();

        return value switch
        {
            "create" => JetstreamCommitOperation.Create,
            "update" => JetstreamCommitOperation.Update,
            "delete" => JetstreamCommitOperation.Delete,
            _ => JetstreamCommitOperation.Unknown
        };
    }

    public override void Write(Utf8JsonWriter writer, JetstreamCommitOperation value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);

        switch (value)
        {
            case JetstreamCommitOperation.Create:
                writer.WriteStringValue("create");
                break;

            case JetstreamCommitOperation.Update:
                writer.WriteStringValue("update");
                break;

            case JetstreamCommitOperation.Delete:
                writer.WriteStringValue("delete");
                break;

            default:
                writer.WriteStringValue("unknown");
                break;
        }
    }
}
