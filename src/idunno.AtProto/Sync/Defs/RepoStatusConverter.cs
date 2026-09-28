// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json;
using System.Text.Json.Serialization;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace idunno.AtProto.Sync;
#pragma warning restore IDE0130 // Namespace does not match folder structure

/// <summary>
/// Converts a <see cref="RepoStatus"/> to and from its JSON representation, mapping any status which is not
/// known to <see cref="RepoStatus.Unknown"/>.
/// </summary>
/// <remarks>
/// <para>The lexicon definition of a repository status is an open list of known values, so unknown statuses are surfaced
/// as <see cref="RepoStatus.Unknown"/> so the rest of the response can still be read.</para>
/// <para>Writing this converter by hand also avoids the trimming problems that the reflection based enum
/// converter exhibits. See https://github.com/dotnet/runtime/issues/114307.</para>
/// </remarks>
internal sealed class RepoStatusConverter : JsonConverter<RepoStatus>
{
    /// <inheritdoc/>
    public override RepoStatus Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.String)
        {
            throw new JsonException($"Expected a string when reading a {nameof(RepoStatus)}, found {reader.TokenType}.");
        }

        return reader.GetString() switch
        {
            "takendown" => RepoStatus.Takendown,
            "suspended" => RepoStatus.Suspended,
            "deleted" => RepoStatus.Deleted,
            "deactivated" => RepoStatus.Deactivated,
            "desynchronized" => RepoStatus.Desynchronized,
            "throttled" => RepoStatus.Throttled,
            _ => RepoStatus.Unknown
        };
    }

    /// <inheritdoc/>
    public override void Write(Utf8JsonWriter writer, RepoStatus value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);

        string status = value switch
        {
            RepoStatus.Takendown => "takendown",
            RepoStatus.Suspended => "suspended",
            RepoStatus.Deleted => "deleted",
            RepoStatus.Deactivated => "deactivated",
            RepoStatus.Desynchronized => "desynchronized",
            RepoStatus.Throttled => "throttled",
            _ => throw new JsonException(
                $"{nameof(RepoStatus)}.{nameof(RepoStatus.Unknown)} cannot be serialized. It only exists to carry a value this library does not recognize.")
        };

        writer.WriteStringValue(status);
    }
}
