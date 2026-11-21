// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Buffers;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace idunno.Bluesky.Record;

/// <summary>
/// A record whose <c>$type</c> is not one of the record types this library understands.
/// </summary>
/// <remarks>
/// <para>Lexicons such as <c>app.bsky.embed.record#viewRecord</c> and <c>app.bsky.feed.defs#postView</c> declare their record
/// as <c>unknown</c>, so a server can return any record type, including types defined after this library was released.
/// Rather than discarding the record type, the library returns an <see cref="UnknownBlueskyRecord"/> whose
/// <see cref="AtProto.Repo.AtProtoRecord.ExtensionData"/> holds every property of the original JSON, including <c>$type</c>.</para>
/// <para>Serializing an <see cref="UnknownBlueskyRecord"/> through the property it was read from writes those properties back
/// unchanged, so the record round-trips without losing its type.</para>
/// <para>The record contents come from a remote server and have not been validated against any lexicon.</para>
/// </remarks>
public sealed record UnknownBlueskyRecord : BlueskyRecord
{
    /// <summary>
    /// Creates a new instance of <see cref="UnknownBlueskyRecord"/> from the record type and the remaining properties of the record.
    /// </summary>
    /// <param name="recordType">The lexicon <c>$type</c> of the record.</param>
    /// <param name="properties">The other properties of the record, which are copied after <c>$type</c>.</param>
    internal UnknownBlueskyRecord(string recordType, IDictionary<string, JsonElement>? properties)
    {
        ArgumentNullException.ThrowIfNull(recordType);

        Dictionary<string, JsonElement> extensionData = new(StringComparer.Ordinal)
        {
            ["$type"] = CreateStringElement(recordType)
        };

        if (properties is not null)
        {
            foreach (KeyValuePair<string, JsonElement> property in properties)
            {
                extensionData.TryAdd(property.Key, property.Value);
            }
        }

        ExtensionData = extensionData;
    }

    /// <summary>
    /// Gets the lexicon <c>$type</c> of the record, or <see langword="null"/> if the record does not have a string <c>$type</c>.
    /// </summary>
    [JsonIgnore]
    public string? RecordType =>
        ExtensionData is not null && ExtensionData.TryGetValue("$type", out JsonElement recordType) && recordType.ValueKind == JsonValueKind.String
            ? recordType.GetString()
            : null;

    /// <summary>
    /// Creates a standalone <see cref="JsonElement"/> holding the JSON string <paramref name="value"/>.
    /// </summary>
    /// <param name="value">The string to encode.</param>
    /// <returns>A <see cref="JsonElement"/> that does not depend on a live <see cref="JsonDocument"/>.</returns>
    private static JsonElement CreateStringElement(string value)
    {
        ArrayBufferWriter<byte> buffer = new();
        using (Utf8JsonWriter writer = new(buffer))
        {
            writer.WriteStringValue(value);
        }

        using JsonDocument document = JsonDocument.Parse(buffer.WrittenMemory);
        return document.RootElement.Clone();
    }
}
