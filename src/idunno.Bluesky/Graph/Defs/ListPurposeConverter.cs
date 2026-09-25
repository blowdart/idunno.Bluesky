// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json;
using System.Text.Json.Serialization;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace idunno.Bluesky.Graph;
#pragma warning restore IDE0130 // Namespace does not match folder structure

/// <summary>
/// Converts a <see cref="ListPurpose"/> to and from its JSON representation, mapping any purpose which is not
/// known to <see cref="ListPurpose.Unknown"/>.
/// </summary>
/// <remarks>
/// <para>The lexicon definition of a list purpose is an open union, so the set of purposes is decided by the
/// service, not by this library. A purpose added upstream would otherwise make every list carrying it fail to
/// deserialize. Unknown purposes are surfaced as <see cref="ListPurpose.Unknown"/> so the rest of the list can
/// still be read.</para>
/// </remarks>
internal sealed class ListPurposeConverter : JsonConverter<ListPurpose>
{
    /// <inheritdoc/>
    public override ListPurpose Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.String)
        {
            throw new JsonException($"Expected a string when reading a {nameof(ListPurpose)}, found {reader.TokenType}.");
        }

        return reader.GetString() switch
        {
            "app.bsky.graph.defs#modlist" => ListPurpose.ModList,
            "app.bsky.graph.defs#curatelist" => ListPurpose.CurateList,
            "app.bsky.graph.defs#referencelist" => ListPurpose.ReferenceList,
            _ => ListPurpose.Unknown
        };
    }

    /// <inheritdoc/>
    public override void Write(Utf8JsonWriter writer, ListPurpose value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);

        switch (value)
        {
            case ListPurpose.ModList:
                writer.WriteStringValue("app.bsky.graph.defs#modlist");
                break;

            case ListPurpose.CurateList:
                writer.WriteStringValue("app.bsky.graph.defs#curatelist");
                break;

            case ListPurpose.ReferenceList:
                writer.WriteStringValue("app.bsky.graph.defs#referencelist");
                break;

            default:
                throw new JsonException(
                    $"{nameof(ListPurpose)}.{nameof(ListPurpose.Unknown)} cannot be serialized. It only exists to carry a list purpose this library does not recognize.");
        }
    }
}
