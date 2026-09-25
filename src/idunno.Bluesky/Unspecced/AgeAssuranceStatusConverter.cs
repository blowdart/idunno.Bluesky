// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json;
using System.Text.Json.Serialization;

namespace idunno.Bluesky.Unspecced;

/// <summary>
/// Converts an <see cref="AgeAssuranceStatus"/> to and from its JSON representation, mapping any status which is
/// not known to <see cref="AgeAssuranceStatus.Unknown"/>.
/// </summary>
/// <remarks>
/// <para>The lexicon definition of an age assurance status is an open list of known values, so the set of statuses
/// is decided by the service, not by this library. A status added upstream would otherwise make every response
/// carrying it fail to deserialize. Unknown statuses are surfaced as <see cref="AgeAssuranceStatus.Unknown"/> so
/// the rest of the response can still be read.</para>
/// <para>This lexicon is unspecced, and so changes upstream more often than most.</para>
/// </remarks>
internal sealed class AgeAssuranceStatusConverter : JsonConverter<AgeAssuranceStatus>
{
    /// <inheritdoc/>
    public override AgeAssuranceStatus Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.String)
        {
            throw new JsonException($"Expected a string when reading an {nameof(AgeAssuranceStatus)}, found {reader.TokenType}.");
        }

        return reader.GetString() switch
        {
            "unknown" => AgeAssuranceStatus.Unknown,
            "pending" => AgeAssuranceStatus.Pending,
            "assured" => AgeAssuranceStatus.Assured,
            "blocked" => AgeAssuranceStatus.Blocked,
            _ => AgeAssuranceStatus.Unknown
        };
    }

    /// <inheritdoc/>
    public override void Write(Utf8JsonWriter writer, AgeAssuranceStatus value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);

        switch (value)
        {
            case AgeAssuranceStatus.Pending:
                writer.WriteStringValue("pending");
                break;

            case AgeAssuranceStatus.Assured:
                writer.WriteStringValue("assured");
                break;

            case AgeAssuranceStatus.Blocked:
                writer.WriteStringValue("blocked");
                break;

            default:
                writer.WriteStringValue("unknown");
                break;
        }
    }
}
