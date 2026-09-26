// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json;
using System.Text.Json.Serialization;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace idunno.AtProto.Sync;
#pragma warning restore IDE0130 // Namespace does not match folder structure

/// <summary>
/// Converts a <see cref="HostStatus"/> to and from its JSON representation, mapping any status which is not
/// known to <see cref="HostStatus.Unknown"/>.
/// </summary>
/// <remarks>
/// <para>The lexicon definition of a host status is an open list of known values, so unknown statuses are surfaced
/// as <see cref="HostStatus.Unknown"/> so the rest of the response can still be read.</para>
/// <para>Writing this converter by hand also avoids the trimming problems that the reflection based enum
/// converter exhibits. See https://github.com/dotnet/runtime/issues/114307.</para>
/// </remarks>
internal sealed class HostStatusConverter : JsonConverter<HostStatus>
{
    /// <inheritdoc/>
    public override HostStatus Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.String)
        {
            throw new JsonException($"Expected a string when reading a {nameof(HostStatus)}, found {reader.TokenType}.");
        }

        return reader.GetString() switch
        {
            "active" => HostStatus.Active,
            "idle" => HostStatus.Idle,
            "offline" => HostStatus.Offline,
            "throttled" => HostStatus.Throttled,
            "banned" => HostStatus.Banned,
            _ => HostStatus.Unknown
        };
    }

    /// <inheritdoc/>
    public override void Write(Utf8JsonWriter writer, HostStatus value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);

        string status = value switch
        {
            HostStatus.Active => "active",
            HostStatus.Idle => "idle",
            HostStatus.Offline => "offline",
            HostStatus.Throttled => "throttled",
            HostStatus.Banned => "banned",
            _ => throw new JsonException(
                $"{nameof(HostStatus)}.{nameof(HostStatus.Unknown)} cannot be serialized. It only exists to carry a value this library does not recognize.")
        };

        writer.WriteStringValue(status);
    }
}
